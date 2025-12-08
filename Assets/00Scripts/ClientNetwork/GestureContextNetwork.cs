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
    public List<AvailableItemDTO> available_tools; // inventory items
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
    
    // public events
    public static event Action OnTriggerDataTransmission;
    public static event Action OnStartDataTransmission;
    public static event Action OnStopDataTransmission;

    private WebSocket _ws;
    private float _sendInterval;
    private GestureContextPayload _payload = new GestureContextPayload();
    
    // texture object for the screen capture
    private Texture2D _screenTexture;
    private RenderTexture _screenRenderTexture;
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

    private int _frameCounter = 0;
    private int _screenCaptureIntervalInFrames;
    private int _objCaptureIntervalInFrames;
    
    // Reusable textures for object capture
    private Texture2D _objectTexture;
    private RenderTexture _objectRenderTexture;
    
    private Collider[] _hitColliders = new Collider[50];

    private bool _shouldTransmitData = false;
    private float _dataSendDuration = 0f;

    public void TriggerDataTransmission(float duration)
    {
        OnTriggerDataTransmission?.Invoke();
        _dataSendDuration = duration;
        Invoke(nameof(StartDataTransmissionTimer), 1f);
    }
    
    private void StartDataTransmissionTimer()
    {
        StopCoroutine(nameof(DataTransmissionTimer));
        StartCoroutine(DataTransmissionTimer(_dataSendDuration));
    }

    private IEnumerator DataTransmissionTimer(float duration)
    {
        _shouldTransmitData = true;
        OnStartDataTransmission?.Invoke();
        
        yield return new WaitForSeconds(duration);
        
        _shouldTransmitData = false;
        OnStartDataTransmission?.Invoke();
    }
    
    private void Start()
    {
        if (!handProcessor || !vrCamera || !objectCaptureCamera || !playerHead)
        {
            Debug.LogError("Hand Processor or VR Cam or Obj Capture Cam is not assigned! Disabling network client.");
            this.enabled = false;
            return;
        }

        _objectRenderTexture = new RenderTexture(captureResolution, captureResolution, 24);
        _objectTexture = new Texture2D(captureResolution, captureResolution, TextureFormat.RGB24, false);
        
        objectCaptureCamera.enabled = false;
        objectCaptureCamera.targetTexture = _objectRenderTexture;
        
        _sendInterval = 1.0f / handDataFPS;
        
        _screenCaptureIntervalInFrames = Mathf.RoundToInt(handDataFPS / screenCaptureFPS);
        if (_screenCaptureIntervalInFrames < 1) _screenCaptureIntervalInFrames = 1;
        
        _objCaptureIntervalInFrames = Mathf.RoundToInt(handDataFPS / objectCaptureFPS);
        if (_objCaptureIntervalInFrames < 1) _objCaptureIntervalInFrames = 1;
        
        Debug.Log($"Sending hand data every {_sendInterval:F2}s ({handDataFPS} FPS).");
        Debug.Log($"Sending screen capture every {_screenCaptureIntervalInFrames} hand frames (approx {screenCaptureFPS} FPS).");
        
        _screenRenderTexture = new RenderTexture(screenCaptureWidth, screenCaptureHeight, 24);
        _screenTexture = new Texture2D(screenCaptureWidth, screenCaptureHeight, TextureFormat.RGB24, false);
        
        _ws = new WebSocket(serverUrl);
        _ws.OnOpen += (sender, e) =>
        {
            Debug.Log("<color=lime>Connected to Python WebSocket at " + serverUrl + "</color>");
            StartCoroutine(SendDataLoop());
        };
        
        // response from the backend of the selected object to attach to the hand
        _ws.OnMessage += (sender, e) =>
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                Debug.Log("Python Response: " + e.Data);
                
                if (actionManager)
                {
                    actionManager.ExecuteAction(e.Data);
                }
            });
        };
        
        _ws.OnError += (sender, e) => Debug.LogError("WebSocket Error: " + e.Message);
        
        _ws.OnClose += (sender, e) => Debug.Log("Disconnected from Python");

        Debug.Log("Attempting to connect to " + serverUrl);
        _ws.Connect();
    }
    
    private IEnumerator SendDataLoop()
    {
        while (_ws.ReadyState == WebSocketState.Open)
        {
            if (!_shouldTransmitData)
            {
                continue;
            }
            
            yield return new WaitForSeconds(_sendInterval);
            _frameCounter++;
            
            if (!handProcessor.IsDataReady()) continue;
            
            _payload.left_hand = handProcessor.leftHand.outputData;
            _payload.right_hand = handProcessor.rightHand.outputData;
            _payload.object_captures.Clear();

            if (_frameCounter % _screenCaptureIntervalInFrames == 0)
            {
                yield return StartCoroutine(CaptureScreenCoroutine());
            }
            
            if (_frameCounter % _objCaptureIntervalInFrames == 0)
            {
                yield return StartCoroutine(CaptureNearbyInteractableObjectsCoroutine());
            }
            
            if (actionManager)
            {
                _payload.available_tools = actionManager.GetAvailableItemsPayload();
            }
            
            string jsonData = JsonUtility.ToJson(_payload);
            _ws.Send(jsonData);

            _payload.screen_capture = null;
        }
    }

    private IEnumerator CaptureScreenCoroutine()
    {
        yield return _waitForEndOfFrame;
        
        vrCamera.targetTexture = _screenRenderTexture;
        vrCamera.Render();
        
        RenderTexture.active = _screenRenderTexture;
        _screenTexture.ReadPixels(new Rect(0, 0, screenCaptureWidth, screenCaptureHeight), 0, 0);
        _screenTexture.Apply();
        
        vrCamera.targetTexture = null;
        RenderTexture.active = null;
        
        byte[] imageBytes = _screenTexture.EncodeToJPG(jpgQuality);
        
        _payload.screen_capture = Convert.ToBase64String(imageBytes);
    }
    
    private IEnumerator CaptureNearbyInteractableObjectsCoroutine()
    {
        int numFound = Physics.OverlapSphereNonAlloc(
            playerHead.position, 
            contextRadius, 
            _hitColliders, 
            interactableLayers
        );
        
        int capturedCount = 0;
        
        for (int i = 0; i < numFound && capturedCount < maxObjectsToCapture; i++)
        {
            Collider objCollider = _hitColliders[i];
            Renderer objRenderer = objCollider.GetComponent<Renderer>();
            
            if (!objRenderer) continue;
            
            Bounds bounds = objRenderer.bounds;
            float objectSize = bounds.extents.magnitude;
            
            float camDistance = (objectSize / (2.0f * Mathf.Tan(objectCaptureCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)));
            camDistance *= 2.0f; // Add padding
            
            objectCaptureCamera.transform.position = bounds.center - (playerHead.forward * camDistance);
            objectCaptureCamera.transform.LookAt(bounds.center);
            
            yield return _waitForEndOfFrame;
            
            objectCaptureCamera.Render();
            
            RenderTexture.active = _objectRenderTexture;
            _objectTexture.ReadPixels(new Rect(0, 0, captureResolution, captureResolution), 0, 0);
            _objectTexture.Apply();
            RenderTexture.active = null;
            
            byte[] imageBytes = _objectTexture.EncodeToJPG(jpgQuality);
            string base64Image = Convert.ToBase64String(imageBytes);
            
            _payload.object_captures.Add(new ObjectCapturePayload {
                label = objCollider.gameObject.tag, // Use tag or name
                image_base64 = base64Image
            });
            
            capturedCount++;
        }
    }

    void OnDestroy()
    {
        _ws?.Close();
        if (_objectRenderTexture) Destroy(_objectRenderTexture);
        if (_objectTexture) Destroy(_objectTexture);
    }
}
