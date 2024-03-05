using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ModelView;

namespace mainMenu
{
    public class UnitOptionLayer : UILayer
    {
        [SerializeField] DedicatedCameraConnector _connector;
        [SerializeField] Image view2D;
        [SerializeField] Animator unitOutAnimator;
        [Header("角色明细T，技能显示T")]
        [SerializeField] RectTransform MemberInfoT;
        
        [Header("mini nine slot")]
        public NineForShow _NineForShow;
        [SerializeField] GameObject clickSkillEditIndicator;
        
        [Header("部下详细")]
        [SerializeField] BOButton skillEditButton;
        
        public override void OnDestroy()
        {
            _connector.Clear();
        }

        public void RefreshMemberDetailPageByFocusingUnit()
        {
            var camRect = _connector.GetComponent<RectTransform>();
            ResizeCameraConnectorAsMaxSquare(camRect, camRect.rect.width, camRect.rect.height);
            
            if (PreScene.target.Focusing == null || PreScene.target.Focusing.id == null || PreScene.target.Focusing.r_id == null)
            {
                skillEditButton.onClick.RemoveAllListeners();
                MemberInfoT.gameObject.SetActive(false);
                return;
            }
            
            // mini nineslot show
            _NineForShow.ShowStones_Acc(PreScene.target.Focusing.id);
            MemberInfoT.gameObject.SetActive(true);
            
            // edit按钮功能加载
            skillEditButton.onClick.RemoveAllListeners();
            void SkillEdit()
            {
                if (PreScene.target.Focusing.id != null)
                    PreScene.target.trySwitchToStep(MainSceneStep.UnitSkillEdit, true);
            }
            skillEditButton.onClick.AddListener(SkillEdit);
            
            // 下面这些都是针对技能显示这个高级功能的，按理说下面这些即便出错，上面的功能也该健全。。即，这些是表现层。
            UnitModelRender(UnitInfo.GetUnitInfo(PreScene.target.Focusing));
        }
        
        void UnitModelRender(UnitInfo info)
        {
            if (info == null)
            {
                Debug.Log("角色详细信息读取错误.尝试将“对准”中的角色信息至空");
                _connector.ShowMyModel(null).Forget();
            }
            else
            {
                _connector.ShowMyModel(info.id).Forget();
                // Set2DView(info.r_id, view2D, unitOutAnimator, 10, 0.6f, 0, DedicatedCameraConnector.Unit2DViewYoKoSpaceWhenAtRight(info.r_id));
            }
        }
        
        #region 教程
        public void PlsClickSkillEdit()
        {
            clickSkillEditIndicator.SetActive(true);
        }
        #endregion
    }
}