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
    public void Initialise()
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
    
    void SwitchTab(int step) // step 1:main ,step 2: login by pw
    {
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
