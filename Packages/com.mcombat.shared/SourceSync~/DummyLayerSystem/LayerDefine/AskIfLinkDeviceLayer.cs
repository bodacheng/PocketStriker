using System;
using UnityEngine;
using UnityEngine.UI;

public class AskIfLinkDeviceLayer : UILayer
{
    [SerializeField] Button Yes;
    [SerializeField] Button No;

    public void Initialise(Action yes, Action no)
    {
        Yes.onClick.AddListener(yes.Invoke);
        No.onClick.AddListener(no.Invoke);
    }
}
