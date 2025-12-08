using TMPro;
using UnityEngine;

public class CanvasNetworkEventSubscriber : MonoBehaviour
{
    [Header("UI Comps")] 
    [SerializeField] 
    private TextMeshProUGUI startTxt;
    [SerializeField]
    private TextMeshProUGUI processTxt;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        GestureContextNetwork.OnTriggerDataTransmission += HandleOnTriggerDataTransmission;
        GestureContextNetwork.OnStartDataTransmission += HandleOnStartDataTransmission;
        GestureContextNetwork.OnStopDataTransmission += HandleOnStopDataTransmission;
    }

    void Start()
    {
        startTxt.enabled = false;
        processTxt.enabled = false;
    }

    private void HandleOnTriggerDataTransmission()
    {
        startTxt.enabled = true;
    }

    private void HandleOnStartDataTransmission()
    {
        startTxt.enabled = false;
        processTxt.enabled = true;
    }

    private void HandleOnStopDataTransmission()
    {
        processTxt.enabled = false;
    }

    // Update is called once per frame
    void OnDisable()
    {
        GestureContextNetwork.OnTriggerDataTransmission -= HandleOnTriggerDataTransmission;
        GestureContextNetwork.OnStartDataTransmission -= HandleOnStartDataTransmission;
        GestureContextNetwork.OnStopDataTransmission -= HandleOnStopDataTransmission;
    }
}