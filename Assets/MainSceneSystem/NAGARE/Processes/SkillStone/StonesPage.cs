using Cysharp.Threading.Tasks;
using dataAccess;
using DG.Tweening;
using DummyLayerSystem;
using mainMenu;
using UnityEngine;

public class StonesPage : MSceneProcess
{
    public StonesPage()
    {
        Step = MainSceneStep.SkillStoneList;
    }
    
    private StoneListLayer layer;
    
    public override void ProcessEnter()
    {
        EnterProcess();
    }

    public override void ProcessEnter<T>(T t)
    {
        EnterProcess(true);
    }


    private Tweener autoScroll;
    //EnterProcess()内绝不能出现triggerMainProcess
    async void EnterProcess(bool updateStone = false)
    {
        BackGroundPS.target.Void();
        UILayerLoader.Remove<UpperInfoBar>();
        ProgressLayer.Loading(string.Empty);
        await Stones.RenderAll();
        ProgressLayer.Close();
        layer = UILayerLoader.Load<StoneListLayer>();
        layer.Setup();
        ReturnLayer.MoveFront();
        layer.levelManager.LevelUpAllStonesBtn.interactable = StoneLevelUpProccessor.HasStoneToBeUpdate();
        layer.levelManager.LevelUpAllStonesBtnAnimator.SetBool("on", StoneLevelUpProccessor.HasStoneToBeUpdate());
        var lowerMainBar = UILayerLoader.Get<LowerMainBar>();
        lowerMainBar.transform.SetAsLastSibling();

        if (!updateStone)
        {
            SetLoaded(true);
            return;
        }
        
        bool canNext = true;
        void Next(string x)
        {
            canNext = true;
            var info = Stones.Get(x);
            var config = SkillConfigTable.GetSkillConfigByRecordId(info.SkillId);
            layer.box.PressTab(config.SP_LEVEL);
            layer.box.RestFilter();

            #region 滚动scroll
            // 下面这些就是自动滚动，完全是gpt代码没仔细想过
            var targetStoneModel = Stones.GetRenderModel(x);
            // 计算目标元素在内容中的局部位置
            Vector3 elementLocalPosition = layer.box.ScrollRect.content.InverseTransformPoint(
                targetStoneModel.GetComponent<RectTransform>().position + new Vector3(0,targetStoneModel.GetComponent<RectTransform>().rect.height / 2 ));
            Vector3 viewportLocalPosition = layer.box.ScrollRect.content.InverseTransformPoint(layer.box.ScrollRect.viewport.position);
            
            // 计算目标位置和视口的差异
            float diff = viewportLocalPosition.y - elementLocalPosition.y;

            // 计算新的内容区域的位置
            Vector2 newAnchoredPosition = layer.box.ScrollRect.content.anchoredPosition + new Vector2(0, diff);
            
            autoScroll?.Kill();
            autoScroll = layer.box.ScrollRect.content.DOAnchorPosY(newAnchoredPosition.y, 0.5f).SetEase(Ease.InOutQuad).OnComplete(
                () =>
                {
                    targetStoneModel.Shine(PreScene.target.postProcessCamera);
                });
            #endregion
            
            layer.levelManager.CloseLevelUpPage();
            layer.TargetStoneID = x;
        }
        
        foreach (var updateAllStoneForm in StoneLevelUpProccessor.UpdateAllStoneForms)
        {
            await UniTask.WaitUntil(()=> canNext);
            canNext = false;
            StoneLevelUpProccessor.LevelUpStone(updateAllStoneForm.targetStoneID, updateAllStoneForm.stoneInstances, Next);
        }
        
        await UniTask.WaitUntil(()=> canNext);
        
        StoneLevelUpProccessor.CalUpdateAllForms();
        layer.levelManager.LevelUpAllStonesBtn.interactable = StoneLevelUpProccessor.HasStoneToBeUpdate();
        layer.levelManager.LevelUpAllStonesBtnAnimator.SetBool("on", false);
        LowerMainBar.RefreshBadge();
        PopupLayer.ArrangeWarnWindow(Translate.Get("AutoMergeFinished"));
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<StoneListLayer>();
    }
}