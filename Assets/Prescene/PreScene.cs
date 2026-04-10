using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using UnityEngine;
using DummyLayerSystem;
using ModelView;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace mainMenu
{
    public partial class PreScene : MonoBehaviour
    {
        public static PreScene target;

        public Camera postProcessCamera;
        public Camera noPostProcessCamera;
        
        [Header("SafeArea")]
        [SerializeField] RectTransform safeAreaRect;
        [FormerlySerializedAs("T")]
        [SerializeField] GameObject legacySafeAreaRoot;
        [Header("AudioSource")]
        [SerializeField] AudioSource audioSource;
        [Header("UIAudioSource")]
        [SerializeField] AudioSource uiAudioSource;
        [Header("Canvas")] 
        [SerializeField] Canvas Canvas;

        public RectTransform stonesTempContainer;

        public UnitInfo Focusing => _focusing;
        UnitInfo _focusing;
        
        public void SetFocusingUnit(string instanceID)
        {
            _focusing = dataAccess.Units.Get(instanceID);
            if (_focusing == null)
            {
                BackGroundPS.target.ChangeBGByElement(Element.Null);
                return;
            }
            
            var unitConfig = Units.GetUnitConfig(_focusing.r_id);
            if (unitConfig == null)
            {
                return;
            }
            PlayerPrefs.SetString("showUnit", instanceID);
            BackGroundPS.target.ChangeBGByElement(unitConfig.element);
        }

        [SerializeField] private RawImage effectBg;
        RenderTexture _effectRenderTexture;

        RectTransform GetSafeAreaRect()
        {
            if (safeAreaRect != null)
            {
                return safeAreaRect;
            }

            if (legacySafeAreaRoot != null)
            {
                return legacySafeAreaRoot.GetComponent<RectTransform>();
            }

            return Canvas != null ? Canvas.GetComponent<RectTransform>() : null;
        }

        void Awake()
        {
            target = this;
            PosCal.Canvas = this.Canvas;
            PosCal.SafeAreaRect = GetSafeAreaRect();
            PosCal.TestIni();
            SetBgRenderTexture();
        }
        
        void SetBgRenderTexture()
        {
            _effectRenderTexture = new RenderTexture(Screen.width, Screen.height, 16);
            _effectRenderTexture.Create();
            noPostProcessCamera.targetTexture = _effectRenderTexture;
            effectBg.texture = _effectRenderTexture;
            effectBg.color = Color.white;
        }
        
        // URP的postprocess有个默认设置，就是overlay的相机如果启用postprocess，
        // 那么它会把被叠加的主相机和在它之前的stack全都加上postprocess，哪怕这些相机culling mask不一样
        // 甚至复数个stack都有postprocess的话还能把前面前面的stack或base的东西postprocess给增强
        public void CameraStackToPostProcess(Camera camera)
        {
            var pCameraData = postProcessCamera.GetComponent<UniversalAdditionalCameraData>();
            if (pCameraData.cameraStack.Contains(camera))
                return;
            
            pCameraData.renderPostProcessing = false;
            for (var index = 0; index < pCameraData.cameraStack.Count; index++)
            {
                var stackCamera = pCameraData.cameraStack[index];
                var sCameraData = stackCamera.GetComponent<UniversalAdditionalCameraData>();
                sCameraData.renderPostProcessing = false;
            }
            
            var cameraData = camera.transform.GetComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Overlay;
            cameraData.renderPostProcessing = true;
            pCameraData.cameraStack.Add(camera);
            OnDestroyCallback.AddOnDestroyCallback(camera.gameObject, () =>
            {
                pCameraData.cameraStack.Remove(camera);
                pCameraData.renderPostProcessing = pCameraData.cameraStack.Count == 0;
                for (var index = 0; index < pCameraData.cameraStack.Count; index++)
                {
                    var stackCamera = pCameraData.cameraStack[index];
                    if (stackCamera != null)
                    {
                        var sCameraData = stackCamera.GetComponent<UniversalAdditionalCameraData>();
                        sCameraData.renderPostProcessing = index == pCameraData.cameraStack.Count - 1;
                    }
                }
            });
        }
        
        public void CameraStackToNonePostProcess(Camera camera)
        {
            var pCameraData = noPostProcessCamera.GetComponent<UniversalAdditionalCameraData>();
            if (pCameraData.cameraStack.Contains(camera))
                return;
            var cameraData = camera.transform.GetComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Overlay;
            cameraData.renderPostProcessing = false;
            pCameraData.cameraStack.Add(camera);
        }

        async void Start()
        {
            TutorialRunner.Main.Shutdown();
            await UniTask.WhenAll(
                PlayFabReadClient.LoadReadMailsAsync(),
                AddressablesLogic.Essentials(),
                ArcadeModeManager.Instance.Initialize(),
                GangbangModeManager.Instance.Initialize()
            );
            CashClear();
            UILayerLoader.Clear();
            var targetSafeArea = GetSafeAreaRect();
            UILayerLoader.SetHanger(targetSafeArea != null ? targetSafeArea : Canvas.transform, Canvas.transform);
            
            AppSetting.UiAudioSource = uiAudioSource;
            AppSetting.BGMSource = audioSource;
            await AppSetting.PlayBGM(CommonSetting.LobbyThemeAddressKey);
            
            UILayerLoader.SetEffectBg(effectBg.rectTransform);
            Time.timeScale = 1;
            FightGlobalSetting.SceneStep = 0;
            
            BasicPhase();
            ToInitialPhase();
        }

        async UniTask PrepareModelOftenUse()
        {
            await UniTask.WhenAll(new List<UniTask>()
            {
                DedicatedCameraConnector.PrepareModel("1"),
                DedicatedCameraConnector.PrepareModel("2"),
                DedicatedCameraConnector.PrepareModel("3"),
                DedicatedCameraConnector.PrepareModel("4"),
                DedicatedCameraConnector.PrepareModel("5"),
                DedicatedCameraConnector.PrepareModel("6"),
                DedicatedCameraConnector.PrepareModel("7")
            });
        }

        public static void CashClear()
        {
            Stones.ClearRender();
            HurtObjectManager.Clear();
            EffectsManager.Clear();
            AnimationResourceLoader.Instance.Clear();
            DedicatedCameraConnector.ClearBackUpModels();
            AddressablesLogic.ReleaseAsyncOperationHandles();
        }
        
        void BasicPhase()
        {
            Application.targetFrameRate = 60;
            
            #region 主界面各大画面
            var settingPage = new SettingPage();
            var frontPage = new FrontPage();
            var teamEditFront = new TeamEditPage();
            var skillStones = new StonesPage();
            var stoneSell = new StoneSell();
            var stoneUpdatesConfirmPage = new StoneUpdatesConfirmPage();
            var selfFightFront = new SelfFightPage();
            var questInfo = new QuestInfoPage();
            var unitListPage = new UnitListPage();
            var memberDetailEdit = new SkillEditPage();
            var arcadeFrontPage = new ArcadeFrontPage();
            var gangbangFrontPage = new GangbangFrontPage();
            var eventFrontPage = new EventFightPage();
            
            // Shop
            var shopTop = new ShopTop();
            
            // Gotcha
            var gotchaFront = new GotchaFront();
            var gotchaResult = new GotchaResult();
            var dropTableInfo = new DropTableInfoDetail();
            var arenaPage = new ArenaPage();
            var rankingPage = new RankingPage();
            var arenaAwardPage = new ArenaAwardPage();
            
            // mail
            var mailBox = new MailBoxProcess();
            var mailDetail = new MailDetailProcess();
            
            ProcessesRunner.Main.Clear();
            ProcessesRunner.Main.Add(MainSceneStep.Setting, settingPage);
            ProcessesRunner.Main.Add(MainSceneStep.TeamEditFront, teamEditFront);
            ProcessesRunner.Main.Add(MainSceneStep.SkillStoneList, skillStones);
            ProcessesRunner.Main.Add(MainSceneStep.SkillStones_Sell, stoneSell);
            ProcessesRunner.Main.Add(MainSceneStep.SelfFightFront, selfFightFront);
            ProcessesRunner.Main.Add(MainSceneStep.StoneUpdateConfirm, stoneUpdatesConfirmPage);
            ProcessesRunner.Main.Add(MainSceneStep.QuestInfo, questInfo);
            ProcessesRunner.Main.Add(MainSceneStep.UnitList, unitListPage);
            ProcessesRunner.Main.Add(MainSceneStep.UnitSkillEdit, memberDetailEdit);
            ProcessesRunner.Main.Add(MainSceneStep.FrontPage, frontPage);
            ProcessesRunner.Main.Add(MainSceneStep.ArcadeFront, arcadeFrontPage);
            ProcessesRunner.Main.Add(MainSceneStep.GangBangFront, gangbangFrontPage);
            ProcessesRunner.Main.Add(MainSceneStep.EventFight, eventFrontPage);
            ProcessesRunner.Main.Add(MainSceneStep.Arena, arenaPage);
            ProcessesRunner.Main.Add(MainSceneStep.Ranking, rankingPage);
            ProcessesRunner.Main.Add(MainSceneStep.ArenaAward, arenaAwardPage);
            ProcessesRunner.Main.Add(MainSceneStep.ShopTop, shopTop);
            ProcessesRunner.Main.Add(MainSceneStep.MailBox, mailBox);
            ProcessesRunner.Main.Add(MainSceneStep.MailDetail, mailDetail);
            ProcessesRunner.Main.Add(MainSceneStep.GotchaFront, gotchaFront);
            ProcessesRunner.Main.Add(MainSceneStep.GotchaResult, gotchaResult);
            ProcessesRunner.Main.Add(MainSceneStep.DropTableInfo, dropTableInfo);
            #endregion
        }
        
        void ToInitialPhase()
        {
            if (ReturnLayer.ReturnMissionList.Count > 0)
            {
                //ReturnLayer.AddFeatureToReturnButton();
                //从战斗画面返回后，进入战斗前的菜单往上跳一节，指的是站前准备画面
                ReturnLayer.POP();
            }
            else
            {
                if (PlayerAccountInfo.Me.tutorialProgress != "Finished")
                {
                    DataLoading(
                        () =>
                        {
                            TutorialRunner.Main.TutorialCheck();
                        }
                    );
                }
                else
                {
                    if (MainMenuNote.GoingTo != MainSceneStep.FrontPage)
                    {
                        ReturnLayer.Stack(MainSceneStep.FrontPage, (x)=> trySwitchToStep(x, false));
                    }
                    trySwitchToStep(MainMenuNote.GoingTo, false);
                }
            }
        }
        
        void Update()
        {
            ProcessesRunner.Main.ProcessNagare();
        }

        void LateUpdate()
        {
            TutorialRunner.Main.Process();
        }

        public void BeginSkillTest_Rotation()
        {
            var stage = FightInfo.RandomSkillTestStage(TeamMode.Rotation);
            stage.Team1ID = PlayerAccountInfo.Me.PlayFabId;
            FightLoad.Go(stage);
        }
        
        public void BeginSkillTest_Multi()
        {
            var stage = FightInfo.RandomSkillTestStage(TeamMode.MultiRaid);
            stage.Team1ID = PlayerAccountInfo.Me.PlayFabId;
            FightLoad.Go(stage);
        }
        
        [EnumAction(typeof(MainSceneStep))]
        public bool trySwitchToStep(MainSceneStep nextStep, bool forward = true)
        {
            if (forward && ProcessesRunner.Main.currentProcess != null)
            {
                var returnToStep = ProcessesRunner.Main.currentProcess.Step;
                bool ReturnToCurrent()
                {
                    return trySwitchToStep(returnToStep, false);
                }
                
                ReturnAction returnAction = new ReturnAction
                {
                    returnToStep = returnToStep,
                    returnAction = ReturnToCurrent
                };
                
                var success = ProcessesRunner.Main.ChangeProcess(nextStep);
                if (success)
                    ReturnLayer.PUSH(returnAction);
                return success;
            }
            else
            {
                return ProcessesRunner.Main.ChangeProcess(nextStep);
            }
        }
        
        public bool ReEnterCurrent()
        {
            return ProcessesRunner.Main.ChangeProcess(ProcessesRunner.Main.currentProcess.Step);
        }
        
        public bool trySwitchToStep<T>(MainSceneStep nextStep, T t, bool forward)
        {
            if (forward && ProcessesRunner.Main.currentProcess != null)
            {
                var returnToStep = ProcessesRunner.Main.currentProcess.Step;
                bool ReturnToCurrent()
                {
                    Debug.Log("返回："+ returnToStep);
                    return trySwitchToStep(returnToStep, false);
                }
                
                ReturnAction returnAction = new ReturnAction
                {
                    returnToStep = returnToStep,
                    returnAction = ReturnToCurrent
                };
                var success = ProcessesRunner.Main.ChangeProcess(nextStep, t);
                if (success)
                    ReturnLayer.PUSH(returnAction);
                return success;
            }
            else
            {
                return ProcessesRunner.Main.ChangeProcess(nextStep, t);
            }
        }
    }
    
    public class ReturnAction
    {
        public MainSceneStep returnToStep;
        public Func<bool> returnAction;
    }
    
    
}
