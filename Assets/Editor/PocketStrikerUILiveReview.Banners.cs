using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class PocketStrikerUILiveReview
{
    [Serializable] sealed class BannerCase
    {
        public string page, language;
        public int width,height;
        public float bannerPixels,rowGapPixels,navigationGapPixels;
        public Rect expectedContent,actualContent;
        public List<string> errors = new List<string>();
    }
    [Serializable] sealed class BannerReport
    {
        public string scope = "Actual logged-in menu scenes and UI, simulated native banner rectangle/heights and explicit device safe insets. No ad network request or ad click. Includes landscape geometry; the shipped game remains portrait. Native phone SDK presentation is not verified.";
        public List<BannerCase> cases = new List<BannerCase>();
        public bool passed;
    }
    static BannerReport bannerReport;
    static RectTransform simulatedBanner;
    static Rect? bannerSafeOverride;
    static float requestedBannerHeight = 50;
    static BannerAds forcedBannerOwner;
    static readonly MethodInfo SetBannerHeight = typeof(BannerAds).GetMethod("SetOccupiedHeight",Fields);
    static bool BannerSimulation => Environment.GetEnvironmentVariable("POCKETSTRIKER_BANNER_LIVE") == "1";

    static void DrawSimulatedBanner()
    {
        if(!BannerSimulation || PosCal.Canvas == null) return;
        var layout = PosCal.Canvas.GetComponent<PortraitSafeAreaLayout>();
        if(layout != null) typeof(PortraitSafeAreaLayout).GetField("safeAreaForValidation",Fields).SetValue(layout,bannerSafeOverride);
        if(BannerAds.target != null) SetBannerHeight.Invoke(BannerAds.target,new object[]{requestedBannerHeight});
        if(simulatedBanner == null)
        {
            var go = new GameObject("Simulated native banner",typeof(RectTransform),typeof(Canvas),typeof(Image));
            simulatedBanner = (RectTransform)go.transform; simulatedBanner.SetParent(PosCal.Canvas.transform,false);
            var canvas = go.GetComponent<Canvas>(); canvas.overrideSorting=true; canvas.sortingOrder=32000;
            var image = go.GetComponent<Image>(); image.color=new Color(.96f,.95f,.89f,1); image.raycastTarget=false;
            var label = new GameObject("Simulation label",typeof(RectTransform),typeof(Text)); label.transform.SetParent(simulatedBanner,false);
            var rt=(RectTransform)label.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=30;text.resizeTextForBestFit=true;text.resizeTextMinSize=18;text.resizeTextMaxSize=30;
            text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.1f,.17f,.2f);text.raycastTarget=false;
        }
        float height=BannerAds.OccupiedHeightPixels;
        var safe=bannerSafeOverride??Screen.safeArea;
        simulatedBanner.anchorMin=new Vector2(safe.xMin/Screen.width,(safe.yMax-height)/Screen.height);
        simulatedBanner.anchorMax=new Vector2(Mathf.Min(safe.xMax,safe.xMin+Screen.width*.5f)/Screen.width,safe.yMax/Screen.height);
        simulatedBanner.offsetMin=simulatedBanner.offsetMax=Vector2.zero;
        simulatedBanner.GetComponentInChildren<Text>(true).text="SIMULATED AD · "+height+" px";
        simulatedBanner.gameObject.SetActive(height>0);
    }
    static async UniTask BannerNavigationReview()
    {
        bannerReport=new BannerReport(); var oldLanguage=AppSetting.Value.Language;
        report.cases.Add(new Case{name="home-banner-and-entry-spacing"});
        report.cases.Add(new Case{name="arena-header-and-banner-spacing"});
        try
        {
            await Check("home-banner-and-entry-spacing",async()=>
            {
                await Home(); await BannerMatrix("home");
                current.note="45 live menu geometry cases: 5 viewport/safe-area shapes × 3 languages × no/50/90px simulated banners, repeated initialization, home row/nav clearance.";
                Require(!bannerReport.cases.Any(x=>x.page=="home"&&x.errors.Count>0),"Home banner geometry failed; see banner-layout.json.");
            });
            await ResetBannerViewport(oldLanguage);
            await Check("arena-header-and-banner-spacing",async()=>
            {
                await Home();await Click(Field<LowerBarIcon>(Layer<FrontLayer>(),"ArenaBtn").BOButton);await Page<ArenaLayer>(MainSceneStep.Arena,45);
                // Arena normally removes UpperInfoBar (and its ad). A separate
                // test owner also checks its geometry if a top ad is introduced.
                forcedBannerOwner=new GameObject("Arena banner simulation owner").AddComponent<BannerAds>();
                await BannerMatrix("arena");
                current.note="45 live arena geometry cases, native current opponents/team, four-digit ticket count, localized title/ranking separation. Arena ordinarily has no banner; its advertised variants are explicit simulations.";
                Require(!bannerReport.cases.Any(x=>x.page=="arena"&&x.errors.Count>0),"Arena header geometry failed; see banner-layout.json.");
            });
        }
        finally
        {
            if(forcedBannerOwner!=null) UnityEngine.Object.Destroy(forcedBannerOwner.gameObject);
            await ResetBannerViewport(oldLanguage);
            bannerReport.passed=bannerReport.cases.Count==90&&bannerReport.cases.All(x=>x.errors.Count==0);
            File.WriteAllText(Path.Combine(Output,"banner-layout.json"),JsonUtility.ToJson(bannerReport,true));
        }
    }
    static async UniTask ResetBannerViewport(SystemLanguage language)
    {
        bannerSafeOverride=null;requestedBannerHeight=50;AppSetting.Value.Language=language;
        SetReviewSize(540,960); foreach(var c in UnityEngine.Object.FindObjectsByType<LanguageConverter>(FindObjectsSortMode.None)) c.Change();
        DrawSimulatedBanner();await UniTask.DelayFrame(4);
    }
    static void SetReviewSize(int width,int height)
    {
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView",true);
        type.GetMethod("SetCustomResolution",Fields).Invoke(EditorWindow.GetWindow(type),new object[]{new Vector2(width,height),"Banner regression"});
    }
    static async UniTask BannerMatrix(string page)
    {
        var devices=new[]{(w:540,h:960,l:0,b:0,r:0,t:0),(w:375,h:667,l:0,b:0,r:0,t:0),
            (w:390,h:844,l:0,b:34,r:0,t:59),(w:768,h:1024,l:0,b:20,r:0,t:24),(w:844,h:390,l:59,b:21,r:59,t:0)};
        foreach(var device in devices)
        {
            SetReviewSize(device.w,device.h); await UniTask.DelayFrame(4);
            Require(Screen.width==device.w&&Screen.height==device.h,"Requested test viewport was not applied.");
            bannerSafeOverride=new Rect(device.l,device.b,device.w-device.l-device.r,device.h-device.b-device.t);
            foreach(var language in new[]{SystemLanguage.Chinese,SystemLanguage.English,SystemLanguage.Japanese})
            foreach(float banner in new[]{0f,50f,90f})
            {
                AppSetting.Value.Language=language;
                foreach(var c in UnityEngine.Object.FindObjectsByType<LanguageConverter>(FindObjectsSortMode.None))c.Change();
                requestedBannerHeight=banner;DrawSimulatedBanner();await UniTask.DelayFrame(4);Canvas.ForceUpdateCanvases();
                var expected=bannerSafeOverride.Value;expected.yMax-=banner;
                var cse=new BannerCase{page=page,language=language.ToString(),width=device.w,height=device.h,bannerPixels=banner,
                    expectedContent=expected,actualContent=PixelRect(PosCal.SafeAreaRect)};
                bannerReport.cases.Add(cse);
                if((cse.actualContent.min-expected.min).sqrMagnitude>.05f||(cse.actualContent.max-expected.max).sqrMagnitude>.05f)cse.errors.Add("Content bounds do not reserve the ad exactly once.");
                var layout=PosCal.Canvas.GetComponent<PortraitSafeAreaLayout>();layout.Initialize(PosCal.Canvas,PosCal.SafeAreaRect);await UniTask.DelayFrame(2);
                if((PixelRect(PosCal.SafeAreaRect).size-cse.actualContent.size).sqrMagnitude>.05f)cse.errors.Add("Repeated initialization compounded the inset.");
                if(page=="home") CheckHomeSpacing(cse); else CheckArenaSpacing(cse);
                if(device.w==390&&language==SystemLanguage.Chinese)await Screenshot(page+"-banner-"+banner);
                if(device.w==768&&language==SystemLanguage.English&&banner==90)await Screenshot(page+"-tablet-en-banner");
                if(device.w==844&&language==SystemLanguage.Japanese&&banner==50)await Screenshot(page+"-landscape-jp-banner");
                File.WriteAllText(Path.Combine(Output,"banner-layout.json"),JsonUtility.ToJson(bannerReport,true));
            }
        }
    }
    static void CheckHomeSpacing(BannerCase c)
    {
        var home=Layer<FrontLayer>();
        var upper=PixelRect((RectTransform)Field<LowerBarIcon>(home,"ArenaBtn").transform);
        var lower=PixelRect((RectTransform)Field<Button>(home,"TrainBtn").transform);
        float navTop=Layer<LowerMainBar>().GetComponentsInChildren<LowerBarIcon>().Max(x=>PixelRect((RectTransform)x.BOButton.transform).yMax);
        float scale=PosCal.Canvas.scaleFactor;
        c.rowGapPixels=upper.yMin-lower.yMax;c.navigationGapPixels=lower.yMin-navTop;
        if(c.rowGapPixels<16*scale-1||c.rowGapPixels>40*scale+1)c.errors.Add("Entry row gap is outside 16–40 canvas units: "+c.rowGapPixels/scale);
        if(c.navigationGapPixels<55*scale-1)c.errors.Add("Lower entries too close to navigation: "+c.navigationGapPixels/scale);
        foreach(var button in home.GetComponentsInChildren<Button>().Where(x=>x.IsInteractable()))
        {
            var bounds=PixelRect((RectTransform)button.transform);
            if(!c.actualContent.Contains(bounds.center))c.errors.Add("Home control center outside content: "+button.name);
        }
        var upperBar=Layer<UpperInfoBar>();
        foreach(var control in upperBar.GetComponentsInChildren<Button>().Where(x=>x.IsInteractable()))
            if(PixelRect((RectTransform)control.transform).yMax>c.actualContent.yMax+1)c.errors.Add("Upper bar control enters banner: "+control.name);
    }
    static void CheckArenaSpacing(BannerCase c)
    {
        var arena=Layer<ArenaLayer>();var label=(RectTransform)arena.transform.Find("top/Line/ChooseYourOpponent");
        foreach(var key in new[]{"rankingPageBtn","rewardBtn"})
            if(PixelRect(label).Overlaps(PixelRect((RectTransform)Field<Button>(arena,key).transform)))c.errors.Add("Arena title overlaps "+key);
        var ticket=Field<Text>(arena,"ticketCount");var original=ticket.text;ticket.text="9905";
        ticket.cachedTextGenerator.Populate(ticket.text,ticket.GetGenerationSettings(ticket.rectTransform.rect.size));
        if(ticket.cachedTextGenerator.lineCount!=1)c.errors.Add("Four-digit ticket value wraps.");
        ticket.text=original;
        var heading=label.GetComponent<Text>();
        var generator=heading.cachedTextGenerator;
        generator.Populate(heading.text,heading.GetGenerationSettings(label.rect.size));
        // preferredHeight uses the authored font size, ignoring best-fit shrink.
        // Check the actual fitted glyph run instead of flagging fitting English.
        if(generator.lineCount!=1 || generator.characterCountVisible<heading.text.Length)
            c.errors.Add("Localized opponent heading is clipped or wraps: lines="+generator.lineCount+", visible="+generator.characterCountVisible+"/"+heading.text.Length);
        foreach(var target in arena.GetComponentsInChildren<Button>().Where(x=>x.IsInteractable()))
            if(!c.actualContent.Contains(PixelRect((RectTransform)target.transform).center))c.errors.Add("Arena button outside content: "+target.name);
    }
    static Rect PixelRect(RectTransform rt)
    {
        var corners=new Vector3[4];rt.GetWorldCorners(corners);var canvas=rt.GetComponentInParent<Canvas>().rootCanvas;
        var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var min=RectTransformUtility.WorldToScreenPoint(cam,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(cam,corners[2]);
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
}
