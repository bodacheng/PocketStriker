using Cysharp.Threading.Tasks;
using UnityEngine;
using ModelView;

public class EventBattleTop : UILayer
{
    [SerializeField] private DedicatedCameraConnector connector;
    [SerializeField] private NineForShow nineForShow;
    [SerializeField] private EventBattleButton easyModeBtn;
    [SerializeField] private EventBattleButton normalModeBtn;
    [SerializeField] private EventBattleButton hardModeBtn;

    public EventBattleButton EasyModeBtn => easyModeBtn;
    public EventBattleButton NormalModeBtn => normalModeBtn;
    public EventBattleButton HardModeBtn => hardModeBtn;
    
    public void SetupCommon()
    {
        var camRect = connector.GetComponent<RectTransform>();
        ResizeCameraConnectorAsMaxSquare(camRect, camRect.rect.width, camRect.rect.height);
    }

    public async UniTask IconButtonFeature(UnitInfo unitInfo)
    {
        UnitConfig unitConfig = Units.GetUnitConfig(unitInfo.r_id);
        
        ProgressLayer.Loading(string.Empty);
        BackGroundPS.target.ChangeBGByElement(unitConfig.element);
        
        await UniTask.WhenAll(
            connector.ShowModel(unitConfig.RECORD_ID), 
            nineForShow.SkillSetInfoOfUnitOnArcadePage(unitInfo.set)
        );
        
        nineForShow.AddOnClickToSlots(
            (RECORD_ID) =>
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(RECORD_ID);
                connector.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
            }
        );
        ProgressLayer.Close();
    }
}
