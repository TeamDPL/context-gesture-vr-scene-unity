using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class Landmark
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class HandOutputData
{
    public bool is_left_hand_wrist_based = true;
    public Landmark world_hand_wrist_root = new Landmark();
    public List<Landmark> relative_landmarks = new List<Landmark>();
}

[System.Serializable]
public class HandData
{
    [Tooltip("The OVRSkeleton component for this hand.")]
    public OVRSkeleton ovrSkeleton;

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


// --- Main MonoBehaviour (Now much cleaner!) ---
public class OVRSkeletonToMediaPipe : MonoBehaviour
{
    [Header("Hand Skeletons")]
    public HandData leftHand;
    public HandData rightHand;

    private Transform _currentReferenceHandWrist;
    private bool _isLeftHandWristBased;
    
    private readonly Dictionary<OVRSkeleton.BoneId, int> _boneIdToMediaPipeIndex = new Dictionary<OVRSkeleton.BoneId, int>
    {
        { OVRSkeleton.BoneId.XRHand_Wrist, 0 },
        { OVRSkeleton.BoneId.XRHand_ThumbMetacarpal, 1 }, { OVRSkeleton.BoneId.XRHand_ThumbProximal, 2 }, { OVRSkeleton.BoneId.XRHand_ThumbDistal, 3 }, { OVRSkeleton.BoneId.XRHand_ThumbTip, 4 },
        { OVRSkeleton.BoneId.XRHand_IndexProximal, 5 }, { OVRSkeleton.BoneId.XRHand_IndexIntermediate, 6 }, { OVRSkeleton.BoneId.XRHand_IndexDistal, 7 }, { OVRSkeleton.BoneId.XRHand_IndexTip, 8 },
        { OVRSkeleton.BoneId.XRHand_MiddleProximal, 9 }, { OVRSkeleton.BoneId.XRHand_MiddleIntermediate, 10 }, { OVRSkeleton.BoneId.XRHand_MiddleDistal, 11 }, { OVRSkeleton.BoneId.XRHand_MiddleTip, 12 },
        { OVRSkeleton.BoneId.XRHand_RingProximal, 13 }, { OVRSkeleton.BoneId.XRHand_RingIntermediate, 14 }, { OVRSkeleton.BoneId.XRHand_RingDistal, 15 }, { OVRSkeleton.BoneId.XRHand_RingTip, 16 },
        { OVRSkeleton.BoneId.XRHand_LittleProximal, 17 }, { OVRSkeleton.BoneId.XRHand_LittleIntermediate, 18 }, { OVRSkeleton.BoneId.XRHand_LittleDistal, 19 }, { OVRSkeleton.BoneId.XRHand_LittleTip, 20 }
    };

    private void Start()
    {
        if (leftHand.ovrSkeleton) leftHand.Initialize();
        if (rightHand.ovrSkeleton) rightHand.Initialize();
    }

    private void Update()
    {
        _currentReferenceHandWrist = null;
        
        if (IsHandTracked(leftHand))
        {
            _currentReferenceHandWrist = GetWristTransform(leftHand.ovrSkeleton);
            _isLeftHandWristBased = true;
        }

        if (!_currentReferenceHandWrist && IsHandTracked(rightHand))
        {
            _currentReferenceHandWrist = GetWristTransform(rightHand.ovrSkeleton);
            _isLeftHandWristBased = false;
        }

        if (_currentReferenceHandWrist)
        {
            if (IsHandTracked(leftHand))
            {
                ProcessHand(leftHand, _currentReferenceHandWrist, _isLeftHandWristBased);
            }

            if (IsHandTracked(rightHand))
            {
                ProcessHand(rightHand, _currentReferenceHandWrist, _isLeftHandWristBased);
            }
        }
    }

    private bool IsHandTracked(HandData hand)
    {
        return hand.ovrSkeleton && hand.ovrSkeleton.IsDataValid && hand.IsInitialized;
    }

    private Transform GetWristTransform(OVRSkeleton skeleton)
    {
        foreach (var bone in skeleton.Bones)
        {
            if (bone.Id == OVRSkeleton.BoneId.XRHand_Wrist)
            {
                return bone.Transform;
            }
        }
        return null;
    }
    
    public bool IsDataReady()
    {
        return IsHandTracked(leftHand) || IsHandTracked(rightHand);
    }

    private void ProcessHand(HandData hand, Transform referenceWrist, bool isLeftHandWristBased)
    {
        hand.outputData.is_left_hand_wrist_based = isLeftHandWristBased;
        
        hand.outputData.world_hand_wrist_root.x = referenceWrist.position.x;
        hand.outputData.world_hand_wrist_root.y = referenceWrist.position.y;
        hand.outputData.world_hand_wrist_root.z = referenceWrist.position.z;

        foreach (var bone in hand.ovrSkeleton.Bones)
        {
            if (_boneIdToMediaPipeIndex.TryGetValue(bone.Id, out int mediaPipeIndex))
            {
                Vector3 localPosition = referenceWrist.InverseTransformPoint(bone.Transform.position);
                
                // approximation
                if (localPosition.magnitude < 0.0001f) localPosition = Vector3.zero;
                
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
    }
}