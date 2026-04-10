using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class PlayFabReadClient
{
    const string PLAYFAB_CUSTOM_ID = "PLAYFAB_CUSTOM_ID";
    const int LoginRetryMaxAttempts = 4;
    const float LoginRetryBaseDelaySeconds = 1.5f;
    const float LoginRetryMaxDelaySeconds = 6f;
    static readonly HashSet<PlayFabErrorCode> TransientLoginErrors = new HashSet<PlayFabErrorCode>
    {
        PlayFabErrorCode.Unknown,
        PlayFabErrorCode.UnknownError,
        PlayFabErrorCode.ConnectionError,
        PlayFabErrorCode.ServiceUnavailable,
        PlayFabErrorCode.InternalServerError,
        PlayFabErrorCode.DownstreamServiceUnavailable,
        PlayFabErrorCode.APIRequestLimitExceeded
    };
    static bool _loginInProgress;
    
    public static string CustomId
    {
        get
        {
#if UNITY_IOS
            if (PlayerPrefs.HasKey(PLAYFAB_CUSTOM_ID))
            {
                return PlayerPrefs.GetString(PLAYFAB_CUSTOM_ID);
            }

            var newId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PLAYFAB_CUSTOM_ID, newId);
            PlayerPrefs.Save();
            return newId;
#else
            // customId的默认值存在诸多疑问，1。29 我们试着把它先从 Guid.NewGuid()改成SystemInfo.deviceUniqueIdentifier
            //var customId = PlayerPrefs.GetString(PLAYFAB_CUSTOM_ID, Guid.NewGuid().ToString());
            var defaultId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(defaultId))
            {
                defaultId = Guid.NewGuid().ToString();
            }

            var customId = PlayerPrefs.GetString(PLAYFAB_CUSTOM_ID, defaultId);
            PlayerPrefs.SetString(PLAYFAB_CUSTOM_ID, customId);
            PlayerPrefs.Save();
            return customId;
#endif
        }
    }
    
    public static string DontShowFrontFight
    {
        get
        {
            var customId = PlayerPrefs.GetString("DontShowFrontFight", "False");
            return customId;
        }
        set => PlayerPrefs.SetString("DontShowFrontFight", value);
    }

    public enum LoginType
    {
        normal,
        dev
    }

    /// <summary>
    /// 以Username和Password来登陆，
    /// 是玩家在设备迁移的时候会用的方法
    /// 其中Username并非一定是这个账户的playfabid，
    /// 而是一个账户生成后按某种主动方式给添加的，
    /// 所以一个playfab玩家账号完全可能这个登陆用的Username是空。
    /// 我们采取的策略是在玩家账号生成瞬间靠自动化流程把玩家的playfabid赋值给Username
    /// 
    /// 有一个非常大的问题在于，如果玩家用这个方法登陆，
    /// 我们希望玩家输入的账号是直接与这个设备进行绑定，
    /// 那么如果当前的设备已经与其他账号进行了绑定的话，则必须先和那个账号进行松绑
    /// 
    /// </summary>
    /// <param name="email"></param>
    /// <param name="pw"></param>
    /// <param name="success"></param>
    /// <param name="fail"></param>
    public static void PlayFabEmailLogin(string email, string pw, Action<LoginResult, LoginType> success)
    {
        RunLoginWithRetry(
            (onSuccess, onError) =>
            {
                PlayFabClientAPI.LoginWithEmailAddress(
                    new LoginWithEmailAddressRequest
                    {
                        Email = email,
                        Password = pw,
                        TitleId = PlayFabSettings.TitleId
                    },
                    onSuccess,
                    onError
                );
            },
            result =>
            {
                DeleteAllLocalMails();
                success?.Invoke(result, LoginType.normal);
            },
            "Email"
        );
    }
    
    /// <summary>
    /// 日常登陆靠这个，前提是玩家的账号已经和deviceid进行绑定
    /// </summary>
    /// <param name="success"></param>
    /// <param name="fail"></param>
    public static void LoginByDevice(Action<LoginResult, LoginType> success)
    {
        RunLoginWithRetry(
            StartDeviceLogin,
            result => success?.Invoke(result, LoginType.normal),
            "Device"
        );
    }
    
    static MissionWatcher _missionWatcher;
    static bool _tutorialProgressGot;
    public static void LoginSuccess(LoginResult result, LoginType loginType = LoginType.normal)
    {
        _tutorialProgressGot = false;
        ProgressLayer.Loading(string.Empty);
        PlayerAccountInfo.Me = new PlayerAccountInfo
        {
            PlayFabId = result.PlayFabId
        };
        
        _missionWatcher = new MissionWatcher(
            new List<string>
            {
                "tutorialProgressGot", "accountInfoFinished"
            },
            ()=>
            {
                CloudScript.CheckIn(EnterMainScene);
            },
            () =>
            {
                PopupLayer.ArrangeWarnWindow("Network Error");
            }
        );
        // 尝试建立并获取玩家测试进度信息，因为延迟没有迅速获得的情况下会多次尝试
        TryProcessWithLimitedTimes(()=> CheckTutorialProgressGot(loginType), ()=> _tutorialProgressGot, 0);
        GetAccountInfo(AccountInfoFinished);
    }
    
    static void AccountInfoFinished(bool value)
    {
        _missionWatcher.Finish("accountInfoFinished", value);
    }

    private static readonly int MAXTry = 5;
    private static readonly float tryInterval = 1f;
    static void TryProcessWithLimitedTimes(Action tryProcess, Func<bool> check, int tryTime)
    {
        UniTask.Delay(TimeSpan.FromSeconds(tryInterval)).ContinueWith(()=>
        {
            tryTime += 1;
            if (tryTime == MAXTry || check())
            {
                return;
            }
            Debug.Log("wait for a initialized account, try time : "+ tryTime);
            tryProcess();
            TryProcessWithLimitedTimes(tryProcess, check, tryTime);
        });
    }

    static void CheckTutorialProgressGot(LoginType loginType)
    {
        PlayFabClientAPI.GetUserData
        (
            new GetUserDataRequest
            {
                PlayFabId = PlayerAccountInfo.Me.PlayFabId
            },
            (obj) =>
            {
                if (obj.Data.ContainsKey("TutorialProgress"))
                {
                    PlayerAccountInfo.Me.tutorialProgress = obj.Data["TutorialProgress"].Value;
                    _tutorialProgressGot = true;
                    _missionWatcher.Finish("tutorialProgressGot", true);
                }
                else // 代表是新账户
                {
                    if (loginType == LoginType.dev)
                    {
                        CloudScript.Common(
                            "grantDevItems",
                            (x) =>
                            {
                                Debug.Log("成功给予测试用账户额外财产"+ x.Logs);
                                //LoadItems(null);
                            }
                        );
                    }
                    
                    UpdateUserData(
                        new UpdateUserDataRequest()
                        {
                            Data = new Dictionary<string, string>
                            {
                                { "TutorialProgress", loginType == LoginType.normal? "Started" : "Finished" }
                            }
                        },
                        () =>
                        {
                            PlayerAccountInfo.Me.tutorialProgress = loginType == LoginType.normal ? "Started" : "Finished";
                            _tutorialProgressGot = true;
                            _missionWatcher.Finish("tutorialProgressGot", true);
                        },
                        () =>
                        {
                            _missionWatcher.Finish("tutorialProgressGot", false);
                        }
                    );
                }
            },
            errorCallback =>
            {
                PopupLayer.ArrangeWarnWindow(errorCallback.ErrorMessage);
            }
        );
    }
    
    static void EnterMainScene()
    {
        MainMenuNote.GoingTo = MainSceneStep.FrontPage;
        SceneManager.LoadScene(1);
    }

    static void RunLoginWithRetry(
        Action<Action<LoginResult>, Action<PlayFabError>> startRequest,
        Action<LoginResult> onSuccess,
        string loginSource)
    {
        if (startRequest == null)
        {
            Debug.LogError("Login request action is null.");
            return;
        }

        if (_loginInProgress)
        {
            Debug.LogWarning($"PlayFab login already running. Skip new {loginSource} request.");
            return;
        }

        _loginInProgress = true;
        RunLoginWithRetryAsync(startRequest, onSuccess, loginSource).Forget();
    }

    static async UniTaskVoid RunLoginWithRetryAsync(
        Action<Action<LoginResult>, Action<PlayFabError>> startRequest,
        Action<LoginResult> onSuccess,
        string loginSource)
    {
        PlayFabError lastError = null;
        try
        {
            for (int attempt = 1; attempt <= LoginRetryMaxAttempts; attempt++)
            {
                var (result, error) = await ExecuteLoginRequestAsync(startRequest);
                if (error == null)
                {
                    Debug.Log($"PlayFab {loginSource} login succeeded on attempt {attempt}.");
                    onSuccess?.Invoke(result);
                    return;
                }

                lastError = error;
                if (!IsTransientLoginError(error) || attempt == LoginRetryMaxAttempts)
                {
                    Debug.LogWarning($"PlayFab {loginSource} login failed with {error.Error}: {error.ErrorMessage}");
                    break;
                }

                var waitSeconds = GetLoginRetryDelaySeconds(attempt);
                Debug.LogWarning($"PlayFab {loginSource} login attempt {attempt} failed ({error.Error}). Retrying in {waitSeconds:0.0}s");
                await UniTask.Delay(TimeSpan.FromSeconds(waitSeconds));
            }
        }
        finally
        {
            _loginInProgress = false;
        }

        ErrorReport(lastError ?? new PlayFabError
        {
            Error = PlayFabErrorCode.Unknown,
            ErrorMessage = "Login failed."
        });
    }

    static UniTask<(LoginResult result, PlayFabError error)> ExecuteLoginRequestAsync(
        Action<Action<LoginResult>, Action<PlayFabError>> startRequest)
    {
        var tcs = new UniTaskCompletionSource<(LoginResult, PlayFabError)>();
        try
        {
            startRequest(
                result => tcs.TrySetResult((result, null)),
                error => tcs.TrySetResult((null, error ?? new PlayFabError
                {
                    Error = PlayFabErrorCode.Unknown,
                    ErrorMessage = "Unknown login error."
                }))
            );
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            tcs.TrySetResult((null, new PlayFabError
            {
                Error = PlayFabErrorCode.Unknown,
                ErrorMessage = ex.Message
            }));
        }

        return tcs.Task;
    }

    static bool IsTransientLoginError(PlayFabError error)
    {
        if (error == null)
        {
            return false;
        }

        if (TransientLoginErrors.Contains(error.Error))
        {
            return true;
        }

        if (error.HttpCode == 0 || error.HttpCode == 408)
        {
            return true;
        }

        return error.HttpCode >= 500 && error.HttpCode < 600;
    }

    static float GetLoginRetryDelaySeconds(int attempt)
    {
        var waitSeconds = LoginRetryBaseDelaySeconds * Mathf.Pow(2f, attempt - 1);
        return Mathf.Min(waitSeconds, LoginRetryMaxDelaySeconds);
    }

    static void StartDeviceLogin(Action<LoginResult> onSuccess, Action<PlayFabError> onError)
    {
#if UNITY_IOS
        PlayFabClientAPI.LoginWithIOSDeviceID(
            new LoginWithIOSDeviceIDRequest
            {
                DeviceId = CustomId,
                CreateAccount = true
            },
            onSuccess,
            onError
        );
        return;
#endif

#if UNITY_ANDROID
        PlayFabClientAPI.LoginWithAndroidDeviceID(
            new LoginWithAndroidDeviceIDRequest
            {
                AndroidDeviceId = CustomId,
                CreateAccount = true
            },
            onSuccess,
            onError
        );
        return;
#endif

        PlayFabClientAPI.LoginWithCustomID(
            new LoginWithCustomIDRequest
            {
                CustomId = CustomId,
                CreateAccount = true
            },
            onSuccess,
            onError
        );
    }
}
