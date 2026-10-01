using System;
using System.Collections.Generic;
using System.Threading;
using DummyLayerSystem;
using mainMenu;
using UnityEngine;

public class ReturnLayer : UILayer
{
    [SerializeField] BOButton returnButton;
    [SerializeField] GameObject indicator;
    [SerializeField] GameObject curtain;
    
    public static readonly List<ReturnAction> ReturnMissionList = new List<ReturnAction>();
    
    void Setup()
    {
        returnButton.SetListener(POP);
    }

    public static void Stack(MainSceneStep step, Func<MainSceneStep, bool> returnAct)
    {
        ReturnAction returnAction = new ReturnAction
        {
            returnToStep = step,
            returnAction = ()=> returnAct(step)
        };
        ReturnMissionList.Add(returnAction);
        var returnLayer = UILayerLoader.Load<ReturnLayer>();
        returnLayer.Setup();
    }
    
    static bool _returning;

    public static void POP()
    {
        if (_returning || ReturnMissionList.Count == 0)
            return;

        var targetMission = ReturnMissionList[^1];
        _returning = true;
        try
        {
            // Loading pages can refuse the transition. Keep their destination
            // until the callback succeeds, including when it throws.
            if (!targetMission.returnAction.Invoke())
                return;

            // A successful transition may clear/rebuild history or append an
            // action. Consume only this completed mission, never the new top.
            ReturnMissionList.Remove(targetMission);
            if (ReturnMissionList.Count == 0)
            {
                UILayerLoader.Remove<ReturnLayer>();
            }
            else
            {
                var returnLayer = UILayerLoader.Load<ReturnLayer>();
                returnLayer.Setup();
            }
        }
        finally { _returning = false; }
    }
    
    public static void PUSH(ReturnAction returnAction)
    {
        void RegisterReturn()
        {
            ReturnMissionList.Add(returnAction);
            var returnLayer = UILayerLoader.Load<ReturnLayer>();
            returnLayer.Setup();
        }
        if (ReturnMissionList.Count > 0)
        {
            var last = ReturnMissionList[^1];
            if (last.returnToStep != returnAction.returnToStep)
            {
                RegisterReturn();
            }
        }
        else
        {
            RegisterReturn();
        }
    }
    
    public void ForceBackMode(bool on)
    {
        curtain.gameObject.SetActive(on);
        indicator.gameObject.SetActive(on);
        ToTop();
    }
    
    public void HalfForceBackMode()
    {
        indicator.gameObject.SetActive(true);
        curtain.gameObject.SetActive(false);
        ToTop();
    }

    public static void AddUniTaskCancel(CancellationTokenSource cts)
    {
        var layer = UILayerLoader.Get<ReturnLayer>();
        if (layer != null)
        {
            void TriggerCancellation()
            {
                layer.returnButton.onClick.RemoveListener(TriggerCancellation);
                try
                {
                    if (cts != null && !cts.IsCancellationRequested)
                        cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The owning preview may already have completed and disposed it.
                }
            }

            layer.returnButton.onClick.AddListener(TriggerCancellation);
        }
    }

    public static void MoveBack()
    {
        var layer = UILayerLoader.Get<ReturnLayer>();
        if (layer != null)
        {
            layer.transform.SetAsFirstSibling();
        }
    }
    
    public static void MoveFront()
    {
        var layer = UILayerLoader.Get<ReturnLayer>();
        if (layer != null)
        {
            layer.transform.SetAsLastSibling();
        }
    }

    public static void Clear()
    {
        ReturnMissionList.Clear();
        UILayerLoader.Remove<ReturnLayer>();
    }
}
