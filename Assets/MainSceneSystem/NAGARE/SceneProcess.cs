namespace mainMenu
{
    public abstract class SceneProcess
    {
        public virtual void ProcessEnter()
        {
        }
        
        public virtual void ProcessEnter<T>(T t)
        {
        }

        public virtual void ProcessEnd()
        {
        }

        public virtual bool CanEnterOtherProcess()
        {
            return false;
        }

        public virtual void LocalUpdate()
        {
        }
    }
}
