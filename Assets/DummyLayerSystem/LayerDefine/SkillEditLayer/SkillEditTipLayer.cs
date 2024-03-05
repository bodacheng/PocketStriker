using DummyLayerSystem;

public class SkillEditTipLayer : UILayer
{
    public void CloseTip()
    {
        UILayerLoader.Remove<SkillEditTipLayer>();
    }
}
