using System.Collections.Generic;
using UnityEngine;

namespace mainMenu
{
    public class ProcessesRunner
    {
        static ProcessesRunner instance_main;
        public static ProcessesRunner Main
        {
            get
            {
                if (instance_main == null)
                {
                    instance_main = new ProcessesRunner();
                }
                return instance_main;
            }
        }
        
        public MSceneProcess lastProcess;
        public MSceneProcess currentProcess;
        readonly IDictionary<MainSceneStep, MSceneProcess> Dic = new Dictionary<MainSceneStep, MSceneProcess>();

        public MSceneProcess GetProcess(MainSceneStep step)
        {
            return Dic[step];
        }

        public void Clear()
        {
            lastProcess = null;
            currentProcess = null;
            Dic.Clear();
        }

        public void Add(MainSceneStep step, MSceneProcess _process)
        {
            DicAdd<MainSceneStep, MSceneProcess>.Add(Dic, step, _process);
        }

        public void ProcessNagare()
        {
            currentProcess?.LocalUpdate();
        }

        public bool ChangeProcess(MainSceneStep sceneStep)
        {
            return ChangeProcess<Any>(sceneStep, null);
        }
        
        public bool ChangeProcess<T>(MainSceneStep sceneStep, T t)
        {
            if (currentProcess != null)
            {
                if (!currentProcess.CanEnterOtherProcess())
                {
                    return false;
                }
                
                currentProcess.ProcessEnd();
                var Log_pre = new MainSceneLog()
                {
                    step = currentProcess.Step,
                    description = "end"
                };
                MainSceneLogger.Logs.Add(Log_pre);
            }
            
            lastProcess = currentProcess;
            Dic.TryGetValue(sceneStep, out currentProcess);
            if (currentProcess != null)
            {
                if (t != null)
                    currentProcess.ProcessEnter(t);
                else
                    currentProcess.ProcessEnter();
                var Log_new = new MainSceneLog()
                {
                    step = currentProcess.Step,
                    description = "start"
                };
                MainSceneLogger.Logs.Add(Log_new);
            }
            else
            {
                Debug.Log("empty state key:" + sceneStep);
            }
            return true;
        }
    }
}