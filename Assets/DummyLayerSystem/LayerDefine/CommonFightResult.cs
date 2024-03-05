using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using FightScene;

public class CommonFightResult : UILayer
{
    [SerializeField] Button returnBtn;
    [SerializeField] Button againBtn;
    [SerializeField] Button againFor1v1Btn;
    [SerializeField] Button againForMultiBtn;
    [SerializeField] RectTransform iconAndSKillShowUISetT;
    [SerializeField] IconAndSKillShowUISet iconAndSKillShowUISetPrefab;
    [SerializeField] NineForShow nineForShowPrefab;
    
    // 战斗结束后统计技能石升级情况时的画面显示
    private readonly List<NineForShow> _nineForShows = new List<NineForShow>();
    
    public void Setup(Action r, Action a, Action ar, Action am)
    {
        returnBtn.onClick.AddListener(r.Invoke);
        againBtn.onClick.AddListener(a.Invoke);
        againFor1v1Btn.onClick.AddListener(ar.Invoke);
        againForMultiBtn.onClick.AddListener(am.Invoke);
    }
    
    public void ShowSKillSets(TeamUIManager teamUIManager)
    {
        _nineForShows.Clear();
        foreach (Transform child in iconAndSKillShowUISetT) 
        {
            Destroy(child.gameObject);
        }
        
        foreach (var kv in teamUIManager.UnitIconDic)
        {
            var iassi = Instantiate(iconAndSKillShowUISetPrefab, iconAndSKillShowUISetT, true);
            var sideCharIcon = teamUIManager.GetSideIcon(kv.Key);
            var nineForShow = Instantiate(nineForShowPrefab);
            _nineForShows.Add(nineForShow);
            iassi.Set(sideCharIcon, nineForShow);
            iassi.transform.localPosition = Vector3.zero;
            iassi.transform.localScale = Vector3.one;
            nineForShow.ShowStones_Acc(RTFightManager.Target.UnitInfoRef[kv.Key].id);
        }
    }
    
    void Clear()
    {
        foreach(NineForShow nineForShow in _nineForShows)
        {
            nineForShow.ClearCurrent();
        }
    }
    
    public override void OnDestroy()
    {
        Clear();
    }
}
