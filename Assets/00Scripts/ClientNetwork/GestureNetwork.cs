using System;
using UnityEngine;
using System.Collections;
using WebSocketSharp;

[System.Serializable]
public class GesturePayload
{
    public HandOutputData left_hand;
    public HandOutputData right_hand;
    public string screen_capture;
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
    
    [Tooltip("How many times per second to send hand data.")]
    [SerializeField]
    private float screenCaptureFPS = 5.0f;
    
    [Header("Screen Capture Settings")]
    [Tooltip("The VR Camera to capture from.")]
    [SerializeField]
    private Camera vrCamera;
    
    [Tooltip("Output resolution. Lower is much faster.")]
    [SerializeField]
    private int captureWidth = 640;
    [SerializeField]
    private int captureHeight = 480;
    [Range(1, 100)]
    [SerializeField]
    private int jpgQuality = 75;

    private WebSocket ws;
    private float sendInterval;
    private GesturePayload payload = new GesturePayload();
    
    // texture object for screen capture
    private Texture2D captureTexture;
    private RenderTexture renderTexture;
    private WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

    private int frameCounter = 0;
    private int captureIntervalInFrames;
    
    void Start()
    {
        if (handProcessor == null || vrCamera == null)
        {
            Debug.LogError("Hand Processor or VR Cam is not assigned! Disabling network client.");
            this.enabled = false;
            return;
        }

        sendInterval = 1.0f / handDataFPS;
        
        if (screenCaptureFPS <= 0) screenCaptureFPS = 1;
        captureIntervalInFrames = Mathf.RoundToInt(handDataFPS / screenCaptureFPS);
        if (captureIntervalInFrames < 1) captureIntervalInFrames = 1;
        
        Debug.Log($"Sending hand data every {sendInterval:F2}s ({handDataFPS} FPS).");
        Debug.Log($"Sending screen capture every {captureIntervalInFrames} hand frames (approx {screenCaptureFPS} FPS).");
        
        renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        captureTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        
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
            
            if (!handProcessor.IsDataReady())
            {
                continue;
            }
            
            payload.left_hand = handProcessor.leftHand.outputData;
            payload.right_hand = handProcessor.rightHand.outputData;

            if (frameCounter % captureIntervalInFrames == 0)
            {
                yield return StartCoroutine(CaptureScreenCoroutine());
            }
            
            string jsonData = JsonUtility.ToJson(payload);
            ws.Send(jsonData);
            
            if (payload.screen_capture != null)
            {
                payload.screen_capture = null;
            }
        }
    }
    
    private IEnumerator CaptureScreenCoroutine()
    {
        yield return waitForEndOfFrame;
        
        vrCamera.targetTexture = renderTexture;
        vrCamera.Render();
        
        RenderTexture.active = renderTexture;
        captureTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        captureTexture.Apply();
        
        vrCamera.targetTexture = null;
        RenderTexture.active = null;
        
        byte[] imageBytes = captureTexture.EncodeToJPG(jpgQuality);
        
        payload.screen_capture = Convert.ToBase64String(imageBytes);
    }

    void OnDestroy()
    {
        if (ws != null)
        {
            ws.Close();
        }
        
        if (renderTexture != null) Destroy(renderTexture);
        if (captureTexture != null) Destroy(captureTexture);
    }
}
