using System;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;

public class ExplainCombo : TutorialProcess
{
    private bool comboFinished = false;
    private SkillEditLayer _skillEditLayer;
    public override bool CanEnterOtherProcess()
    {
        return comboFinished;
    }

    public override void ProcessEnter()
    {
        Process();
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
}
