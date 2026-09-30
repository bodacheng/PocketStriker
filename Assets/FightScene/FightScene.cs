using System;
using UnityEngine;
using UniRx;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;
using System.Threading;
using UnityEngine.SceneManagement;

namespace FightScene
{
    public class FightScene : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform safeAreaRect;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioSource uiAudioSource;

        [Header("FX")]
        public Camera fxCamera;
        
        [SerializeField] AdmobAdsButton watchAdBtnPrefab;
        [SerializeField] private AIServiceManager aiServiceManager;

        public AIServiceManager AIServiceManager => aiServiceManager;
        private StoryInfo aiStoryInfo;
        private UniTaskCompletionSource<StoryInfo> aiStoryLoadSource;
        private FightInfo aiStoryFight;
        private CancellationTokenSource aiStoryCancellation;
#if UNITY_EDITOR
        internal Func<UniTask<StoryInfo>> StoryLoaderForValidation;
#endif
        public StoryInfo AIStoryInfo => ShouldLoadAIStory() && ReferenceEquals(aiStoryFight, FightLoad.Fight) ? aiStoryInfo : null;

        public static FightScene target;
        
        public ReactiveProperty<bool> LoadStageFinished { get; set; } = new ReactiveProperty<bool>(false);
        
        public static List<GangbangInfo.SoldierGroupSet> team1GroupSet;
        
        private AdmobAdsButton watchBtn;
        private AdmobAdsButton postBattleInterstitial;
        public void ShowAds(int extraAdReward, RectTransform btnTarget, Action afterWatched, int finishedStage = -1)
        {
            if (extraAdReward > 0 && watchBtn != null)
            {
                watchBtn.transform.SetParent(btnTarget, false);
                watchBtn.transform.localPosition = Vector3.zero;
                
                string awardText = "x2"; // 简化处理 
                watchBtn.Text = awardText;
                watchBtn.SetWatchedAdExtraProcess(
                    () =>
                    {
                        watchBtn.ShowAdButton.gameObject.SetActive(false);
                        CloudScript.RequestAdReward(
                            "DM",
                            extraAdReward, 
                            afterWatched,
                            finishedStage
                        );
                    }
                );
                watchBtn.gameObject.SetActive(true);
            }
        }

        public void JustShowAds()
        {
            if (postBattleInterstitial != null)
            {
                if (postBattleInterstitial.AdIsReady)
                {
                    postBattleInterstitial.ShowAd();
                }
            }
        }

        void Awake()
        {
            target = this;
            EnsureAIServiceManager();

            var targetSafeArea = safeAreaRect != null ? safeAreaRect : canvas.GetComponent<RectTransform>();
            PosCal.Canvas = this.canvas;
            PosCal.SafeAreaRect = targetSafeArea;
            PosCal.TestIni();
            safeAreaRect = PosCal.SafeAreaRect;
        }

        private void EnsureAIServiceManager()
        {
            if (aiServiceManager == null)
            {
                aiServiceManager = GetComponent<AIServiceManager>();
                if (aiServiceManager == null)
                {
                    aiServiceManager = gameObject.AddComponent<AIServiceManager>();
                }
            }
        }
        
        void Start()
        {
            UILayerLoader.Clear();
            var targetSafeArea = safeAreaRect != null ? safeAreaRect : canvas.GetComponent<RectTransform>();
            UILayerLoader.SetHanger(targetSafeArea, canvas.transform);
            
            //HighLightLayer.DarkOff(Color.white, 0, true);
            Time.timeScale = 1;
            if (FightLoad.Fight == null)
            {
                return;
            }
            
            AppSetting.BGMSource = audioSource;
            AppSetting.UiAudioSource = uiAudioSource;
            Application.targetFrameRate = 60;
            FightGlobalSetting.SceneStep = 1;
            
            //Position_Set_Executor.Instance.P_sets.Clear();
            var preparingProcess = new PreparingProcess();
            var countDownProcess = new CountDownProcess();
            var fightingProcess = new FightingProcess();
            var fightResultAnim = new FightResultAnim();
            var fightOverProcess = new FightOverProcess();
            
            FSceneProcessesRunner.Main.Clear();
            switch(FightLoad.Fight.EventType)
            {
                case FightEventType.Arena:
                case FightEventType.Quest:
                case FightEventType.Gangbang:
                case FightEventType.Event:
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.Preparing, preparingProcess);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.CountDown, countDownProcess);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.Fighting, fightingProcess);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.FightResultAnim, fightResultAnim);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.FightOver, fightOverProcess);
                    break;
                case FightEventType.SkillTest:
                case FightEventType.Self:
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.Preparing, preparingProcess);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.CountDown, countDownProcess);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.Fighting, fightingProcess);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.FightOver, fightOverProcess);
                    break;
                case FightEventType.Screensaver:
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.Preparing, preparingProcess);
                    FSceneProcessesRunner.Main.AddNewProcess(SceneStep.Fighting, fightingProcess);
                    break;
            }
            FSceneProcessesRunner.Main.ArrangeProcessOrder();
            FSceneProcessesRunner.Main.ChangeProcess(SceneStep.Preparing);

            PreloadAIStory();
        }

        public void PreloadAIStory()
        {
            if (ShouldLoadAIStory())
            {
                EnsureAIStory().Forget();
            }
        }

        private bool ShouldLoadAIStory()
        {
            var fight = FightLoad.Fight;
            if (fight == null || fight.RunTutorial ||
                (fight.EventType == FightEventType.Quest && AdventureModeRules.IsTutorialStage(fight.ID)))
            {
                return false;
            }

            switch (fight.EventType)
            {
                case FightEventType.Event:
                case FightEventType.Quest:
                case FightEventType.Gangbang:
                    return true;
                default:
                    return false;
            }
        }

        public UniTask<StoryInfo> EnsureAIStory()
        {
            if (!ShouldLoadAIStory())
            {
                return UniTask.FromResult<StoryInfo>(null);
            }

            if (!ReferenceEquals(aiStoryFight, FightLoad.Fight))
            {
                CancelAIStory();
                aiStoryFight = FightLoad.Fight;
                aiStoryInfo = null;
                aiStoryLoadSource = null;
            }

            if (aiStoryInfo != null)
            {
                return UniTask.FromResult(aiStoryInfo);
            }

            if (aiStoryLoadSource == null)
            {
                EnsureAIServiceManager();
                aiStoryLoadSource = new UniTaskCompletionSource<StoryInfo>();
                aiStoryCancellation = new CancellationTokenSource();
                LoadAIStory(aiStoryLoadSource, aiStoryFight, aiStoryCancellation.Token).Forget();
            }

            return aiStoryLoadSource.Task;
        }

        private async UniTask LoadAIStory(UniTaskCompletionSource<StoryInfo> loadSource, FightInfo fight, CancellationToken cancellationToken)
        {
            UniTask<StoryInfo> Request()
            {
#if UNITY_EDITOR
                if (StoryLoaderForValidation != null) return StoryLoaderForValidation();
#endif
                return aiServiceManager != null ? aiServiceManager.LoadAIStory() : UniTask.FromResult<StoryInfo>(null);
            }
            var story = await BattleStoryRequest.Load(Request, cancellationToken);
            // A late reply from the previous battle cannot replace the current story.
            if (this != null && !cancellationToken.IsCancellationRequested && ReferenceEquals(fight, FightLoad.Fight)
                && ReferenceEquals(loadSource, aiStoryLoadSource)) aiStoryInfo = story;
            loadSource.TrySetResult(story);
        }

        void CancelAIStory()
        {
            aiStoryLoadSource?.TrySetResult(null);
            if (aiStoryCancellation == null) return;
            aiStoryCancellation.Cancel();
            aiStoryCancellation.Dispose();
            aiStoryCancellation = null;
        }

        void OnDestroy()
        {
            CancelAIStory();
            if (target == this) target = null;
        }

        public void LoadAds()
        {
            if (watchAdBtnPrefab == null || FightLoad.Fight == null ||
                (PlayerAccountInfo.Me != null && PlayerAccountInfo.Me.noAdsState))
                return;

            switch (FightLoad.Fight.EventType)
            {
                case FightEventType.Quest:
                case FightEventType.Gangbang:
                    if (watchBtn == null)
                    {
                        watchBtn = Instantiate(watchAdBtnPrefab, transform, false);
                        watchBtn.HasTicket = true;
                        watchBtn.gameObject.SetActive(false);
                    }
                    watchBtn.LoadAd();
                    break;
                case FightEventType.Event:
                    if (postBattleInterstitial == null)
                    {
                        postBattleInterstitial = Instantiate(watchAdBtnPrefab, transform, false);
                        postBattleInterstitial.UseInterstitialAd();
                        postBattleInterstitial.HasTicket = true;
                        postBattleInterstitial.gameObject.SetActive(false);
                    }
                    postBattleInterstitial.LoadAd();
                    break;
            }
        }
        
        void Update()
        {
            FSceneProcessesRunner.Main.ProcessUpdate();
            //TutorialRunner.Main.Process();
        }

        void FixedUpdate()
        {
            FSceneProcessesRunner.Main.ProcessFixedUpdate();
        }

        public void ReturnToFront(MainSceneStep mainSceneStep = MainSceneStep.FrontPage)
        {
            CancelAIStory();
            FSceneProcessesRunner.Main.ChangeProcess(SceneStep.None);
            var cameraManager = RTFightManager.Target?._CameraManager;
            if (cameraManager != null)
            {
                cameraManager.Assign_Camera(C_Mode.NULL, null, null);
            }
            RTFightManager.Target.ClearUnitData();
            RTFightManager.Target.team1.Clear();
            RTFightManager.Target.team2.Clear();
            FightLogger.value.WatchMissionsAbandon();
            FSceneProcessesRunner.Main.Clear();
            if (FightLoad.Fight.EventType == FightEventType.Quest)
                ProcessesRunner.Main.Clear();
            MainMenuNote.GoingTo = mainSceneStep;
            HitBoxesProcesser.Instance.Clear();
            SingleAssignmentDisposableCleaner.Clear();
            SceneManager.LoadScene(1);
        }
    }
}
