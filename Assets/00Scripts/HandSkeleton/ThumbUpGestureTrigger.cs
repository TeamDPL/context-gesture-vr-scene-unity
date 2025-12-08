using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ManualGestureDetector : MonoBehaviour
{
    [Header("Dependencies")]
    public OVRSkeleton rightHandSkeleton; // Drag your OVRRightHandDataSource 

    [Header("Thresholds (Tune these in Play Mode)")]
    [Tooltip("If finger tip is closer to wrist than this, it is considered curled.")]
    // public float curlThreshold = 0.08f; // for thumb up
    public float curlThreshold = 0.14f;

    [Tooltip("If thumb tip is further from wrist than this, it is considered extended.")]
    //  public float thumbExtendThreshold = 0.06f; // for thumb up
    public float thumbExtendThreshold = 0.13f;

    [Header("Debug")]
    public bool isThumbsUp = false;
    public float currentIndexDist;
    public float currentThumbDistFromIndex;

    [SerializeField]
    private UnityEvent _whenSelected;

    private bool hasTriggered = false;

    void Update()
    {
        // 1. Safety Checks
        if (rightHandSkeleton == null || !rightHandSkeleton.IsDataValid) return;

        // 2. Get Key Bone Positions (World Space)
        Vector3 wristPos = GetBonePos(OVRSkeleton.BoneId.XRHand_Wrist);
        Vector3 thumbTip = GetBonePos(OVRSkeleton.BoneId.XRHand_ThumbTip);
        Vector3 indexTip = GetBonePos(OVRSkeleton.BoneId.XRHand_IndexTip);
        Vector3 middleTip = GetBonePos(OVRSkeleton.BoneId.XRHand_MiddleTip);
        Vector3 ringTip = GetBonePos(OVRSkeleton.BoneId.XRHand_RingTip);
        Vector3 pinkyTip = GetBonePos(OVRSkeleton.BoneId.XRHand_LittleTip);

        // 3. Calculate Distances to Wrist
        float thumbDistFromIndex = Vector3.Distance(thumbTip, indexTip);
        float indexDist = Vector3.Distance(indexTip, wristPos);
        float middleDist = Vector3.Distance(middleTip, wristPos);
        float ringDist = Vector3.Distance(ringTip, wristPos);
        float pinkyDist = Vector3.Distance(pinkyTip, wristPos);

        // Debug values to help you tune in Inspector
        currentThumbDistFromIndex = thumbDistFromIndex;
        currentIndexDist = pinkyDist;

        // 4. The Logic: Thumb OUT, Others IN
        bool thumbIsOut = thumbDistFromIndex < thumbExtendThreshold;
        //(indexDist < curlThreshold) &&
        bool fingersAreCurled = (middleDist > curlThreshold) &&
                                (ringDist > curlThreshold) &&
                                (pinkyDist > curlThreshold);

        // 5. Optional: Orientation Check (Thumb points UP)
        // We compare the Thumb Tip direction relative to the Wrist against Vector3.up
        Vector3 thumbDir = (thumbTip - wristPos).normalized;
        float dotUp = Vector3.Dot(thumbDir, Vector3.up);
        bool pointingUp = dotUp > 0.5f; // Roughly 45 degrees up or more

        // 6. Trigger
        if (thumbIsOut && fingersAreCurled)
        {
            isThumbsUp = true;
            if (!hasTriggered)
            {
                Debug.Log("<color=green>MANUAL THUMBS UP DETECTED!</color>");
                _whenSelected.Invoke();
                hasTriggered = true;
            }
        }
        else
        {
            isThumbsUp = false;
            hasTriggered = false;
        }
    }

    // Helper to safely get bone position
    private Vector3 GetBonePos(OVRSkeleton.BoneId id)
    {
        foreach (var bone in rightHandSkeleton.Bones)
        {
            if (bone.Id == id) return bone.Transform.position;
        }
        return Vector3.zero;
    }
}