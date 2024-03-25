using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using DummyLayerSystem;
using mainMenu;

public partial class GotchaResultLayer : UILayer
{
    private bool showFinished = false;
    public bool ShowFinished => showFinished;
    // Gotcha总动画过程 点击画面的话进入下一个星星
    public async UniTask WholeAnimProcess(List<StoneOfPlayerInfo> results)
    {
        UILayerLoader.Remove<LowerMainBar>();
        
        showFinished = false;
        Reset();
        NineForShow.transform.gameObject.SetActive(false);
        await UniTask.DelayFrame(1);
        await PrepareEffects(results);
        
        StarsFall.target.StartGachaEffect(true);
        
        starFallAnimWholeProcess = StartCoroutine (StarFallAnim(results));
        SpeedOnce.gameObject.SetActive(true);
        Skip.gameObject.SetActive(true);
        while(!_starFallen)
            await UniTask.DelayFrame(1);

        FallingStarsFade();
        SpeedOnce.gameObject.SetActive(false);
        Skip.gameObject.SetActive(false);
        NineForShow.transform.gameObject.SetActive(true);
        
        await UniTask.DelayFrame(1);
        
        StarsFall.target.LookReset();
        PosDecide();
        StarSortAnim(results);
        
        await UniTask.Delay( TimeSpan.FromSeconds( 2 ) );
        
        string a1 = null, a2 = null, a3 = null, b1 = null, b2 = null, b3 = null, c1 = null, c2 = null, c3 = null;
        for (var i = 0; i < results.Count; i++)
        {
            switch(i)
            {
                case 0:
                    a1 = results[i].SkillId;
                    break;
                case 1:
                    a2 = results[i].SkillId;
                    break;
                case 2:
                    a3 = results[i].SkillId;
                    break;
                case 3:
                    b1 = results[i].SkillId;
                    break;
                case 4:
                    b2 = results[i].SkillId;
                    break;
                case 5:
                    b3 = results[i].SkillId;
                    break;
                case 6:
                    c1 = results[i].SkillId;
                    break;
                case 7:
                    c2 = results[i].SkillId;
                    break;
                case 8:
                    c3 = results[i].SkillId;
                    break;
            }
        }
        
        await NineForShow.ShowStones(a1, a2, a3, b1, b2, b3, c1, c2, c3);
        StarsFall.target.StartGachaEffect(false);
        showFinished = true;
        
        if (PlayerAccountInfo.Me.tutorialProgress == "Finished")
            switch (gotchaId)
            {
                case "GDGotcha":
                    GDGotchaBtn.gameObject.SetActive(true);
                    break;
                case "DMGotcha":
                    DMGotchaBtn.gameObject.SetActive(true);
                    break;
            }
        
        var returnLayer = UILayerLoader.Load<ReturnLayer>();
        if (returnLayer != null)
        {
            returnLayer.gameObject.SetActive(true);
        }
        LowerMainBar.Open();
    }
}
