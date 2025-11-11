using UnityEngine;
using System;
using System.IO;
using System.Collections;

public class BoundingBoxCapture : MonoBehaviour
{
    // Assign your main VR camera here (e.g., the one attached to CenterEyeAnchor)
    [SerializeField]
    private Camera vrCamera;
    
    [SerializeField]
    private RenderTexture captureRenderTexture;
    
    [SerializeField]
    private RenderTexture sRGB_CaptureTexture;
    
    [SerializeField]
    private GameObject targetObject;
    
    [Header("Visualization")]
    public Material lineMaterial;
    [Range(0.001f, 0.01f)]
    public float lineWidth = 0.002f;

    private bool _isinitialized = false;

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Two))
            CaptureObjectBoundingBox((jpegBytes) => {
            
                if (jpegBytes != null)
                {
                    Debug.Log($"Capture successful! Got {jpegBytes.Length} bytes of JPG data.");
                    // Here, you would send the 'jpegBytes' array to your backend.
                    // For example: SendToBackend(jpegBytes);
                    
                    // save the file
                    string fileName = $"capture_{targetObject.name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.jpeg";
                    string path = Path.Combine(Application.persistentDataPath, fileName);
                    File.WriteAllBytes(path, jpegBytes);

                    Debug.Log($"<color=lime>Saved capture to: {path}</color>");
                }
                else
                {
                    Debug.LogError("Capture failed. The object might be off-screen.");
                }
            });
    }

    /// <summary>
    /// Starts the capture process for the specified target object.
    /// </summary>
    /// <param name="targetObject">The GameObject to capture.</param>
    /// <param name="onComplete">A callback action that receives the JPG byte array. The array will be null if capture fails.</param>
    public void CaptureObjectBoundingBox(Action<byte[]> onComplete)
    {
        StartCoroutine(CaptureAndEncode(targetObject, onComplete));
    }

    private IEnumerator CaptureAndEncode(GameObject targetObject, Action<byte[]> onComplete)
    {
        // Wait for the end of the frame, ensuring URP has completed its render to the headset.
        yield return new WaitForEndOfFrame();

        Rect rect = GetScreenSpaceRect(targetObject);

        if (rect.width <= 0 || rect.height <= 0)
        {
            Debug.LogWarning("Target object is not visible to the camera. Cannot capture.");
            onComplete?.Invoke(null);
            yield break;
        }
    
        // --- **NEW URP-NATIVE LOGIC** ---

        // 1. Take a snapshot of what the camera just rendered. 
        //    The 'null' source tells Blit to use the camera's current output.
        //    We copy this to our HDR texture to preserve the full lighting data.
        Graphics.Blit(null, captureRenderTexture);

        // 2. Now, convert the HDR snapshot to a standard sRGB texture for correct color.
        Graphics.Blit(captureRenderTexture, sRGB_CaptureTexture);

        // 3. Read the pixels from the final, color-corrected sRGB texture.
        Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
        RenderTexture.active = sRGB_CaptureTexture;
        texture.ReadPixels(rect, 0, 0);
        texture.Apply();
        RenderTexture.active = null;

        // --- **END OF NEW LOGIC** ---

        // Encode to JPG and return the data
        byte[] jpegData = texture.EncodeToJPG(90);
        Destroy(texture);
        onComplete?.Invoke(jpegData);
    }

    /// <summary>
    /// Calculates the 2D screen-space rectangle that encompasses the target GameObject.
    /// </summary>
    /// <param name="target">The game object to calculate bounds for.</param>
    /// <returns>A Rect representing the bounding box in screen coordinates.</returns>
    private Rect GetScreenSpaceRect(GameObject target)
{
    if (!target || !vrCamera) return new Rect();
    
    Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
    if (renderers.Length == 0) return new Rect();

    Bounds totalBounds = renderers[0].bounds;
    for (int i = 1; i < renderers.Length; i++)
    {
        totalBounds.Encapsulate(renderers[i].bounds);
    }

    Vector3[] corners = new Vector3[8];
    corners[0] = totalBounds.center + new Vector3(-totalBounds.extents.x, -totalBounds.extents.y, -totalBounds.extents.z);
    corners[1] = totalBounds.center + new Vector3( totalBounds.extents.x, -totalBounds.extents.y, -totalBounds.extents.z);
    corners[2] = totalBounds.center + new Vector3(-totalBounds.extents.x, -totalBounds.extents.y,  totalBounds.extents.z);
    corners[3] = totalBounds.center + new Vector3( totalBounds.extents.x, -totalBounds.extents.y,  totalBounds.extents.z);
    corners[4] = totalBounds.center + new Vector3(-totalBounds.extents.x,  totalBounds.extents.y, -totalBounds.extents.z);
    corners[5] = totalBounds.center + new Vector3( totalBounds.extents.x,  totalBounds.extents.y, -totalBounds.extents.z);
    corners[6] = totalBounds.center + new Vector3(-totalBounds.extents.x,  totalBounds.extents.y,  totalBounds.extents.z);
    corners[7] = totalBounds.center + new Vector3( totalBounds.extents.x,  totalBounds.extents.y,  totalBounds.extents.z);
    
    float minX = float.MaxValue, minY = float.MaxValue;
    float maxX = float.MinValue, maxY = float.MinValue;

    for (int i = 0; i < corners.Length; i++)
    {
        // Get the coordinate from the camera's perspective
        Vector3 screenPoint = vrCamera.WorldToScreenPoint(corners[i]);
        
        // Only consider points that are in front of the camera
        if (screenPoint.z > 0)
        {
            // --- **THIS IS THE FIX** ---
            // We rescale the point from the camera's output dimensions 
            // to the render texture's dimensions.
            float scaledX = screenPoint.x * ((float)captureRenderTexture.width / vrCamera.pixelWidth);
            float scaledY = screenPoint.y * ((float)captureRenderTexture.height / vrCamera.pixelHeight);

            minX = Mathf.Min(minX, scaledX);
            minY = Mathf.Min(minY, scaledY);
            maxX = Mathf.Max(maxX, scaledX);
            maxY = Mathf.Max(maxY, scaledY);
        }
    }

    // Clamp values to be safely within the render texture dimensions
    minX = Mathf.Clamp(minX, 0, captureRenderTexture.width);
    maxX = Mathf.Clamp(maxX, 0, captureRenderTexture.width);
    minY = Mathf.Clamp(minY, 0, captureRenderTexture.height);
    maxY = Mathf.Clamp(maxY, 0, captureRenderTexture.height);

    return new Rect(minX, minY, maxX - minX, maxY - minY);
}
}