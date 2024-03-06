using PlayFab;
using PlayFab.ClientModels;
using System;

public partial class PlayFabReadClient
{
    /// <summary>
    /// 真正的CustomId登陆，用来以添加账号丰富竞技场
    /// </summary>
    /// <param name="success"></param>
    /// <param name="fail"></param>
    public static void LoginByCustomId(string customId, Action<LoginResult, LoginType> success)
    {
        PlayFabClientAPI.LoginWithCustomID(
            new LoginWithCustomIDRequest
            {
                CustomId = customId,
                CreateAccount = true
            },
            (x)=>success.Invoke(x, LoginType.dev),
            ErrorReport
        );
    }
    
    public static void DevUserLogin(string id)
    {
        LoginByCustomId(id, LoginSuccess);
    }
}
