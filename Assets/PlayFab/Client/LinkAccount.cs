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
                        PopupLayer.ArrangeWarnWindow(Translate.Get("AccountLinked"));
                        success.Invoke();
                    }
                );
            },
            Translate.Get("HowAboutLinkDevice"));
    }
    
    public static void DeleteAccountPopup(Action success)
    {
        PopupLayer.ArrangeConfirmWindow(
            () =>
            {
                // 再确认
                PopupLayer.ArrangeConfirmWindow(
                    () =>
                    {
                        success.Invoke();
                        // 底下这个操作就是放飞自我
                        UnLinkDevice(
                            PlayerAccountInfo.Me.currentLinkedDeviceId,
                            DeletePlayer
                        );
                    },
                    () =>
                    {
                    },
                    Translate.Get("DeleteAccountConfirm2"));
            },
            () =>
            {
            },
            Translate.Get("DeleteAccountConfirm1"));
    }
}
