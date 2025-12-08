using UnityEngine;

public class HeadOverlayUI : MonoBehaviour
{
    public Transform cameraTransform; // Assign CenterEyeAnchor here
    public float distance = 1.0f;
    public float smoothTime = 0.3f;

    private Vector3 _velocity = Vector3.zero;

    void Update()
    {
        if (!cameraTransform) return;

        // Calculate target position in front of the camera
        Vector3 targetPosition = cameraTransform.position + (cameraTransform.forward * distance);
        
        // Force the UI to look at the camera
        transform.LookAt(transform.position + cameraTransform.rotation * Vector3.forward, 
            cameraTransform.rotation * Vector3.up);

        // Smoothly move the UI to the target position
        // transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, smoothTime);
        transform.position = targetPosition;
    }
}