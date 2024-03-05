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
    
    public static void POP()
    {
        if (ReturnMissionList.Count == 0)
            return;

        var targetMission = ReturnMissionList[^1];
        ReturnMissionList.RemoveAt(ReturnMissionList.Count - 1);
        var success = targetMission.returnAction.Invoke();
        
        if (success)
        {
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
            void triggerCts()
            {
                if (cts != null && !cts.IsCancellationRequested)
                    cts.Cancel();
            }
            
            layer.returnButton.onClick.AddListener(() =>
            {
                triggerCts();
                layer.returnButton.onClick.RemoveListener(triggerCts);
            });
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
}
