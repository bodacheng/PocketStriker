using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using Singleton;
using UnityEngine;
using UnityEngine.UI;

namespace ModelView
{
    public partial class DedicatedCameraConnector : MonoBehaviour
    {
        [SerializeField] private Text unitName;
        [SerializeField] private Vector3 modelPos = new Vector3(999,0,0);
        [SerializeField] private float directionY = -30;
        
        readonly IDictionary<string, Data_Center> _saves = new Dictionary<string, Data_Center>();

        public void ClearBackUpModels()
        {
            foreach (var save in _saves)
            {
                if (save.Value != null)
                    Destroy(save.Value.WholeT.gameObject);
            }
            _saves.Clear();
        }
        
        public async UniTask ShowMyModel(string instanceID)
        {
            var info = dataAccess.Units.Get(instanceID);
            await ShowModel(info?.r_id);
        }
        
        Data_Center _focusingC;
        public Data_Center FocusingC => _focusingC;
        
        string _focusRId;
        bool IfShowingSkill { get; set; } = false;

        private readonly SingleThreadProcessor _singleThreadProcessor = new SingleThreadProcessor();
        int _modelDisplayVersion;

        void OnDisable()
        {
            // A queued/in-flight selection must not resume after its UI closes,
            // especially by closing the new battle-loading ProgressLayer.
            _modelDisplayVersion++;
        }

        public int TaskRunningCount => _singleThreadProcessor.TaskRunningCount;
        public async UniTask ShowModel(string recordID)
        {
            var displayVersion = _modelDisplayVersion;
            await _singleThreadProcessor.RunAsQueued(() => _ShowModel(recordID, displayVersion));
        }
        
        async UniTask _ShowModel(string recordID, int displayVersion)
        {
            bool IsCurrent() => this != null && isActiveAndEnabled && displayVersion == _modelDisplayVersion;
            if (!IsCurrent()) return;

            foreach (var save in _saves)
            {
                if (save.Value != null)
                    save.Value.WholeT.gameObject.SetActive(false);
            }
            
            Data_Center saveData = null;
            if (recordID != null)
                _saves.TryGetValue(recordID, out saveData);

            var config = Units.GetUnitConfig(recordID);
            unitName.text = Translate.Get(config?.REAL_NAME);
            
            if (recordID == null)
            {
                _focusingC = null;
                return;
            }
            if (saveData != null)
            {
                _focusingC = saveData;
            }
            else
            {
                ProgressLayer.Loading(string.Empty);
                saveData = await GeneralModelPool.GetModel(recordID, transform, modelPos- new Vector3(0,0, 200));
                if (!IsCurrent())
                {
                    if (saveData != null && saveData.WholeT != null)
                        Destroy(saveData.WholeT.gameObject);
                    return;
                }
                _focusingC = saveData;
                if (_saves.TryGetValue(recordID, out var oldModel))
                {
                    if (oldModel != null)
                    {
                        Destroy(oldModel.WholeT.gameObject);
                    }
                }
                
                ProgressLayer.Close();
                if (_focusingC == null)
                {
                    _focusRId = null;
                    return;
                }
                DicAdd<string, Data_Center>.Add(_saves, recordID, _focusingC);
            }

            if (!IsCurrent())
            {
                return; // 上方的await后layer可能已经被销毁
            }
            
            _focusingC.WholeT.SetParent(transform); // 确保模型总与图层一起被摧毁
            _focusingC.WholeT.position = modelPos;
            
            _focusRId = recordID;
            _focusingC.AnimationManger.AnimatorRef.applyRootMotion = true;
            
            // 这个短暂变色是为了掩盖一些模型刚加载瞬间有些渲染没到位的尴尬。比如裙子摇晃 
            _focusingC._ShaderManager.FlatColorForAShortTime(Color.black, 0f, 1f);
            _focusingC.WholeT.gameObject.SetActive(true);
            
            await UniTask.DelayFrame(5);// 否则Unity对mesh的尺寸计算有错误。算是Unity的bug
            if (!IsCurrent())
            {
                return;
            }
            if (_focusingC != null && _focusingC.WholeT != null)
            {
                foreach (var save in _saves)
                {
                    if (save.Value != null && save.Value != _focusingC)
                        save.Value.WholeT.gameObject.SetActive(false);
                }
                Initialize(false,_focusingC.WholeT.gameObject.transform, transform);
                _focusingC.AnimationManger.CasualFace();
                ItemDetailStartDirection(directionY,0,0);
            }
        }
        
        public async UniTask PrepareModel(string recordID)
        {
            var displayVersion = _modelDisplayVersion;
            // The 2D lobby intentionally warms up an inactive 3D connector.
            // A later disable/close still invalidates an already-running warmup.
            bool IsCurrent() => this != null && displayVersion == _modelDisplayVersion;
            if (!IsCurrent()) return;

            Data_Center saveData = null;
            if (recordID != null)
                _saves.TryGetValue(recordID, out saveData);

            var config = Units.GetUnitConfig(recordID);
            if (config == null)
            {
                return;
            }
            if (saveData == null)
            {
                saveData = await GeneralModelPool.GetModel(recordID);
                if (saveData == null)
                {
                    return;
                }
                if (!IsCurrent())
                {
                    if (saveData.WholeT != null)
                    {
                        Destroy(saveData.WholeT.gameObject);
                    }
                    return;
                }
                saveData.WholeT.SetParent(transform);
                saveData.WholeT.position = modelPos;
                saveData.WholeT.gameObject.SetActive(false);
                DicAdd<string, Data_Center>.Add(_saves, recordID, saveData);
            }
        }
        
        public async UniTask SkillShowRunWithPrepare(string skillName, bool waitLastMotionEnd = true)
        {
            if (IfShowingSkill && waitLastMotionEnd)
                return;

            await HurtObjectManager.ConstructDPool();
            
            var unitConfig = Units.GetUnitConfig(_focusRId);
            if (unitConfig == null)
                return;
            
            if (_focusingC.AnimationManger != null)
            {
                await _focusingC.AnimationManger.PreloadPersonalAnimResourceMode(unitConfig.TYPE, skillName, unitConfig.element, 1);
                if (_focusingC == null)
                {
                    return;
                }
                IfShowingSkill = true;
                _focusingC.AnimationManger.AnimationTrigger(skillName, 0.25f);
                _focusingC.AnimationManger.TriggerExpression(Facial.aggressive);
            }
        }

        private TweenerCore<Vector3, Vector3, VectorOptions> resetModelPosTween;
        void SkillsPrintOutLateUpdate()
        {
            if (_focusingC != null && _focusingC.AnimationManger != null && _focusingC.WholeT.gameObject.activeSelf)
            {
                if (_focusingC.AnimationManger.GetBool("in_transition") == false && 
                    _focusingC.AnimationManger.GetCurrentAnimatorStateInfo(1).normalizedTime >= 1f)
                {
                    _focusingC.AnimationManger.AnimationTrigger(string.Empty, 0.25f);
                    IfShowingSkill = false;
                    resetModelPosTween?.Kill();
                    resetModelPosTween = _focusingC.WholeT.transform.DOMove(modelPos, 1)
                        .SetLink(_focusingC.WholeT.gameObject);
                }
            }
        }
    }
}
