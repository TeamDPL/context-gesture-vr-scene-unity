using UnityEngine;

public class PlaceEnterTrigger : MonoBehaviour
{
    public int placeIndex = 0;
    public ActionManager actionManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && actionManager)
        {
            actionManager.SetPlaceIndex(placeIndex);
        }
    }
}
