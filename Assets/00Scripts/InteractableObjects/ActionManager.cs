using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 1. Structure to map a name (string) to a Prefab (GameObject)
[System.Serializable]
public class ItemMapping
{
    public string itemName;
    public GameObject prefab;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
}

// 2. Class to parse the incoming JSON result
[System.Serializable]
public class PythonResult
{
    public string predicted_tool;
    public bool is_left_hand_wrist_based;
}

[System.Serializable]
public class AvailableItemDTO
{
    public string id;
    public string description;
}

public class ActionManager : MonoBehaviour
{
    [Header("Hand Anchors")]
    [Tooltip("The Transform where items should be attached on the Left Hand (the Wrist bone).")]
    public Transform leftHandAnchor;
    
    [Tooltip("The Transform where items should be attached on the Right Hand.")]
    public Transform rightHandAnchor;

    [Header("Item Database")]
    [Tooltip("Tools registration")]
    public List<ItemMapping> availableItems;
    public List<ItemMapping> availableItems_kitchen;
    public List<ItemMapping> availableItems_livingroom;
    public List<ItemMapping> availableItems_bathroom;

    private int currentPlaceIndex = 0;
    private bool isLeftHandBase = true;

    public void SetPlaceIndex(int placeIndex)
    {
        currentPlaceIndex = placeIndex;
    }

    public List<AvailableItemDTO> GetAvailableItemsPayload()
    {
        List<ItemMapping> currentAvailableItems;

        if (currentPlaceIndex == 0)
        {
            currentAvailableItems = availableItems_kitchen;
        }
        else if (currentPlaceIndex == 1)
        {
            currentAvailableItems = availableItems_livingroom;
        }
        else
        {
            currentAvailableItems = availableItems_bathroom;
        }

        return currentAvailableItems.Select(item => new AvailableItemDTO
        {
            id = item.itemName,
            description = item.itemName
        }).ToList();
    }

    // Internal tracker to destroy old items before spawning new ones
    private GameObject _currentHeldItem;
    private Transform _currentHandTransform;
    private string _currentHeldItemID;

    public void SetReferenceHand(bool isLeftHandBased)
    {
        isLeftHandBase = isLeftHandBased;
    }

    /// <summary>
    /// Call this function when you receive a message from the WebSocket
    /// </summary>
    /// <param name="jsonResponse">The raw JSON string from Python</param>
    public void ExecuteAction(string jsonResponse)
    {
        PythonResult result = JsonUtility.FromJson<PythonResult>(jsonResponse);
        
        // if (result.action != "Equip") return;

        Transform targetHand = isLeftHandBase ? leftHandAnchor : rightHandAnchor;

        SpawnAndAttach(result.predicted_tool, targetHand);

        // _currentHeldItemID = result.ID;
        // Invoke(nameof(InvokeSpawn), 5f);
    }

    private void InvokeSpawn()
    {
        SpawnAndAttach(_currentHeldItemID, rightHandAnchor);
    }

    private void SpawnAndAttach(string toolName, Transform handTransform)
    {
        if (_currentHeldItem != null && _currentHeldItemID == toolName)
        {
            return;
        }
        
        ItemMapping itemMap = availableItems.Find(x => x.itemName == toolName);

        if (itemMap == null || !itemMap.prefab)
        {
            Debug.LogError($"ActionExecutor: Could not find prefab for tool '{toolName}'");
            return;
        }

        // 4. Clean up previous item (optional - depends on game design)
        if (_currentHeldItem)
        {
            // Destroy(_currentHeldItem);
            return;
        }

        // 5. Instantiate the object
        // spawn it at the hand's position immediately
        GameObject newObj = Instantiate(itemMap.prefab, handTransform.position, handTransform.rotation);

        // 6. The "Grab" Logic: Parent it to the hand
        newObj.transform.SetParent(handTransform);

        // 7. Apply Offsets (Crucial for making it look like a natural grip)
        newObj.transform.localPosition = itemMap.positionOffset;
        newObj.transform.localEulerAngles = itemMap.rotationOffset;

        // 8. Disable Physics (Optional but Recommended)
        // If the tool has a Rigidbody, set it to Kinematic so it doesn't fail gravity check
        Rigidbody rb = newObj.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
        }

        _currentHeldItem = newObj;
        _currentHandTransform = handTransform;
        _currentHeldItemID = toolName;
        Debug.Log($"<color=cyan>Equipped {toolName} to {handTransform.name}</color>");

        Invoke(nameof(DetachCurrentItem), 1.0f);
    }

    private void DetachCurrentItem()
    {
        if (_currentHeldItem)
        {
            Rigidbody rb = _currentHeldItem.GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            _currentHeldItem.transform.parent = null;
            _currentHeldItem = null;
        }
    }
}