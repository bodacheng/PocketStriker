using System;

public partial class PlayFabReadClient
{
    public static void LinkAccountPopup(Action success)
    {
        PopupLayer.ArrangeConfirmWindow(
            () =>
            {
                LinkDevice(
                    () =>
                    {
                        PopupLayer.ArrangeWarnWindow(" 已经关联账户 ");
                        success.Invoke();
                    }
                );
            },
            "当前设备没和这个账户进行绑定，绑定一下？绑定了的话。。");
    }
    
    public static void UnLinkAccountPopup(Action success)
    {
        PopupLayer.ArrangeConfirmWindow(
            () =>
            {
                UnLinkDevice(
                    PlayerAccountInfo.Me.currentLinkedDeviceId,
                    () =>
                    {
                        PopupLayer.ArrangeWarnWindow(" 已经与当前设备断开链接 ");
                        success.Invoke();
                    }
                );
            },
            "要把当前设备和当前账户断开链接？");
    }
}
