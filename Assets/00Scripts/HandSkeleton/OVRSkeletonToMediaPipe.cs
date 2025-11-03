using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine.Networking;

// --- Helper classes for JSON serialization (No changes here) ---
[System.Serializable]
public class Landmark
{
    public float x;
    public float y;
    public float z;
}

/*[System.Serializable]
public class LandmarkList
{
    public List<Landmark> landmarks = new List<Landmark>();
}*/

[System.Serializable]
public class BothHandsData
{
    public string left_hand_coords;
    public string right_hand_coords;
}

[System.Serializable]
public class HandOutputData
{
    public Landmark world_wrist_root = new Landmark();
    public List<Landmark> relative_landmarks = new List<Landmark>();
}

// --- Class to manage data for a single hand (No changes here) ---
[System.Serializable]
public class HandData
{
    [Tooltip("The OVRSkeleton component for this hand.")]
    public OVRSkeleton ovrSkeleton;

    [Header("Output")]
    [Tooltip("The final JSON output for this hand.")]
    [TextArea(5, 10)]
    public string jsonOutput;

    [HideInInspector]
    public List<Vector3> mediaPipeLandmarks = new List<Vector3>(21);
    
    [HideInInspector]
    public HandOutputData outputData = new HandOutputData();

    public bool IsInitialized { get; set; } = false;

    public void Initialize()
    {
        for (int i = 0; i < 21; i++)
        {
            mediaPipeLandmarks.Add(Vector3.zero);
            outputData.relative_landmarks.Add(new Landmark());
        }
        IsInitialized = true;
    }
}


// --- Main MonoBehaviour ---
public class OVRSkeletonToMediaPipe : MonoBehaviour
{
    [Header("Hand Skeletons")]
    public HandData leftHand;
    public HandData rightHand;

    // CHANGE #1: The Dictionary now uses OVRSkeleton.BoneId as its key.
    private readonly Dictionary<OVRSkeleton.BoneId, int> _boneIdToMediaPipeIndex = new Dictionary<OVRSkeleton.BoneId, int>
    {
        // CHANGE #2: All enum values now reference OVRSkeleton.BoneId.
        { OVRSkeleton.BoneId.XRHand_Wrist, 0 },
        { OVRSkeleton.BoneId.XRHand_ThumbMetacarpal, 1 }, { OVRSkeleton.BoneId.XRHand_ThumbProximal, 2 }, { OVRSkeleton.BoneId.XRHand_ThumbDistal, 3 }, { OVRSkeleton.BoneId.XRHand_ThumbTip, 4 },
        { OVRSkeleton.BoneId.XRHand_IndexProximal, 5 }, { OVRSkeleton.BoneId.XRHand_IndexIntermediate, 6 }, { OVRSkeleton.BoneId.XRHand_IndexDistal, 7 }, { OVRSkeleton.BoneId.XRHand_IndexTip, 8 },
        { OVRSkeleton.BoneId.XRHand_MiddleProximal, 9 }, { OVRSkeleton.BoneId.XRHand_MiddleIntermediate, 10 }, { OVRSkeleton.BoneId.XRHand_MiddleDistal, 11 }, { OVRSkeleton.BoneId.XRHand_MiddleTip, 12 },
        { OVRSkeleton.BoneId.XRHand_RingProximal, 13 }, { OVRSkeleton.BoneId.XRHand_RingIntermediate, 14 }, { OVRSkeleton.BoneId.XRHand_RingDistal, 15 }, { OVRSkeleton.BoneId.XRHand_RingTip, 16 },
        { OVRSkeleton.BoneId.XRHand_LittleProximal, 17 }, { OVRSkeleton.BoneId.XRHand_LittleIntermediate, 18 }, { OVRSkeleton.BoneId.XRHand_LittleDistal, 19 }, { OVRSkeleton.BoneId.XRHand_LittleTip, 20 }
    };

    void Start()
    {
        if (leftHand.ovrSkeleton != null) leftHand.Initialize();
        if (rightHand.ovrSkeleton != null) rightHand.Initialize();
    }

    void Update()
    {
        ProcessHand(leftHand);
        ProcessHand(rightHand);

        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            // SaveLandmarksToFile(leftHand);
            // SaveLandmarksToFile(rightHand);
            SendGestureData();
        }
        
        
    }

    private void ProcessHand(HandData hand)
    {
        if (!hand.ovrSkeleton || !hand.ovrSkeleton.IsDataValid || !hand.IsInitialized)
        {
            return;
        }
        
        Transform wristRoot = hand.ovrSkeleton.Bones[(int)OVRSkeleton.BoneId.Hand_WristRoot].Transform;
        if (!wristRoot) return;
        
        Vector3 wristWorldPosition = wristRoot.position;
        hand.outputData.world_wrist_root.x = wristWorldPosition.x;
        hand.outputData.world_wrist_root.y = wristWorldPosition.y;
        hand.outputData.world_wrist_root.z = wristWorldPosition.z;

        foreach (var bone in hand.ovrSkeleton.Bones)
        {
            if (_boneIdToMediaPipeIndex.TryGetValue(bone.Id, out int mediaPipeIndex))
            {
                Vector3 localPosition = wristRoot.InverseTransformPoint(bone.Transform.position);
                Vector3 mediaPipePosition = new Vector3(localPosition.x, -localPosition.y, localPosition.z);
                hand.mediaPipeLandmarks[mediaPipeIndex] = mediaPipePosition;
            }
        }

        for (int i = 0; i < hand.mediaPipeLandmarks.Count; i++)
        {
            hand.outputData.relative_landmarks[i].x = hand.mediaPipeLandmarks[i].x;
            hand.outputData.relative_landmarks[i].y = hand.mediaPipeLandmarks[i].y;
            hand.outputData.relative_landmarks[i].z = hand.mediaPipeLandmarks[i].z;
        }
        hand.jsonOutput = JsonUtility.ToJson(hand.outputData, true);
    }

    public void SaveLandmarksToFile(HandData hand)
    {
        if (!hand.ovrSkeleton || !hand.ovrSkeleton.IsDataValid) return;

        string handType = hand.ovrSkeleton.GetSkeletonType().ToString();
        string fileName = $"hand_landmarks_{handType}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllText(path, hand.jsonOutput);
        Debug.Log($"<color=lime>Saved {handType} landmarks to: {path}</color>");
    }

    public void SendGestureData()
    {
        StartCoroutine(PostGestureRequest("http://127.0.0.1:8000/process-gesture/"));
    }

    IEnumerator PostGestureRequest(string url)
    {
        BothHandsData data = new BothHandsData();

        data.left_hand_coords = leftHand.jsonOutput;
        data.right_hand_coords = rightHand.jsonOutput;
        
        string jsonData = JsonUtility.ToJson(data, true);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // 4. Send the request and wait for a response
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 5. Successfully received a response from Python
                Debug.Log("Python Response: " + request.downloadHandler.text);
                // You can now parse this response (e.g., using JsonUtility.FromJson)
                // and trigger the action in-game.
            }
            else
            {
                Debug.LogError("Error: " + request.error);
            }
        }
    }
}