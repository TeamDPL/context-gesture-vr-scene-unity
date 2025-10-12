using UnityEngine;
using System.Collections.Generic;

public class HandSkeletonVisualizer : MonoBehaviour
{
    [Tooltip("Drag the OVRSkeleton with 'Update Root Pose' CHECKED here.")]
    public OVRSkeleton skeleton;

    [Header("Visualization")]
    public GameObject jointPrefab;
    public Material lineMaterial;
    [Range(0.001f, 0.01f)]
    public float lineWidth = 0.002f;
    [Range(0.001f, 0.02f)]
    public float jointScale = 0.005f;

    private bool _isInitialized = false;
    // We need to store a reference to the lines to update them.
    private List<LineRenderer> _boneLines = new List<LineRenderer>();
    private List<OVRBone> _bonesToUpdate = new List<OVRBone>();
    
    private readonly HashSet<OVRSkeleton.BoneId> _mediaPipeBones = new HashSet<OVRSkeleton.BoneId>
    {
        // Wrist
        OVRSkeleton.BoneId.Hand_WristRoot,
        // Thumb
        OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.Hand_Thumb2, OVRSkeleton.BoneId.Hand_Thumb3,
        // Index
        OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.Hand_Index3, OVRSkeleton.BoneId.Hand_IndexTip,
        // Middle
        OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_Middle2, OVRSkeleton.BoneId.Hand_Middle3, OVRSkeleton.BoneId.Hand_MiddleTip,
        // Ring
        OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.Hand_Ring3, OVRSkeleton.BoneId.Hand_RingTip,
        // Pinky
        OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.Hand_Pinky3, OVRSkeleton.BoneId.Hand_PinkyTip
        // Note: Hand_Thumb0 (THUMB_CMC in MediaPipe) is visually represented by the line from Wrist to Thumb1.
    };

    void Update()
    {
        if (!skeleton) return;

        // One-time setup when the skeleton is ready.
        if (!_isInitialized && skeleton.IsInitialized)
        {
            CreateVisuals();
            _isInitialized = true;
        }

        // Continuously update line endpoints every frame after setup.
        if (_isInitialized)
        {
            UpdateLines();
        }
    }

    private void CreateVisuals()
    {
        foreach (var bone in skeleton.Bones)
        {
            if (!_mediaPipeBones.Contains(bone.Id))
            {
                continue; // Skip this bone
            }
            
            // Create the joint dot and parent it to the bone transform.
            if (jointPrefab)
            {
                GameObject jointObj = Instantiate(jointPrefab, bone.Transform);
                jointObj.transform.localPosition = Vector3.zero;
                jointObj.transform.localRotation = Quaternion.identity;
                jointObj.transform.localScale = Vector3.one * jointScale;
            }

            // Create and configure the bone line.
            if (bone.ParentBoneIndex != -1)
            {
                LineRenderer line = bone.Transform.gameObject.AddComponent<LineRenderer>();
                line.material = lineMaterial;
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.positionCount = 2;
                line.useWorldSpace = false; // Use local space for efficiency.
                line.SetPosition(0, Vector3.zero); // Start point is always the bone's origin.

                // Store the line renderer so we can update it later.
                _boneLines.Add(line);
                _bonesToUpdate.Add(bone);
            }
        }
    }

    private void UpdateLines()
    {
        for (int i = 0; i < _bonesToUpdate.Count; i++)
        {
            OVRBone bone = _bonesToUpdate[i];
            LineRenderer line = _boneLines[i];
            
            Transform parentTransform = skeleton.Bones[bone.ParentBoneIndex].Transform;
            Vector3 parentLocalPosition = bone.Transform.InverseTransformPoint(parentTransform.position);
            line.SetPosition(1, parentLocalPosition);
        }
    }
}