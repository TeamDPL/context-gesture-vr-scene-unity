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
        OVRSkeleton.BoneId.XRHand_Wrist,
        // Thumb
        OVRSkeleton.BoneId.XRHand_ThumbMetacarpal, OVRSkeleton.BoneId.XRHand_ThumbProximal, OVRSkeleton.BoneId.XRHand_ThumbDistal, OVRSkeleton.BoneId.XRHand_ThumbTip,
        // Index
        OVRSkeleton.BoneId.XRHand_IndexProximal, OVRSkeleton.BoneId.XRHand_IndexIntermediate, OVRSkeleton.BoneId.XRHand_IndexDistal, OVRSkeleton.BoneId.XRHand_IndexTip,
        // Middle
        OVRSkeleton.BoneId.XRHand_MiddleProximal, OVRSkeleton.BoneId.XRHand_MiddleIntermediate, OVRSkeleton.BoneId.XRHand_MiddleDistal, OVRSkeleton.BoneId.XRHand_MiddleTip,
        // Ring
        OVRSkeleton.BoneId.XRHand_RingProximal, OVRSkeleton.BoneId.XRHand_RingIntermediate, OVRSkeleton.BoneId.XRHand_RingDistal, OVRSkeleton.BoneId.XRHand_RingTip,
        // Pinky
        OVRSkeleton.BoneId.XRHand_LittleProximal, OVRSkeleton.BoneId.XRHand_LittleIntermediate, OVRSkeleton.BoneId.XRHand_LittleDistal, OVRSkeleton.BoneId.XRHand_LittleTip,
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
            
            if (_mediaPipeBones.Contains(bone.Id) && bone.Id != OVRSkeleton.BoneId.XRHand_Wrist)
            {
                var line = bone.Transform.gameObject.AddComponent<LineRenderer>();
                line.material = lineMaterial;
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.positionCount = 2;
                line.useWorldSpace = false; // Remains local space.
                
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

            if (bone.Id != OVRSkeleton.BoneId.XRHand_IndexProximal
                && bone.Id != OVRSkeleton.BoneId.XRHand_MiddleProximal
                && bone.Id != OVRSkeleton.BoneId.XRHand_RingProximal
                && bone.Id != OVRSkeleton.BoneId.XRHand_LittleProximal)
            {
                Transform parentTransform = skeleton.Bones[bone.ParentBoneIndex].Transform;
                
                line.SetPosition(0, Vector3.zero); // Start at the current bone's own transform origin
                line.SetPosition(1, bone.Transform.InverseTransformPoint(parentTransform.position));
            }
            else
            {
                line.SetPosition(0, Vector3.zero); // Start at the current bone's own transform origin
                line.SetPosition(1, bone.Transform.InverseTransformPoint(skeleton.Bones[_bonesToUpdate[0].ParentBoneIndex].Transform.position));
            }
        }
    }
}