using DummyLayerSystem;
using PlayFab;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class PlayFabReadClient
{
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
        Debug.Log("error.ErrorMessage:"+ error.Error);
        if (!Application.isPlaying)
        {
            return;
        }

        switch (error.Error)
        {
            case PlayFabErrorCode.NotAuthorizedByTitle:
                PopupLayer.ArrangeWarnWindow(
                    () => { HandleErrorReturn(returnToMainMenu); },
                    Translate.Get("NotAuthorizedByTitle"));
                break;
            case PlayFabErrorCode.ConnectionError:
                PopupLayer.ArrangeWarnWindow(
                    () => { HandleErrorReturn(returnToMainMenu); },
                    Translate.Get("ReturnToLobbyForConnectionError"));
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
                    () => { HandleErrorReturn(returnToMainMenu); },
                    Translate.Get("ConnectionError"));
                break;
        }
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
