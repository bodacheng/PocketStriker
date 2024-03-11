using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace mainMenu
{
    public class ProcessesRunner
    {
        static ProcessesRunner _instanceMain;
        public static ProcessesRunner Main
        {
            get
            {
                if (_instanceMain == null)
                {
                    _instanceMain = new ProcessesRunner();
                }
                return _instanceMain;
            }
        }
        
        public MSceneProcess lastProcess;
        public MSceneProcess currentProcess;

        public readonly ReactiveProperty<MainSceneStep> _currentStep = new ReactiveProperty<MainSceneStep>();
        public ReactiveProperty<MainSceneStep> CurrentStep => _currentStep;
        readonly IDictionary<MainSceneStep, MSceneProcess> _dic = new Dictionary<MainSceneStep, MSceneProcess>();

        public MSceneProcess GetProcess(MainSceneStep step)
        {
            return _dic[step];
        }

        public void Clear()
        {
            lastProcess = null;
            currentProcess = null;
            _dic.Clear();
        }

        public void Add(MainSceneStep step, MSceneProcess _process)
        {
            DicAdd<MainSceneStep, MSceneProcess>.Add(_dic, step, _process);
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
            _dic.TryGetValue(sceneStep, out currentProcess);
            if (currentProcess != null)
            {
                _currentStep.Value = sceneStep;
                
                if (t != null)
                    currentProcess.ProcessEnter(t);
                else
                    currentProcess.ProcessEnter();
                var logNew = new MainSceneLog()
                {
                    step = currentProcess.Step,
                    description = "start"
                };
                MainSceneLogger.Logs.Add(logNew);
            }
            else
            {
                Debug.Log("empty state key:" + sceneStep);
            }
            return true;
        }
    }
}