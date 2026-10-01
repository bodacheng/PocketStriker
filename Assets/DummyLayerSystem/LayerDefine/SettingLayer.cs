using System;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    
    public BOButton AccountBtn=>accountBtn;
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
    [SerializeField] BOButton deleteAccountBtn;
    #endregion

    #region linkDevice
    [SerializeField] BOButton linkDeviceBtn;
    [SerializeField] BOButton unLinkDeviceBtn;
    [SerializeField] Text linkInstruction;
    #endregion
    
    #region Support
    [SerializeField] BOButton privacyBtn;
    [SerializeField] BOButton contactBtn;
    [SerializeField] BOButton privacyOptionsBtn;
    [SerializeField] Text privacyOptionsLabel;
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

    bool _initialized;
    bool _languageChanging;
    bool _nicknameDialogOpen;
    int _nicknameDialogVersion;

    // Instance-scoped service seams keep the real click flow independently testable.
    Func<UniTask> _reloadSkillNames = SkillNameTable.LoadSkillNamesFromConfig;
    Action _refreshSkillConfig = SkillConfigTable.RefreshSkillConfigDicForReference;
    Action _changeLanguagePresentation = LanguageConverterManger.ChangeLanguage;
    Action<Action<string>, Action> _openNickname = (success, cancel) => SettingPage.SetNickName(success, true, cancel);
    Action _nicknameSaved = () => PopupLayer.ArrangeWarnWindow(Translate.Get("NicknameSet"));
#if UNITY_EDITOR
    Action _confirmEmailForValidation;
    Action _sendPasswordResetForValidation;
#endif

    bool _contentCenteringPending;
    Rect _lastAvailableRect;

    void OnEnable()
    {
        _contentCenteringPending = true;
        AdsInitializer.PrivacyOptionsAvailabilityChanged += RefreshPrivacyOptionsButton;
        RefreshPrivacyOptionsButton();
    }

    void OnDisable()
    {
        AdsInitializer.PrivacyOptionsAvailabilityChanged -= RefreshPrivacyOptionsButton;
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        _contentCenteringPending = true;
    }

    void LateUpdate()
    {
        // Safe-area changes can resize only the middle area, leaving the
        // SettingLayer root dimensions unchanged.
        if (MiddleArea != null && MiddleArea.rect != _lastAvailableRect)
            _contentCenteringPending = true;
        if (!_contentCenteringPending) return;
        _contentCenteringPending = false;
        CenterActivePanelContent();
    }

    void RefreshContentCentering()
    {
        // Apply immediately when changing tabs, then once after this frame's
        // layout pass (for translated text, sliders and newly enabled panels).
        _contentCenteringPending = true;
        CenterActivePanelContent();
    }

    void CenterActivePanelContent()
    {
        if (MiddleArea == null) return;
        _lastAvailableRect = MiddleArea.rect;
        CenterVisibleContent(volumePanel, MiddleArea);
        CenterVisibleContent(accountPanel, MiddleArea);
        CenterVisibleContent(devicePanel, MiddleArea);
        CenterVisibleContent(supportPanel, MiddleArea);
        CenterVisibleContent(languagePanel, MiddleArea);
        CenterVisibleContent(nickNamePanel, MiddleArea);
    }

    /// <summary>
    /// Centers a tab's visible labels and controls in the available window,
    /// preserving all spacing within the tab. The oversized authored panel and
    /// decorative backgrounds do not contribute to the content bounds.
    /// </summary>
    public static bool CenterVisibleContent(RectTransform panel, RectTransform availableArea)
    {
        if (panel == null || availableArea == null || !panel.gameObject.activeInHierarchy)
            return false;

        var groups = panel.GetComponentsInChildren<LayoutGroup>();
        for (int i = groups.Length - 1; i >= 0; i--)
        {
            if (groups[i].isActiveAndEnabled)
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)groups[i].transform);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        float lower = float.PositiveInfinity;
        float upper = float.NegativeInfinity;
        var corners = new Vector3[4];

        void Include(RectTransform rect)
        {
            if (!IsVisibleContent(rect, panel)) return;
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                float y = availableArea.InverseTransformPoint(corner).y;
                lower = Mathf.Min(lower, y);
                upper = Mathf.Max(upper, y);
            }
        }

        foreach (var label in panel.GetComponentsInChildren<Text>())
        {
            if (label.isActiveAndEnabled && label.color.a > 0 &&
                !string.IsNullOrWhiteSpace(label.text))
                Include(label.rectTransform);
        }
        foreach (var control in panel.GetComponentsInChildren<Selectable>())
        {
            if (control.isActiveAndEnabled)
                Include((RectTransform)control.transform);
        }

        if (float.IsInfinity(lower)) return false;
        float offset = availableArea.rect.center.y - (lower + upper) * 0.5f;
        if (Mathf.Abs(offset) > 0.01f)
            panel.position += availableArea.TransformVector(new Vector3(0, offset, 0));
        return true;
    }

    static bool IsVisibleContent(RectTransform rect, RectTransform panel)
    {
        if (!rect.gameObject.activeInHierarchy) return false;
        for (Transform parent = rect; parent != null; parent = parent.parent)
        {
            bool ignoreParentGroups = false;
            foreach (var group in parent.GetComponents<CanvasGroup>())
            {
                if (!group.enabled) continue;
                if (group.alpha <= 0) return false;
                ignoreParentGroups |= group.ignoreParentGroups;
            }
            // A fade on the window or its ancestors must not delay laying out
            // the selected tab. Only visibility within the tab affects bounds.
            if (ignoreParentGroups || parent == panel) break;
        }
        return true;
    }

    public void AccountPhase_EmailToBeSet()
    {
        emailSettingT.gameObject.SetActive(true);
        emailT.gameObject.SetActive(false);
        
        CurrentEmail.gameObject.SetActive(false);
        EmailInput.gameObject.SetActive(true);
        EmailConfirmBtn.gameObject.SetActive(true);
        SendPwResetBtn.gameObject.SetActive(false);
        
        EmailConfirmBtn.onClick.RemoveListener(ConfirmEmail);
        EmailConfirmBtn.onClick.AddListener(ConfirmEmail);
        RefreshContentCentering();
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
        
        SendPwResetBtn.onClick.RemoveListener(SendPasswordReset);
        SendPwResetBtn.onClick.AddListener(SendPasswordReset);
        RefreshContentCentering();
    }

    void ConfirmEmail()
    {
#if UNITY_EDITOR
        if (_confirmEmailForValidation != null) { _confirmEmailForValidation(); return; }
#endif
        if (PlayerAccountInfo.Me.PlayFabUserName == null)
            PlayFabReadClient.AddUserNameAndEmail(PlayerAccountInfo.Me.PlayFabId,
                EmailInput.text.Trim(), AccountPhase_EmailSet);
    }

    void SendPasswordReset()
    {
#if UNITY_EDITOR
        if (_sendPasswordResetForValidation != null) { _sendPasswordResetForValidation(); return; }
#endif
        PlayFabReadClient.SendPwResetEmail(PlayerAccountInfo.Me.Email,
            () => PopupLayer.ArrangeWarnWindow(" Email Sent "));
    }

    void LanguageIndicator()
    {
        Transform target = null;
        switch (AppSetting.Value.Language)
        {
            case SystemLanguage.English: target = enBtn.transform; break;
            case SystemLanguage.Japanese: target = jpBtn.transform; break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: target = chBtn.transform; break;
        }
        if (target != null) selectedIndicator.transform.SetParent(target, false);
        selectedIndicator.transform.localPosition = Vector3.zero;
    }

    async UniTask ChangeLanguage(SystemLanguage code)
    {
        // A click dispatched again in the same frame must not start a second load.
        if (_languageChanging || AppSetting.Value.Language == code) return;
        _languageChanging = true;
        var previousLanguage = AppSetting.Value.Language;
        bool enWasEnabled = enBtn.interactable, jpWasEnabled = jpBtn.interactable, chWasEnabled = chBtn.interactable;
        enBtn.interactable = jpBtn.interactable = chBtn.interactable = false;
        try
        {
            AppSetting.Value.Language = code;
            _changeLanguagePresentation();
            RefreshPrivacyOptionsButton();
            RefreshContentCentering();
            LanguageIndicator();
            await _reloadSkillNames();
            _refreshSkillConfig();
        }
        catch
        {
            // A failed resource load must not leave the selected language
            // pointing at a catalogue that never finished loading.
            AppSetting.Value.Language = previousLanguage;
            _changeLanguagePresentation();
            RefreshPrivacyOptionsButton();
            RefreshContentCentering();
            LanguageIndicator();
            throw;
        }
        finally
        {
            _languageChanging = false;
            if (this != null)
            {
                enBtn.interactable = enWasEnabled;
                jpBtn.interactable = jpWasEnabled;
                chBtn.interactable = chWasEnabled;
                LanguageIndicator();
                RefreshContentCentering();
            }
        }
    }

    void OpenNicknameDialog()
    {
        if (_nicknameDialogOpen) return;
        _nicknameDialogOpen = true;
        int version = ++_nicknameDialogVersion;
        gameObject.SetActive(false);
        void Resume() => ResumeNicknameDialog(version);
        try
        {
            _openNickname(value =>
            {
                if (!_nicknameDialogOpen || version != _nicknameDialogVersion) return;
                Resume();
                if (this == null || IsClosing) return;
                nickName.text = value;
                _nicknameSaved();
                RefreshContentCentering();
            }, Resume);
        }
        catch
        {
            Resume();
            throw;
        }
    }

    void ResumeNicknameDialog(int version)
    {
        if (version != _nicknameDialogVersion || !_nicknameDialogOpen) return;
        _nicknameDialogOpen = false;
        if (this != null && !IsClosing) gameObject.SetActive(true);
    }

    void SetSelectedFrame(RectTransform target)
    {
        selectedFrame.position = target.position;
        selectedFrame.sizeDelta = target.rect.size;
        selectedFrame.SetAsLastSibling();
        selectedFrame.gameObject.SetActive(true);
        // Keep the current section readable above opaque tab backgrounds.
        foreach (var button in new[] { accountBtn, volumeBtn, deviceBtn, supportBtn, languageBtn, nickNameBtn })
        {
            bool selected = button.transform == target;
            if (button.targetGraphic != null)
                button.targetGraphic.color = selected
                    ? new Color(0.27f, 0.32f, 0.30f) : new Color(0.14f, 0.24f, 0.29f);
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = selected
                ? new Color(0.96f, 0.78f, 0.43f) : new Color(0.95f, 0.94f, 0.86f);
        }
        RefreshContentCentering();
    }
    
    public void Initialise()
    {
        nickName.text = PlayerAccountInfo.Me.TitleDisplayName;
        CurrentEmail.text = PlayerAccountInfo.Me.PlayFabUserName;
        ResetSliders();
        LanguageIndicator();
        RefreshPrivacyOptionsButton();
        RefreshContentCentering();
        if (_initialized) return;
        _initialized = true;

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
            RefreshPrivacyOptionsButton();
            SetSelectedFrame(supportBtn.GetComponent<RectTransform>());
        });

        enBtn.onClick.AddListener(() => ChangeLanguage(SystemLanguage.English).Forget());
        jpBtn.onClick.AddListener(() => ChangeLanguage(SystemLanguage.Japanese).Forget());
        chBtn.onClick.AddListener(() => ChangeLanguage(SystemLanguage.Chinese).Forget());
        resetNickNameBtn.onClick.AddListener(OpenNicknameDialog);

        languageBtn.onClick.AddListener(
            () =>
            {
                CloseAllPanels();
                languagePanel.gameObject.SetActive(true);
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
            }
        );

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
            var language = AppSetting.Value != null ? AppSetting.Value.Language : Application.systemLanguage;
            string section = language == SystemLanguage.Japanese ? "ja" :
                language == SystemLanguage.English ? "en" : "zh";
            Application.OpenURL("https://personal-site.hotaru-studio.workers.dev/privacy/pocket-striker/#" + section);
        });

        privacyOptionsBtn.onClick.AddListener(AdsInitializer.ShowPrivacyOptionsForm);
        
        contactBtn.onClick.AddListener(() =>
        {
            Application.OpenURL("https://personal-site.hotaru-studio.workers.dev/#contact");
        });
        
        deleteAccountBtn.onClick.AddListener(() =>
        {
            PlayFabReadClient.DeleteAccountPopup(() =>
            {
                PopupLayer.ArrangeWarnWindow(
                    () =>
                    {
                        SceneManager.LoadScene(0);
                    },
                    Translate.Get("AccountDeleted")
                );
            });
        });
    }

    void RefreshPrivacyOptionsButton()
    {
        if (privacyOptionsBtn == null) return;

        // UMP may finish updating after the settings window has already opened.
        bool required = AdsInitializer.PrivacyOptionsRequired;
        if (privacyOptionsBtn.gameObject.activeSelf != required)
        {
            privacyOptionsBtn.gameObject.SetActive(required);
            RefreshContentCentering();
        }

        if (privacyOptionsLabel == null) return;
        var language = AppSetting.Value != null ? AppSetting.Value.Language : Application.systemLanguage;
        switch (language)
        {
            case SystemLanguage.Japanese:
                privacyOptionsLabel.text = "広告のプライバシー設定";
                break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                privacyOptionsLabel.text = "广告隐私选项";
                break;
            default:
                privacyOptionsLabel.text = "Ad Privacy Choices";
                break;
        }
    }

    public void RefreshLinkDeviceBtn()
    {
        unLinkDeviceBtn.gameObject.SetActive(PlayerAccountInfo.Me.currentLinkedDeviceId == PlayFabReadClient.CustomId);
        linkDeviceBtn.gameObject.SetActive(PlayerAccountInfo.Me.currentLinkedDeviceId != PlayFabReadClient.CustomId);
        linkInstruction.text = PlayerAccountInfo.Me.currentLinkedDeviceId == PlayFabReadClient.CustomId ? 
            Translate.Get("DeviceBindInstruction") : 
            Translate.Get("DeviceNotBindInstruction");
        RefreshContentCentering();
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
