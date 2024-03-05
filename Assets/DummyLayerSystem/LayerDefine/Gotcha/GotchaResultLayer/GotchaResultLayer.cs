using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using DG.Tweening;
using DummyLayerSystem;
using UnityEngine;
using UnityEngine.UI;

public partial class GotchaResultLayer : UILayer
{
    public NineForShow NineForShow;
    
    #region 动画的跳过以及加速
    [SerializeField] Button Skip;
    [SerializeField] Button SpeedOnce;
    bool _starFallen;
    bool _oneStarFallen;
    Coroutine starFallAnimWholeProcess;
    Coroutine starFallAnimOneProcess;
    #endregion

    #region effect resource key
    [SerializeField] string stoneFigureSp0 = "gachastar0";
    [SerializeField] string stoneFigureSp1 = "gachastar1";
    [SerializeField] string stoneFigureSp2 = "gachastar2";
    [SerializeField] string stoneFigureSp3 = "gachastar3";
    [SerializeField] string flashSp0 = "screenStarExplostionTest0";
    [SerializeField] string flashSp1 = "screenStarExplostionTest1";
    [SerializeField] string flashSp2 = "screenStarExplostionTest2";
    [SerializeField] string flashSp3 = "screenStarExplostionTest3";
    [SerializeField] string explosionSp0 = "ButtonEffects/redmagic/explosion0.prefab";
    [SerializeField] string explosionSp1 = "ButtonEffects/redmagic/explosion1.prefab";
    [SerializeField] string explosionSp2 = "ButtonEffects/redmagic/explosion2.prefab";
    [SerializeField] string explosionSp3 = "ButtonEffects/redmagic/explosion3.prefab";
    #endregion
    
    private readonly Dictionary<StoneOfPlayerInfo, StoneFallEffectSet> _effectDic = new Dictionary<StoneOfPlayerInfo, StoneFallEffectSet>();

    (string, string, string) GetEffectName(int spLevel)
    {
        string stoneFigureName = string.Empty;
        string flashName = string.Empty;
        string explosionName = string.Empty;
        
        switch(spLevel)
        {
            case 0:
                stoneFigureName = stoneFigureSp0;
                flashName = flashSp0;
                explosionName = explosionSp0;
                break;
            case 1:
                stoneFigureName = stoneFigureSp1;
                flashName = flashSp1;
                explosionName = explosionSp1;
                break;
            case 2:
                stoneFigureName = stoneFigureSp2;
                flashName = flashSp2;
                explosionName = explosionSp2;
                break;
            case 3:
                stoneFigureName = stoneFigureSp3;
                flashName = flashSp3;
                explosionName = explosionSp3;
                break;
        }
        return (stoneFigureName, flashName, explosionName);
    }

    private string gotchaId;
    
    private class StoneFallEffectSet
    {
        public UIObject StoneFigure;
        public UIObject StoneFlashFigure;
        public UIObject ScreenExplosionFigure;
        Sequence _currentSequence;
        
        private Func<int, (string, string, string)> _getEffectName;
        
        public void Setup(Func<int, (string, string, string)> getEffectName)
        {
            this._getEffectName = getEffectName;
        }
        
        public async UniTask Load(int spLevel)
        {
            var effectName = _getEffectName(spLevel);
            var stoneFigureName = effectName.Item1;
            var flashName = effectName.Item2;
            var explosionName = effectName.Item3;
            
            StoneFigure = await AddressablesLogic.LoadTOnObject<UIObject>(stoneFigureName);
            StoneFlashFigure = await AddressablesLogic.LoadTOnObject<UIObject>(flashName);
            ScreenExplosionFigure = await AddressablesLogic.LoadTOnObject<UIObject>(explosionName);
            
            StoneFigure.ParticleSystem.Stop(true);
            StoneFlashFigure.ParticleSystem.Stop(true);
            ScreenExplosionFigure.ParticleSystem.Stop(true);
        }

        public void Clear()
        {
            if (StoneFigure != null)
                Destroy(StoneFigure.gameObject);
            if (StoneFlashFigure != null)
                Destroy(StoneFlashFigure.gameObject);
            if (ScreenExplosionFigure != null)
                Destroy(ScreenExplosionFigure.gameObject);

            KillSequence();
        }

        void KillSequence()
        {
            if (_currentSequence != null)
            {
                _currentSequence.Kill();
                _currentSequence = null;
            }
        }

        public void RunSequence(Sequence task)
        {
            KillSequence();
            _currentSequence = task;
        }
    }

    async UniTask PrepareEffects(List<StoneOfPlayerInfo> results)
    {
        async UniTask Prepare(StoneOfPlayerInfo result)
        {
            var set = new StoneFallEffectSet();
            set.Setup(GetEffectName);
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(result.SkillId);
            await set.Load(skillConfig.SP_LEVEL);
            DicAdd<StoneOfPlayerInfo, StoneFallEffectSet>.Add(_effectDic, result, set);
        }
        var tasks = new List<UniTask>();
        foreach (var result in results)
        {
            tasks.Add(Prepare(result));
        }
        await UniTask.WhenAll(tasks);
    }
    
    public void Setup(string gotchaId, Action<string, string, int> nine)
    {
        this.gotchaId = gotchaId;
        
        GDGotchaBtn.Setup(nine);
        DMGotchaBtn.Setup(nine);
        
        Skip.onClick.AddListener(SkipStarFallAnim);
        SpeedOnce.onClick.AddListener(SpeedOneGotchaAnim);
        SetWaitPos();
    }

    public static void Close()
    {
        var layer = UILayerLoader.Get<GotchaResultLayer>();
        if (layer != null)
        {
            layer.Reset();
        }
        UILayerLoader.Remove<GotchaResultLayer>();
    }
    
    // 清理相关特效等等
    public void Reset()
    {
        _starFallen = false;
        _oneStarFallen = false;
        ClearDetail();
        ClearAllParticle();
    }

    void ClearAllParticle()
    {
        foreach (var kv in _effectDic)
        {
            kv.Value.Clear();
        }
        _effectDic.Clear();
    }

    void FallingStarsFade()
    {
        foreach (var kv in _effectDic)
        {
            kv.Value.StoneFlashFigure.ParticleSystem.Stop();
            kv.Value.StoneFigure.ParticleSystem.Stop();
        }
    }
}
