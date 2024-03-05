using System;
using UnityEngine;
using UnityEngine.UI;

public class DropTablePage : MonoBehaviour
{
    public RectTransform parentT;
    [SerializeField] private Text title;
    [SerializeField] private string itemId;
    [SerializeField] private GotchaBtn gotcha;
    [SerializeField] private BOButton openDropTableInfo;
    public string ItemId => itemId;

    public void Setup(Action<string,string,int> nine, Action<string> dropTableInfo, bool tutorial)
    {
        gotcha.Setup(nine);
        openDropTableInfo.gameObject.SetActive(!tutorial);
        openDropTableInfo.onClick.AddListener(()=> dropTableInfo.Invoke(itemId));
    }

    public void Show(bool on)
    {
        title.gameObject.SetActive(on);
        gotcha.gameObject.SetActive(on);
        openDropTableInfo.gameObject.SetActive(on);
    }
}
