using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RectAd : MonoBehaviour
{
    void OnEnable()
    {
        AdsManager.Instance.ShowMRec();
        
    }

    void OnDisable()
    {
        AdsManager.Instance.HideMRec();
    }

}
