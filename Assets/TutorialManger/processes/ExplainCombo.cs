using System;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;

public class ExplainCombo : TutorialProcess
{
    private bool comboFinished = false;
    private SkillEditLayer _skillEditLayer;
    private ReturnLayer _returnLayer;
    public override bool CanEnterOtherProcess()
    {
        return comboFinished;
    }

    public override void ProcessEnter()
    {
        Process();
    }

    public override void LocalUpdate()
    {
        if (_returnLayer == null)
        {
            _returnLayer = UILayerLoader.Get<ReturnLayer>();
            _returnLayer.gameObject.SetActive(false);
        }
    }

    async void Process()
    {
        if (_skillEditLayer == null)
        {
            _skillEditLayer = UILayerLoader.Get<SkillEditLayer>();
            await _skillEditLayer.ShowCombo(true);
        }
        
        await UniTask.Delay(TimeSpan.FromSeconds(1));
        comboFinished = true;
    }

    public override void ProcessEnd()
    {
        
    }
}
