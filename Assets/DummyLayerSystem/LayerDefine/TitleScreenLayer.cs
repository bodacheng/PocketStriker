using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 这个layer的问题在于，它必须灵活的适应未来可能做出的一些改动
/// 就是说它既可能出现在"标题战斗"上，也可能出现在主界面
/// </summary>
public class TitleScreenLayer : UILayer
{
    // Main
    [SerializeField] RectTransform mainTab;
    [SerializeField] Image title;
    [SerializeField] BOButton touchScreenBtn;
    [SerializeField] BOButton accountLoginBtn;
    
    // Login by pw
    [SerializeField] RectTransform loginByPwTab;
    [SerializeField] InputField id;
    [SerializeField] InputField password;
    [SerializeField] BOButton loginBtn;
    [SerializeField] BOButton cancelBtn;

    // Dev login
    [SerializeField] InputField devId;
    [SerializeField] Button devEnter;
    [SerializeField] Button devLoginBtn;

    [SerializeField] Text version;
    
    private float titleAnimFactor = 0;
    private Tween titleTween;
    private Material titleMaterial;
    private bool eventsBound;
    public void Initialise(bool illustrated = false)
    {
        version.text = Application.version;
        if (!eventsBound)
        {
            touchScreenBtn.onClick.AddListener(TouchScreenLogin);
            accountLoginBtn.onClick.AddListener(() => SwitchTab(2));
            cancelBtn.onClick.AddListener(() => SwitchTab(1));
            loginBtn.onClick.AddListener(EmailLogin);
            devLoginBtn.onClick.AddListener(DevUserLogin);
            eventsBound = true;
        }
        devEnter.gameObject.SetActive(CommonSetting.DevMode);

        // Image.material is a shared asset. Animate a layer-owned instance so
        // returning to the title cannot mutate the source or another title.
        if (titleMaterial == null && title != null && title.material != null)
        {
            titleMaterial = new Material(title.material) { name = title.material.name + " (Runtime)", hideFlags = HideFlags.DontSave };
            title.material = titleMaterial;
        }
        if (illustrated) ApplyIllustratedLayout();
        titleAnimFactor = 0;
        if (titleMaterial != null) titleMaterial.SetFloat("_Animation_Factor", titleAnimFactor);
        titleTween?.Kill();
        titleTween = DOTween.To(() => titleAnimFactor, (x) => titleAnimFactor = x, 2, 10)
            .SetLink(gameObject)
            .OnUpdate(() =>
            {
                if (this == null || title == null || titleMaterial == null)
                {
                    return;
                }

                titleMaterial.SetFloat("_Animation_Factor", titleAnimFactor);
            });
    }
    
    void ApplyIllustratedLayout()
    {
        // Keep existing title branding above the fighters and controls on quiet foreground.
        var rect = title.rectTransform;
        rect.anchorMin = new Vector2(.07f, 1);
        rect.anchorMax = new Vector2(.93f, 1);
        rect.anchoredPosition = new Vector2(0, 80);
        rect.sizeDelta = new Vector2(0, 180);
        title.preserveAspect = true;
        title.GetComponent<SizeAdjustBySpriteSize>()?.AdjustSize();
        if (titleMaterial != null)
        {
            titleMaterial.SetColor("_MainColor", new Color(.08f, .15f, .20f));
            titleMaterial.SetColor("_OutlineColor", new Color(.13f, .19f, .23f));
        }
        version.fontSize = 34;
        version.color = new Color(.08f, .15f, .20f);
        var company = TopArea.Find("Company")?.GetComponent<Text>();
        if (company != null) { company.fontSize = 34; company.color = version.color; }
        var hint = mainTab.Find("touchScreenText") as RectTransform;
        if (hint != null)
        {
            hint.anchorMin = hint.anchorMax = new Vector2(.5f, 0);
            hint.anchoredPosition = new Vector2(0, -90);
            var text = hint.GetComponent<Text>();
            if (text != null) text.fontSize = 56;
        }
        var account = (RectTransform)accountLoginBtn.transform;
        account.anchorMin = account.anchorMax = new Vector2(.5f, 0);
        account.pivot = new Vector2(.5f, 0);
        account.anchoredPosition = new Vector2(0, 100);
        account.sizeDelta = new Vector2(480, 130);
    }

    void SwitchTab(int step) // step 1:main ,step 2: login by pw
    {
        accountLoginBtn.gameObject.SetActive(step == 1);
        if (step == 1)
        {
            mainTab.gameObject.SetActive(true);
            loginByPwTab.gameObject.SetActive(false);
        }
        else if (step == 2)
        {
            mainTab.gameObject.SetActive(false);
            loginByPwTab.gameObject.SetActive(true);
        }
    }
    
    void EmailLogin()
    {
        ProgressLayer.Loading("");
        PlayFabReadClient.PlayFabEmailLogin(
            id.text.Trim(), password.text.Trim(), 
            PlayFabReadClient.LoginSuccess);
    }
    
    void TouchScreenLogin()
    {
        ProgressLayer.Loading("");
        PlayFabReadClient.LoginByDevice(PlayFabReadClient.LoginSuccess);
    }

    void DevUserLogin()
    {
        PlayFabReadClient.LoginByCustomId(
            devId.text,
            PlayFabReadClient.LoginSuccess);
    }

    public override void OnDestroy()
    {
        titleTween?.Kill();
        titleTween = null;
        if (titleMaterial != null) Destroy(titleMaterial);
        titleMaterial = null;
        base.OnDestroy();
    }
}
