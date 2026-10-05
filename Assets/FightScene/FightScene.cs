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
        
        private AdmobAdsButton postBattleInterstitial;
        private readonly PostBattleAdSession postBattleAds = new PostBattleAdSession();
        private FightInfo postBattleAdFight;

        public void JustShowAds()
        {
            postBattleAds.CompleteBattle();
            TryShowPostBattleAd();
        }

        public void BeginBattleAds()
        {
            postBattleAdFight = FightLoad.Fight;
            if (postBattleAdFight == null)
            {
                postBattleAds.Cancel();
                return;
            }
            postBattleAds.BeginBattle(postBattleAdFight.EventType, postBattleAdFight.ID, postBattleAdFight.RunTutorial);
            LoadAds();
        }

        private void TryShowPostBattleAd()
        {
            if (!postBattleAds.Pending) return;
            postBattleAds.TryPresent(
                ReferenceEquals(postBattleAdFight, FightLoad.Fight)
                    && FSceneProcessesRunner.Main.currentProcess is FightOverProcess,
                PlayerAccountInfo.Me != null && PlayerAccountInfo.Me.noAdsState,
                postBattleInterstitial != null && postBattleInterstitial.AdIsReady,
                AdmobAdsButton.IsFullScreenAdShowing,
                () => postBattleInterstitial.TryShowAd());
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

        public void BeginStoryBattleAttempt()
        {
            // Preparing is the actual retry/next-battle boundary. Repeated
            // preloads and ordinary result reads retain this attempt's job.
            CancelAIStory();
            aiStoryFight = FightLoad.Fight;
            aiStoryInfo = null;
            aiStoryLoadSource = null;
            PocketStrikerStoryVariety.BeginBattleAttempt(aiStoryFight);
            PreloadAIStory();
        }

        public void PreloadAIStory(bool newBattleAttempt = false)
        {
            if (!ShouldLoadAIStory()) return;
            // Retain the optional argument for existing callers. Only the
            // Preparing boundary starts another story, including after failure.
            EnsureAIStory().Forget();
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
                PocketStrikerStoryVariety.ForFight(aiStoryFight);
                aiStoryLoadSource = new UniTaskCompletionSource<StoryInfo>();
                aiStoryCancellation = new CancellationTokenSource();
                var loadSource = aiStoryLoadSource;
                LoadAIStory(loadSource, aiStoryFight, aiStoryCancellation.Token).Forget();
                return loadSource.Task;
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
                if (PocketStrikerQueuedStory.Enabled) return PocketStrikerQueuedStory.Load(cancellationToken);
                return PocketStrikerQueuedStory.LoadLegacy(cancellationToken);
            }
            var story = await BattleStoryRequest.Load(Request, cancellationToken, PocketStrikerQueuedStory.Enabled ? 110 : 60);
            // A late reply from the previous battle cannot replace the current story.
            if (this != null && !cancellationToken.IsCancellationRequested && ReferenceEquals(fight, FightLoad.Fight)
                && ReferenceEquals(loadSource, aiStoryLoadSource)) aiStoryInfo = story;
            else PocketStrikerQueuedStory.Release(story);
            loadSource.TrySetResult(story);
        }

        void CancelAIStory()
        {
            PocketStrikerQueuedStory.Release(aiStoryInfo);
            aiStoryLoadSource?.TrySetResult(null);
            if (aiStoryCancellation == null) return;
            aiStoryCancellation.Cancel();
            aiStoryCancellation.Dispose();
            aiStoryCancellation = null;
        }

        void OnDestroy()
        {
            BattleEffectLifetime.InvalidateAll();
            postBattleAds.Cancel();
            CancelAIStory();
            if (target == this) target = null;
        }

        public void LoadAds()
        {
            if (!AdsInitializer.ShouldEnableAds() || FightLoad.Fight == null ||
                (PlayerAccountInfo.Me != null && PlayerAccountInfo.Me.noAdsState))
                return;

            if (!PostBattleAdSession.IsEligible(FightLoad.Fight.EventType, FightLoad.Fight.ID, FightLoad.Fight.RunTutorial))
                return;
            if (postBattleInterstitial == null)
            {
                // Automatic placement has no button, reward, or purchase UI.
                var adHost = new GameObject("Post Battle Interstitial");
                adHost.transform.SetParent(transform, false);
                postBattleInterstitial = adHost.AddComponent<AdmobAdsButton>();
                postBattleInterstitial.UseInterstitialAd();
            }
            postBattleInterstitial.LoadAd();
        }
        
        void Update()
        {
            FSceneProcessesRunner.Main.ProcessUpdate();
            // An ad that finishes loading after settlement can still appear on
            // this result screen, but never after a retry or return to the menu.
            TryShowPostBattleAd();
            //TutorialRunner.Main.Process();
        }

        void FixedUpdate()
        {
            FSceneProcessesRunner.Main.ProcessFixedUpdate();
        }

        public void ReturnToFront(MainSceneStep mainSceneStep = MainSceneStep.FrontPage)
        {
            postBattleAds.Cancel();
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
