using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using WebSocketSharp;

[System.Serializable]
public class ObjectCapturePayload
{
    public string label;
    public string image_base64;
}

[System.Serializable]
public class GesturePayload
{
    public HandOutputData left_hand;
    public HandOutputData right_hand;
    public List<ObjectCapturePayload> object_captures = new List<ObjectCapturePayload>();
    // or other data
}

public class GestureNetwork : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the object that has your OVRSkeletonToMediaPipe script here.")]
    [SerializeField]
    private OVRSkeletonToMediaPipe handProcessor;

    [Header("Network Settings")]
    [Tooltip("The *WebSocket* URL for your backend.")]
    [SerializeField]
    private string serverUrl = "ws://127.0.0.1:8000/ws/process-gesture-stream";
    
    [Tooltip("How many times per second to send hand data.")]
    [SerializeField]
    private float handDataFPS = 20.0f; // 20 FPS
    
    [Tooltip("How many times per second to send object image data.")]
    [SerializeField]
    private float objectCaptureFPS = 5.0f;
    
    [Header("Object Capture Settings")]
    [Tooltip("The invisible camera for capturing objects.")]
    [SerializeField]
    private Camera objectCaptureCamera;
    
    [Tooltip("The physics layers to check for interactable objects.")]
    [SerializeField]
    private LayerMask interactableLayers;
    
    [Tooltip("The player's head/camera to find nearby objects.")]
    [SerializeField]
    private Transform playerHead;
    
    [Tooltip("How far around the player to check for objects.")]
    [SerializeField]
    private float contextRadius = 15.0f;
    
    [Tooltip("Max number of objects to capture per frame (to save performance).")]
    [SerializeField]
    private int maxObjectsToCapture = 10;
    
    [Tooltip("Resolution for each captured object image.")]
    [SerializeField]
    private int captureResolution = 128;
    
    [Tooltip("JPG quality for captured objects.")]
    [Range(1, 100)]
    [SerializeField]
    private int jpgQuality = 75;

    private WebSocket ws;
    private float sendInterval;
    private GesturePayload payload = new GesturePayload();
    
    // texture object for the screen capture
    private Texture2D captureTexture;
    private RenderTexture renderTexture;
    private WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

    private int frameCounter = 0;
    private int captureIntervalInFrames;
    
    // Reusable textures for object capture
    private Texture2D objectTexture;
    private RenderTexture objectRenderTexture;
    
    private Collider[] hitColliders = new Collider[50];
    
    void Start()
    {
        if (handProcessor == null || objectCaptureCamera == null || playerHead == null)
        {
            Debug.LogError("Hand Processor or VR Cam is not assigned! Disabling network client.");
            this.enabled = false;
            return;
        }

        objectRenderTexture = new RenderTexture(captureResolution, captureResolution, 24);
        objectTexture = new Texture2D(captureResolution, captureResolution, TextureFormat.RGB24, false);
        
        objectCaptureCamera.enabled = false;
        objectCaptureCamera.targetTexture = objectRenderTexture;
        
        sendInterval = 1.0f / handDataFPS;
        captureIntervalInFrames = Mathf.RoundToInt(handDataFPS / objectCaptureFPS);
        if (captureIntervalInFrames < 1) captureIntervalInFrames = 1;
        
        Debug.Log($"Sending hand data every {sendInterval:F2}s ({handDataFPS} FPS).");
        Debug.Log($"Sending screen capture every {captureIntervalInFrames} hand frames (approx {captureIntervalInFrames} FPS).");
        
        ws = new WebSocket(serverUrl);
        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("<color=lime>Connected to Python WebSocket at " + serverUrl + "</color>");
            StartCoroutine(SendHandDataLoop());
        };
        ws.OnMessage += (sender, e) =>
        {
            Debug.Log("Python Response: " + e.Data);
        };
        ws.OnError += (sender, e) => Debug.LogError("WebSocket Error: " + e.Message);
        ws.OnClose += (sender, e) => Debug.Log("Disconnected from Python");

        Debug.Log("Attempting to connect to " + serverUrl);
        ws.Connect();
    }
    
    private IEnumerator SendHandDataLoop()
    {
        while (ws.ReadyState == WebSocketState.Open)
        {
            yield return new WaitForSeconds(sendInterval);
            frameCounter++;
            
            if (!handProcessor.IsDataReady()) continue;
            
            payload.left_hand = handProcessor.leftHand.outputData;
            payload.right_hand = handProcessor.rightHand.outputData;
            payload.object_captures.Clear();

            if (frameCounter % captureIntervalInFrames == 0)
            {
                yield return StartCoroutine(CaptureNearbyInteractableObjectsCoroutine());
            }
            
            string jsonData = JsonUtility.ToJson(payload);
            ws.Send(jsonData);
        }
    }
    
    private IEnumerator CaptureNearbyInteractableObjectsCoroutine()
    {
        int numFound = Physics.OverlapSphereNonAlloc(
            playerHead.position, 
            contextRadius, 
            hitColliders, 
            interactableLayers
        );
        
        int capturedCount = 0;
        for (int i = 0; i < numFound && capturedCount < maxObjectsToCapture; i++)
        {
            Collider objCollider = hitColliders[i];
            Renderer objRenderer = objCollider.GetComponent<Renderer>();
            
            // We need a renderer to know the object's size
            if (objRenderer == null) continue;

            // 2. Frame the object
            Bounds bounds = objRenderer.bounds;
            float objectSize = bounds.extents.magnitude;
            
            // Calculate distance to keep camera to frame object
            // This formula is derived from camera FOV trigonometry
            float camDistance = (objectSize / (2.0f * Mathf.Tan(objectCaptureCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)));
            camDistance *= 2.0f; // Add padding
            
            // Position camera and look at the object's center
            objectCaptureCamera.transform.position = bounds.center - (playerHead.forward * camDistance);
            objectCaptureCamera.transform.LookAt(bounds.center);

            // 3. Render and Encode
            // We must wait for the end of the frame
            yield return waitForEndOfFrame;
            
            // Tell the camera to render *once*
            objectCaptureCamera.Render();

            // Read pixels from the RenderTexture
            RenderTexture.active = objectRenderTexture;
            objectTexture.ReadPixels(new Rect(0, 0, captureResolution, captureResolution), 0, 0);
            objectTexture.Apply();
            RenderTexture.active = null;

            // Encode to JPG and Base64
            byte[] imageBytes = objectTexture.EncodeToJPG(jpgQuality);
            string base64Image = Convert.ToBase64String(imageBytes);

            // 4. Add to payload
            payload.object_captures.Add(new ObjectCapturePayload {
                label = objCollider.gameObject.tag, // Use tag or name
                image_base64 = base64Image
            });
            
            capturedCount++;
        }
    }

    void OnDestroy()
    {
        ws?.Close();
        if (objectRenderTexture) Destroy(objectRenderTexture);
        if (objectTexture) Destroy(objectTexture);
    }
}
