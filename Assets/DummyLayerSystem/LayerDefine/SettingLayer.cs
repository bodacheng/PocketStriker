using DummyLayerSystem;
using UnityEngine;
using UnityEngine.UI;

public class SettingLayer : UILayer
{
    [SerializeField] RectTransform selectedFrame;
    
    #region Btns
    [SerializeField] BOButton accountBtn;
    [SerializeField] BOButton volumeBtn;
    [SerializeField] BOButton deviceBtn;
    [SerializeField] BOButton supportBtn;
    [SerializeField] BOButton languageBtn;
    [SerializeField] BOButton nickNameBtn;
    #endregion
    
    #region Panels
    [SerializeField] RectTransform volumePanel;
    [SerializeField] RectTransform accountPanel;
    [SerializeField] RectTransform devicePanel;
    [SerializeField] RectTransform supportPanel;
    [SerializeField] RectTransform languagePanel;
    [SerializeField] RectTransform nickNamePanel;
    #endregion
    
    #region Sound
    [SerializeField] Slider bgmSlider;
    [SerializeField] Slider effectsSoundsSlider;
    #endregion

    #region PlayFab Id
    [SerializeField] Text playFabId;
    #endregion

    #region Email
    [SerializeField] RectTransform emailSettingT;
    [SerializeField] RectTransform emailT;
    [SerializeField] InputField CurrentEmail;
    [SerializeField] InputField EmailInput;
    [SerializeField] BOButton EmailConfirmBtn;
    [SerializeField] BOButton SendPwResetBtn;
    #endregion

    #region linkDevice
    [SerializeField] BOButton linkDeviceBtn;
    [SerializeField] BOButton unLinkDeviceBtn;
    [SerializeField] Text linkInstruction;
    #endregion
    
    #region Support
    [SerializeField] BOButton privacyBtn;
    [SerializeField] BOButton contactBtn;
    #endregion
    
    #region Support
    [SerializeField] BOButton chBtn;
    [SerializeField] BOButton jpBtn;
    [SerializeField] BOButton enBtn;
    [SerializeField] GameObject selectedIndicator;
    #endregion
    
    #region nickName
    [SerializeField] Text nickName;
    [SerializeField] BOButton resetNickNameBtn;
    #endregion

    public void AccountPhase_EmailToBeSet()
    {
        emailSettingT.gameObject.SetActive(true);
        emailT.gameObject.SetActive(false);
        
        CurrentEmail.gameObject.SetActive(false);
        EmailInput.gameObject.SetActive(true);
        EmailConfirmBtn.gameObject.SetActive(true);
        SendPwResetBtn.gameObject.SetActive(false);
        
        EmailConfirmBtn.onClick.RemoveAllListeners();
        EmailConfirmBtn.onClick.AddListener(() =>
        {
            if (PlayerAccountInfo.Me.PlayFabUserName == null)
            {
                PlayFabReadClient.AddUserNameAndEmail(
                    PlayerAccountInfo.Me.PlayFabId, 
                    EmailInput.text.Trim(),
                    AccountPhase_EmailSet
                ); // 这个方法没有server版，只能客户端主动执行
            }
        });
    }
    
    public void AccountPhase_EmailSet()
    {
        emailSettingT.gameObject.SetActive(false);
        emailT.gameObject.SetActive(true);
        
        CurrentEmail.gameObject.SetActive(true);
        CurrentEmail.text = PlayerAccountInfo.Me.Email;
        playFabId.text = PlayerAccountInfo.Me.PlayFabId;
        
        EmailInput.gameObject.SetActive(false);
        EmailConfirmBtn.gameObject.SetActive(false);
        SendPwResetBtn.gameObject.SetActive(true);
        
        SendPwResetBtn.onClick.AddListener(
        () =>
            {
                PlayFabReadClient.SendPwResetEmail(
                    PlayerAccountInfo.Me.Email,
                    () =>
                    {
                        PopupLayer.ArrangeWarnWindow(" Email Sent ");
                    }
                );
            }
        );
    }

    void SetSelectedFrame(RectTransform target)
    {
        selectedFrame.position = target.position;
        selectedFrame.gameObject.SetActive(true);
    }
    
    public void Initialise()
    {
        nickName.text = PlayerAccountInfo.Me.TitleDisplayName;
        CurrentEmail.text = PlayerAccountInfo.Me.PlayFabUserName;
        
        void CloseAllPanels()
        {
            volumePanel.gameObject.SetActive(false);
            accountPanel.gameObject.SetActive(false);
            devicePanel.gameObject.SetActive(false);
            supportPanel.gameObject.SetActive(false);
            nickNamePanel.gameObject.SetActive(false);
            languagePanel.gameObject.SetActive(false);
        }
        
        volumeBtn.onClick.AddListener(() =>
        {
            CloseAllPanels();
            volumePanel.gameObject.SetActive(true);
            SetSelectedFrame(volumeBtn.GetComponent<RectTransform>());
        });
        
        accountBtn.onClick.AddListener(() =>
        {
            CloseAllPanels();
            accountPanel.gameObject.SetActive(true);
            SetSelectedFrame(accountBtn.GetComponent<RectTransform>());
        });
        
        deviceBtn.onClick.AddListener(() =>
        {
            CloseAllPanels();
            devicePanel.gameObject.SetActive(true);
            SetSelectedFrame(deviceBtn.GetComponent<RectTransform>());
        });
        
        supportBtn.onClick.AddListener(() =>
        {
            CloseAllPanels();
            supportPanel.gameObject.SetActive(true);
            SetSelectedFrame(supportBtn.GetComponent<RectTransform>());
        });

        void LanguageIndicator()
        {
            switch (AppSetting.Value.Language)
            {
                case SystemLanguage.English:
                    selectedIndicator.transform.SetParent(enBtn.transform);
                    break;
                case SystemLanguage.Japanese:
                    selectedIndicator.transform.SetParent(jpBtn.transform);
                    break;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    selectedIndicator.transform.SetParent(chBtn.transform);
                    break;
            }
            selectedIndicator.transform.localPosition= Vector3.zero;
        }

        async void SetLanguage(SystemLanguage code)
        {
            AppSetting.Value.Language = code;
            LanguageConverterManger.ChangeLanguage();
            await SkillNameTable.LoadSkillNamesFromConfig();
            SkillConfigTable.RefreshSkillConfigDicForReference();
            LanguageIndicator();
        }
        
        languageBtn.onClick.AddListener(
            () =>
            {
                CloseAllPanels();
                languagePanel.gameObject.SetActive(true);
                enBtn.onClick.AddListener(() => { SetLanguage(SystemLanguage.English); });
                jpBtn.onClick.AddListener(() => { SetLanguage(SystemLanguage.Japanese); });
                chBtn.onClick.AddListener(() => { SetLanguage(SystemLanguage.Chinese); });
                SetSelectedFrame(languageBtn.GetComponent<RectTransform>());
            }
        );
        
        LanguageIndicator();
        
        nickNameBtn.onClick.AddListener(
            () =>
            {
                CloseAllPanels();
                nickNamePanel.gameObject.SetActive(true);
                SetSelectedFrame(nickNameBtn.GetComponent<RectTransform>());
                resetNickNameBtn.onClick.AddListener(
                    () =>
                    {
                        this.gameObject.SetActive(false);
                        SettingPage.SetNickName((x) =>
                        {
                            PopupLayer.ArrangeWarnWindow(Translate.Get("NicknameSet"));
                            nickName.text = x;
                            this.gameObject.SetActive(true);
                        }, 
                        true, () =>
                        {
                            this.gameObject.SetActive(true);
                        });
                    }
                );
            }
        );
        
        ResetSliders();
        
        linkDeviceBtn.onClick.AddListener(() =>
            {
                PlayFabReadClient.LinkAccountPopup(RefreshLinkDeviceBtn);
            }
        );
        unLinkDeviceBtn.onClick.AddListener(() =>
            {
                //PlayFabReadClient.UnLinkAccountPopup(RefreshLinkDeviceBtn);
            }
        );
        
        privacyBtn.onClick.AddListener(() =>
        {
            Application.OpenURL("https://mugencombat.webnode.jp/purofiru/");
        });
        
        contactBtn.onClick.AddListener(() =>
        {
            Application.OpenURL("https://mugencombat.webnode.jp/o-weni-hewase/");
        });
        accountBtn.onClick.Invoke();
    }

    public void RefreshLinkDeviceBtn()
    {
        unLinkDeviceBtn.gameObject.SetActive(PlayerAccountInfo.Me.currentLinkedDeviceId == PlayFabReadClient.CustomId);
        linkDeviceBtn.gameObject.SetActive(PlayerAccountInfo.Me.currentLinkedDeviceId != PlayFabReadClient.CustomId);
        linkInstruction.text = PlayerAccountInfo.Me.currentLinkedDeviceId == PlayFabReadClient.CustomId ? 
            Translate.Get("DeviceBindInstruction") : 
            Translate.Get("DeviceNotBindInstruction");
    }
    
    public static void Close()
    {
        AppSetting.Save();
        UILayerLoader.Remove<SettingLayer>();
    }
    
    void ResetSliders()
    {
        effectsSoundsSlider.value = AppSetting.Value.EffectsVolume;
        bgmSlider.value = AppSetting.Value.BgmVolume;
    }
    
    public void OnBgmChange()
    {
        AppSetting.Value.BgmVolume = bgmSlider.value;
    }
    
    public void OnEffectChange()
    {
        AppSetting.Value.EffectsVolume = effectsSoundsSlider.value;
    }
}