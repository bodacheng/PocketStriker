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

    bool _contentCenteringPending;
    Rect _lastAvailableRect;

    void OnEnable()
    {
        _contentCenteringPending = true;
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
        RefreshContentCentering();
    }

    void SetSelectedFrame(RectTransform target)
    {
        selectedFrame.position = target.position;
        selectedFrame.gameObject.SetActive(true);
        RefreshContentCentering();
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
            RefreshContentCentering();
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
        
        deleteAccountBtn.SetListener(() =>
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
