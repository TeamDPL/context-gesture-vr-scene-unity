using UnityEngine;
using System.Collections;
using WebSocketSharp;

[System.Serializable]
public class GesturePayload
{
    public HandOutputData left_hand;
    public HandOutputData right_hand;
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
    private float framesPerSecond = 20.0f; // 20 FPS

    private WebSocket ws;
    private float sendInterval;
    private GesturePayload payload = new GesturePayload();
    
    void Start()
    {
        if (handProcessor == null)
        {
            Debug.LogError("Hand Processor is not assigned! Disabling network client.");
            this.enabled = false;
            return;
        }

        sendInterval = 1.0f / framesPerSecond;
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
            
            string jsonData = JsonUtility.ToJson(payload);
            ws.Send(jsonData);
        }
    }

    void OnDestroy()
    {
        if (ws != null)
        {
            ws.Close();
        }
    }
}
