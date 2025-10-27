using System;
using UnityEngine;
using System.IO;
using System.Collections;
using Object = UnityEngine.Object;

public class ObjectBoudingBox : MonoBehaviour
{
    [Tooltip("Assign the World Space Canvas that holds your highlight image.")]
    public RectTransform highlightCanvas;

    [Tooltip("How far the highlight should appear in front of the object.")]
    public float offset = 0.01f;
    
    public Transform _gazedAtObject;
    private Rect _boundingBox2D;

    private void Start()
    {
        if (highlightCanvas != null)
        {
            highlightCanvas.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        UpdateHighlight();
        
        // Press the 'B' button to capture.
        if (OVRInput.GetDown(OVRInput.Button.Two) && _gazedAtObject)
        {
            StartCoroutine(CaptureAndSave());
        }
    }
    
    void UpdateHighlight()
    {
        if (!highlightCanvas || !_gazedAtObject) return;
        
        var renderer = _gazedAtObject.GetComponent<Renderer>();
        if (!renderer) return;

        // Show the highlight
        highlightCanvas.gameObject.SetActive(true);

        // Position the canvas at the center of the object
        highlightCanvas.position = renderer.bounds.center;

        // Scale the canvas to match the object's size
        float size = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z);
        highlightCanvas.sizeDelta = new Vector2(size, size);
        
        // Make the canvas face the camera
        highlightCanvas.LookAt(Camera.main.transform);
        
        // Move it slightly forward so it doesn't clip into the object
        highlightCanvas.position += highlightCanvas.forward * offset;
    }

    private Rect Calculate2DBoundingBox(Transform target)
    {
        var renderer = target.GetComponent<Renderer>();
        if (!renderer) return new Rect();

        Bounds bounds = renderer.bounds;
        Vector3[] corners = new Vector3[8];
        
        // 8 corners of the 3D bounding box in world space
        corners[0] = new Vector3(bounds.center.x + bounds.extents.x, bounds.center.y + bounds.extents.y, bounds.center.z + bounds.extents.z);
        corners[1] = new Vector3(bounds.center.x + bounds.extents.x, bounds.center.y + bounds.extents.y, bounds.center.z - bounds.extents.z);
        corners[2] = new Vector3(bounds.center.x + bounds.extents.x, bounds.center.y - bounds.extents.y, bounds.center.z + bounds.extents.z);
        corners[3] = new Vector3(bounds.center.x + bounds.extents.x, bounds.center.y - bounds.extents.y, bounds.center.z - bounds.extents.z);
        corners[4] = new Vector3(bounds.center.x - bounds.extents.x, bounds.center.y + bounds.extents.y, bounds.center.z + bounds.extents.z);
        corners[5] = new Vector3(bounds.center.x - bounds.extents.x, bounds.center.y + bounds.extents.y, bounds.center.z - bounds.extents.z);
        corners[6] = new Vector3(bounds.center.x - bounds.extents.x, bounds.center.y - bounds.extents.y, bounds.center.z + bounds.extents.z);
        corners[7] = new Vector3(bounds.center.x - bounds.extents.x, bounds.center.y - bounds.extents.y, bounds.center.z - bounds.extents.z);
        
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

        // Project each corner onto the screen
        for (int i = 0; i < 8; i++)
        {
            Vector3 screenPoint = Camera.main.WorldToScreenPoint(corners[i]);
            minX = Mathf.Min(minX, screenPoint.x);
            maxX = Mathf.Max(maxX, screenPoint.x);
            minY = Mathf.Min(minY, screenPoint.y);
            maxY = Mathf.Max(maxY, screenPoint.y);
        }

        return new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
    }

    private IEnumerator CaptureAndSave()
    {
        // Wait for the end of the frame so everything has been rendered
        yield return new WaitForEndOfFrame();

        // the final bounding box for the capture
        Rect regionToCapture = Calculate2DBoundingBox(_gazedAtObject);
        
        Texture2D texture = new Texture2D((int)regionToCapture.width, (int)regionToCapture.height, TextureFormat.RGB24, false);
        texture.ReadPixels(regionToCapture, 0, 0);
        texture.Apply();

        // texture into a JPEG byte array
        byte[] jpegBytes = texture.EncodeToJPG();
        Object.Destroy(texture); // Clean up

        // save the file
        string fileName = $"capture_{_gazedAtObject.name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.jpeg";
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllBytes(path, jpegBytes);

        Debug.Log($"<color=lime>Saved capture to: {path}</color>");

        // send 'jpegBytes' to the backend.
    }
}
