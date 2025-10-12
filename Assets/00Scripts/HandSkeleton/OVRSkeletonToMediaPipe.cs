using UnityEngine;
using System.Collections.Generic;
using System.IO;

// --- Helper classes for JSON serialization (No changes here) ---
[System.Serializable]
public class Landmark
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class LandmarkList
{
    public List<Landmark> landmarks = new List<Landmark>();
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
    public LandmarkList landmarkList = new LandmarkList();

    public bool IsInitialized { get; set; } = false;

    public void Initialize()
    {
        for (int i = 0; i < 21; i++)
        {
            mediaPipeLandmarks.Add(Vector3.zero);
            landmarkList.landmarks.Add(new Landmark());
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
        { OVRSkeleton.BoneId.Hand_WristRoot, 0 },
        { OVRSkeleton.BoneId.Hand_Thumb0, 1 }, { OVRSkeleton.BoneId.Hand_Thumb1, 2 }, { OVRSkeleton.BoneId.Hand_Thumb2, 3 }, { OVRSkeleton.BoneId.Hand_Thumb3, 4 },
        { OVRSkeleton.BoneId.Hand_Index1, 5 }, { OVRSkeleton.BoneId.Hand_Index2, 6 }, { OVRSkeleton.BoneId.Hand_Index3, 7 }, { OVRSkeleton.BoneId.Hand_IndexTip, 8 },
        { OVRSkeleton.BoneId.Hand_Middle1, 9 }, { OVRSkeleton.BoneId.Hand_Middle2, 10 }, { OVRSkeleton.BoneId.Hand_Middle3, 11 }, { OVRSkeleton.BoneId.Hand_MiddleTip, 12 },
        { OVRSkeleton.BoneId.Hand_Ring1, 13 }, { OVRSkeleton.BoneId.Hand_Ring2, 14 }, { OVRSkeleton.BoneId.Hand_Ring3, 15 }, { OVRSkeleton.BoneId.Hand_RingTip, 16 },
        { OVRSkeleton.BoneId.Hand_Pinky1, 17 }, { OVRSkeleton.BoneId.Hand_Pinky2, 18 }, { OVRSkeleton.BoneId.Hand_Pinky3, 19 }, { OVRSkeleton.BoneId.Hand_PinkyTip, 20 }
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

        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveLandmarksToFile(leftHand);
            SaveLandmarksToFile(rightHand);
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
            hand.landmarkList.landmarks[i].x = hand.mediaPipeLandmarks[i].x;
            hand.landmarkList.landmarks[i].y = hand.mediaPipeLandmarks[i].y;
            hand.landmarkList.landmarks[i].z = hand.mediaPipeLandmarks[i].z;
        }
        hand.jsonOutput = JsonUtility.ToJson(hand.landmarkList, true);
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
}