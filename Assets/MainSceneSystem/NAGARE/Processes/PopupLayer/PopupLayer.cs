using DummyLayerSystem;
using UnityEngine;
using UnityEngine.UI;

public partial class PopupLayer : UILayer
{
    [SerializeField] Image bigCurtain;
    
    static void Close()
    {
        UILayerLoader.Remove<PopupLayer>();
        ProgressLayer.Close();
    }
}
