using System.Collections.Generic;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public static List<InteractableObject> AllGrabbableObjects = new List<InteractableObject>();

    void OnEnable()
    {
        AllGrabbableObjects.Add(this);
    }

    private void OnDisable()
    {
        AllGrabbableObjects.Remove(this);
    }
}
