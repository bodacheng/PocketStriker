using mainMenu;

namespace FightScene
{
    public abstract class FSceneProcess: SceneProcess
    {
        public SceneStep Step;
        public SceneStep nextProcessStep = SceneStep.None;//有的话代表本process存在一个注定会自然迁移到的下一个process。没的话代表本process不一定迁移到哪。
    }
    
    public enum SceneStep
    {
        None = 0,
        Preparing = 1,
        CountDown = 4,
        Fighting = 2,
        FightResultAnim = 5,
        FightOver = 3
    }
}