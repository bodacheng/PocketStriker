using System;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using PlayFab;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class PlayFabReadClient
{
    const int PlayFabRequestRetryMaxAttempts = 3;
    const float PlayFabRequestRetryBaseDelaySeconds = 0.75f;
    const float PlayFabRequestRetryMaxDelaySeconds = 3f;

    public static void ErrorReport(PlayFabError error)
    {
        ErrorReportInternal(error, true);
    }

    public static void ErrorReportStayInScene(PlayFabError error)
    {
        ErrorReportInternal(error, false);
    }

    static void ErrorReportInternal(PlayFabError error, bool returnToMainMenu)
    {
        if (error == null)
        {
            Debug.LogWarning("PlayFab error is null.");
            return;
        }

        Debug.LogWarning("PlayFab error: " + error.GenerateErrorReport());
        var shouldReturnToMainMenu = returnToMainMenu && ShouldReturnToMainMenu(error);
        if (Application.isPlaying)
        {
            switch (error.Error)
            {
                case PlayFabErrorCode.NotAuthorizedByTitle:
                    PopupLayer.ArrangeWarnWindow(
                        ()=>
                        {
                            HandleErrorReturn(shouldReturnToMainMenu);
                        },
                        Translate.Get("NotAuthorizedByTitle"));
                    break;
                case PlayFabErrorCode.ConnectionError:
                    PopupLayer.ArrangeWarnWindow(
                        ()=>
                        {
                            HandleErrorReturn(shouldReturnToMainMenu);
                        },
                        Translate.Get("ConnectionError"));
                    break;
                case PlayFabErrorCode.InvalidUsername:
                    PopupLayer.ArrangeWarnWindow(Translate.Get("InvalidUsername"));
                    break;
                case PlayFabErrorCode.DuplicateUsername:
                    PopupLayer.ArrangeWarnWindow(Translate.Get("DuplicateUsername"));
                    break;
                case PlayFabErrorCode.InvalidParams:
                    PopupLayer.ArrangeWarnWindow(Translate.Get("InvalidParams"));
                    break;
                case PlayFabErrorCode.AccountNotFound:
                    PopupLayer.ArrangeWarnWindow(Translate.Get("AccountNotFound"));
                    break;
                case PlayFabErrorCode.InvalidEmailOrPassword:
                    PopupLayer.ArrangeWarnWindow(Translate.Get("InvalidEmailOrPassword"));
                    break;
                default:
                    PopupLayer.ArrangeWarnWindow(
                        ()=>
                        {
                            HandleErrorReturn(shouldReturnToMainMenu);
                        },
                        GetPlayFabErrorMessage(error));
                    break;
            }
        }
    }

    static string GetPlayFabErrorMessage(PlayFabError error)
    {
        if (error == null)
        {
            return Translate.Get("ConnectionError");
        }

        if (error.Error == PlayFabErrorCode.ConnectionError)
        {
            return Translate.Get("ConnectionError");
        }

        if (!string.IsNullOrEmpty(error.ErrorMessage))
        {
            return error.ErrorMessage;
        }

        return error.Error.ToString();
    }

    static bool ShouldReturnToMainMenu(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.AccountBanned:
            case PlayFabErrorCode.InvalidSessionTicket:
            case PlayFabErrorCode.NotAuthenticated:
            case PlayFabErrorCode.ExpiredAuthToken:
            case PlayFabErrorCode.NotAuthorizedByTitle:
                return true;
            default:
                return false;
        }
    }

    public static bool ShouldRetryPlayFabRequest(PlayFabError error, int attempt)
    {
        return attempt < PlayFabRequestRetryMaxAttempts && IsTransientPlayFabError(error);
    }

    public static void RetryPlayFabRequest(Action retry, int attempt, string operation)
    {
        if (retry == null)
        {
            return;
        }

        var waitSeconds = GetPlayFabRequestRetryDelaySeconds(attempt);
        Debug.LogWarning($"PlayFab {operation} failed with a transient error. Retrying in {waitSeconds:0.0}s");
        UniTask.Delay(TimeSpan.FromSeconds(waitSeconds)).ContinueWith(retry).Forget();
    }

    public static bool IsTransientPlayFabError(PlayFabError error)
    {
        if (error == null)
        {
            return false;
        }

        switch (error.Error)
        {
            case PlayFabErrorCode.Unknown:
            case PlayFabErrorCode.UnknownError:
            case PlayFabErrorCode.ConnectionError:
            case PlayFabErrorCode.ServiceUnavailable:
            case PlayFabErrorCode.InternalServerError:
            case PlayFabErrorCode.DownstreamServiceUnavailable:
            case PlayFabErrorCode.APIRequestLimitExceeded:
            case PlayFabErrorCode.CloudScriptAPIRequestError:
            case PlayFabErrorCode.CloudScriptHTTPRequestError:
            case PlayFabErrorCode.CloudScriptAzureFunctionsHTTPRequestError:
                return true;
        }

        if (error.HttpCode == 0 || error.HttpCode == 408 || error.HttpCode == 429)
        {
            return true;
        }

        return error.HttpCode >= 500 && error.HttpCode < 600;
    }

    static float GetPlayFabRequestRetryDelaySeconds(int attempt)
    {
        var waitSeconds = PlayFabRequestRetryBaseDelaySeconds * Mathf.Pow(2f, attempt - 1);
        return Mathf.Min(waitSeconds, PlayFabRequestRetryMaxDelaySeconds);
    }

    static void HandleErrorReturn(bool returnToMainMenu)
    {
        if (!returnToMainMenu)
        {
            UILayerLoader.Remove<PopupLayer>();
            return;
        }

        if (SceneManager.GetActiveScene().buildIndex != 0)
        {
            SceneManager.LoadScene(0);
        }
        else
        {
            UILayerLoader.Remove<PopupLayer>();
        }
    }
}
