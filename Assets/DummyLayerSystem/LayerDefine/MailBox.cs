using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Singleton;

public class MailBox : UILayer
{
    [SerializeField] MailListView mailListViewPrefab;
    [SerializeField] VerticalLayoutGroup mailBoxT;
    [SerializeField] BOButton readAll;
    [SerializeField] BOButton deleteAllRead;
    
    readonly List<MailListView> _currentMailListViews = new List<MailListView>();
    
    public void Setup()
    {
        RefreshMailList();
        deleteAllRead.onClick.AddListener(() =>
        {
            PlayFabReadClient.DeleteAllLocalMails();
            RefreshMailList();
        });
        
        readAll.onClick.AddListener(() =>
        {
            PlayFabReadClient.ClaimAllPresentMails(PlayFabReadClient.SaveReadMailAsJson);
            //PlayFabReadClient.DeleteAllLocalMails();
            RefreshMailList();
        });
    }
    
    void RefreshMailList()
    {
        foreach (Transform t in mailBoxT.transform)
        {
            Destroy(t.gameObject);
        }
        _currentMailListViews.Clear();
        
        var myMailList = PlayFabReadClient.GetMailsData();
        for (var i = 0; i < myMailList.Count; i++)
        {
            var mailListView = Instantiate(mailListViewPrefab);
            mailListView.Setup(LoadPic);
            mailListView.PassMailInfo(myMailList[i], Sort);
            _currentMailListViews.Add(mailListView);
        }
        Sort();
    }

    void Sort()
    {
        float rectHeight = 0; 
        foreach (var t in _currentMailListViews)
        {
            t.transform.SetParent(null);
            rectHeight += (t.GetComponent<RectTransform>().rect.height + mailBoxT.spacing);
        }
        foreach (var t in _currentMailListViews)
        {
            t.transform.SetParent(mailBoxT.transform);
            t.transform.localPosition = Vector3.zero;
            t.transform.localScale = Vector3.one;
            t.gameObject.SetActive(true);
        }
        foreach (var t in _currentMailListViews)
        {
            t.transform.SetParent(mailBoxT.transform);
            t.transform.localPosition = Vector3.zero;
            t.transform.localScale = Vector3.one;
            t.gameObject.SetActive(true);
        }
        mailBoxT.GetComponent<RectTransform>().sizeDelta = new Vector2(mailBoxT.GetComponent<RectTransform>().sizeDelta.x, rectHeight);
    }
    
    public static async void LoadPic(Image mailIcon, string itemId)
    {
        if (itemId.Contains("DM"))
        {
            mailIcon.sprite = DefaultIconSetting._diamondIcon;
        }
        else if (itemId.Contains("GD"))
        {
            mailIcon.sprite = DefaultIconSetting._coinIcon;
        }
        else if (itemId.Contains("UnitGift"))
        {
            string[] words = itemId.Split(new [] {"UnitGift"}, StringSplitOptions.None);
            if (words.Length > 1)
            {
                var pic = await UnitIconDic.Load(words[1], mailIcon.gameObject);
                if (mailIcon != null)
                    mailIcon.sprite = pic;
            }
        }
        else
        {
            mailIcon.gameObject.SetActive(false);
        }
    }
}
