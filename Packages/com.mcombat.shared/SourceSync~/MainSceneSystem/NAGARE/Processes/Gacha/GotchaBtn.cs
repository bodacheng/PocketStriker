using System;
using UnityEngine;

public class GotchaBtn : MonoBehaviour
{
    [SerializeField] private string itemId;
    [SerializeField] private string currencyCode;
    [SerializeField] private int currencyCount;
    [SerializeField] private BOButton executeBtn;

    public void Setup(Action<string,string,int> nine)
    {
        executeBtn.SetListener(() =>
        {
            nine.Invoke(itemId, currencyCode, currencyCount);
        });
    }
}
