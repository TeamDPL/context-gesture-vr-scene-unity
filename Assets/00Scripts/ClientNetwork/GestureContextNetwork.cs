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
public class GestureContextPayload
{
    public HandOutputData left_hand;
    public HandOutputData right_hand;
    public string screen_capture;
    public List<ObjectCapturePayload> object_captures = new List<ObjectCapturePayload>();
    public List<AvailableItemDTO> available_tools;
    // or other data
}

public class GestureContextNetwork : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the object that has your OVRSkeletonToMediaPipe script here.")]
    [SerializeField]
    private OVRSkeletonToMediaPipe handProcessor;

    [Header("Network Settings")]
    [Tooltip("The *WebSocket* URL for your backend.")]
    [SerializeField]
    private string serverUrl = "ws://127.0.0.1:8000/ws/process-gesture-context-stream";
    
    [Tooltip("How many times per second to send hand data.")]
    [SerializeField]
    private float handDataFPS = 20.0f;
    
    [Tooltip("How many times per second to send screen image data.")]
    [SerializeField]
    private float screenCaptureFPS = 5.0f;
    
    [Tooltip("How many times per second to send object image data.")]
    [SerializeField]
    private float objectCaptureFPS = 5.0f;
    
    [Header("Screen Capture Settings")]
    [Tooltip("The VR Camera to capture from.")]
    [SerializeField]
    private Camera vrCamera;
    
    [Header("Object Capture Settings")]
    [Tooltip("The invisible camera for capturing objects.")]
    [SerializeField]
    private Camera objectCaptureCamera;
    
    [Tooltip("Output resolution. Lower is much faster.")]
    [SerializeField]
    private int screenCaptureWidth = 640;
    [SerializeField]
    private int screenCaptureHeight = 480;
    
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
    
    [Header("Action Manager")]
    [SerializeField]
    private ActionManager actionManager;

    private WebSocket ws;
    private float sendInterval;
    private GestureContextPayload payload = new GestureContextPayload();
    
    // texture object for the screen capture
    private Texture2D screenTexture;
    private RenderTexture screenRenderTexture;
    private WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

    private int frameCounter = 0;
    private int screenCaptureIntervalInFrames;
    private int objCaptureIntervalInFrames;
    
    // Reusable textures for object capture
    private Texture2D objectTexture;
    private RenderTexture objectRenderTexture;
    
    private Collider[] hitColliders = new Collider[50];
    
    void Start()
    {
        if (!handProcessor || !vrCamera || !objectCaptureCamera || !playerHead)
        {
            Debug.LogError("Hand Processor or VR Cam or Obj Capture Cam is not assigned! Disabling network client.");
            this.enabled = false;
            return;
        }

        objectRenderTexture = new RenderTexture(captureResolution, captureResolution, 24);
        objectTexture = new Texture2D(captureResolution, captureResolution, TextureFormat.RGB24, false);
        
        objectCaptureCamera.enabled = false;
        objectCaptureCamera.targetTexture = objectRenderTexture;
        
        sendInterval = 1.0f / handDataFPS;
        
        screenCaptureIntervalInFrames = Mathf.RoundToInt(handDataFPS / screenCaptureFPS);
        if (screenCaptureIntervalInFrames < 1) screenCaptureIntervalInFrames = 1;
        
        objCaptureIntervalInFrames = Mathf.RoundToInt(handDataFPS / objectCaptureFPS);
        if (objCaptureIntervalInFrames < 1) objCaptureIntervalInFrames = 1;
        
        Debug.Log($"Sending hand data every {sendInterval:F2}s ({handDataFPS} FPS).");
        Debug.Log($"Sending screen capture every {screenCaptureIntervalInFrames} hand frames (approx {screenCaptureFPS} FPS).");
        
        screenRenderTexture = new RenderTexture(screenCaptureWidth, screenCaptureHeight, 24);
        screenTexture = new Texture2D(screenCaptureWidth, screenCaptureHeight, TextureFormat.RGB24, false);
        
        ws = new WebSocket(serverUrl);
        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("<color=lime>Connected to Python WebSocket at " + serverUrl + "</color>");
            StartCoroutine(SendDataLoop());
        };
        
        // response from the backend of the selected object to attach to the hand
        ws.OnMessage += (sender, e) =>
        {
            Debug.Log("Python Response: " + e.Data);
            MainThreadDispatcher.Enqueue(() =>
            {
                Debug.Log("Python Response: " + e.Data);
                
                if (actionManager)
                {
                    actionManager.ExecuteAction(e.Data);
                }
            });
        };
        
        ws.OnError += (sender, e) => Debug.LogError("WebSocket Error: " + e.Message);
        
        ws.OnClose += (sender, e) => Debug.Log("Disconnected from Python");

        Debug.Log("Attempting to connect to " + serverUrl);
        ws.Connect();
    }
    
    private IEnumerator SendDataLoop()
    {
        while (ws.ReadyState == WebSocketState.Open)
        {
            yield return new WaitForSeconds(sendInterval);
            frameCounter++;
            
            if (!handProcessor.IsDataReady()) continue;
            
            payload.left_hand = handProcessor.leftHand.outputData;
            payload.right_hand = handProcessor.rightHand.outputData;
            payload.object_captures.Clear();

            if (frameCounter % screenCaptureIntervalInFrames == 0)
            {
                yield return StartCoroutine(CaptureScreenCoroutine());
            }
            
            if (frameCounter % objCaptureIntervalInFrames == 0)
            {
                yield return StartCoroutine(CaptureNearbyInteractableObjectsCoroutine());
            }
            
            if (actionManager)
            {
                payload.available_tools = actionManager.GetAvailableItemsPayload();
            }
            
            string jsonData = JsonUtility.ToJson(payload);
            ws.Send(jsonData);

            payload.screen_capture = null;
        }
    }

    private IEnumerator CaptureScreenCoroutine()
    {
        yield return waitForEndOfFrame;
        
        vrCamera.targetTexture = screenRenderTexture;
        vrCamera.Render();
        
        RenderTexture.active = screenRenderTexture;
        screenTexture.ReadPixels(new Rect(0, 0, screenCaptureWidth, screenCaptureHeight), 0, 0);
        screenTexture.Apply();
        
        vrCamera.targetTexture = null;
        RenderTexture.active = null;
        
        byte[] imageBytes = screenTexture.EncodeToJPG(jpgQuality);
        
        payload.screen_capture = Convert.ToBase64String(imageBytes);
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
            
            if (!objRenderer) continue;
            
            Bounds bounds = objRenderer.bounds;
            float objectSize = bounds.extents.magnitude;
            
            float camDistance = (objectSize / (2.0f * Mathf.Tan(objectCaptureCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)));
            camDistance *= 2.0f; // Add padding
            
            objectCaptureCamera.transform.position = bounds.center - (playerHead.forward * camDistance);
            objectCaptureCamera.transform.LookAt(bounds.center);
            
            yield return waitForEndOfFrame;
            
            objectCaptureCamera.Render();
            
            RenderTexture.active = objectRenderTexture;
            objectTexture.ReadPixels(new Rect(0, 0, captureResolution, captureResolution), 0, 0);
            objectTexture.Apply();
            RenderTexture.active = null;
            
            byte[] imageBytes = objectTexture.EncodeToJPG(jpgQuality);
            string base64Image = Convert.ToBase64String(imageBytes);
            
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
