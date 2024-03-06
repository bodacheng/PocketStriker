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
        
        static readonly IDictionary<string, Data_Center> Saves = new Dictionary<string, Data_Center>();

        public static void ClearBackUpModels()
        {
            foreach (var save in Saves)
            {
                if (save.Value != null)
                    Destroy(save.Value.WholeT.gameObject);
            }
            Saves.Clear();
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
        public int TaskRunningCount => _singleThreadProcessor.TaskRunningCount;
        public async UniTask ShowModel(string recordID)
        {
            await _singleThreadProcessor.RunAsQueued(_ShowModel(recordID));
        }
        
        async UniTask _ShowModel(string recordID)
        {
            foreach (var save in Saves)
            {
                if (save.Value != null)
                    save.Value.WholeT.gameObject.SetActive(false);
            }
            
            Data_Center saveData = null;
            if (recordID != null)
                Saves.TryGetValue(recordID, out saveData);

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
                _focusingC = await GeneralModelPool.GetModel(recordID, transform, modelPos- new Vector3(0,0, 200));
                await UniTask.DelayFrame(2);
                if (Saves.ContainsKey(recordID))
                {
                    var oldModel = Saves[recordID];
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
                DicAdd<string, Data_Center>.Add(Saves, recordID, _focusingC);
            }

            if (this == null)
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
            if (this == null)
            {
                return;
            }
            if (_focusingC != null && _focusingC.WholeT != null)
            {
                foreach (var save in Saves)
                {
                    if (save.Value != null && save.Value != _focusingC)
                        save.Value.WholeT.gameObject.SetActive(false);
                }
                Initialize(false,_focusingC.WholeT.gameObject.transform, transform);
                _focusingC.AnimationManger.CasualFace();
                ItemDetailStartDirection(0,0,0);
            }
        }
        
        public static async UniTask PrepareModel(string recordID)
        {
            Data_Center saveData = null;
            if (recordID != null)
                Saves.TryGetValue(recordID, out saveData);

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
                saveData.WholeT.gameObject.SetActive(false);
                DicAdd<string, Data_Center>.Add(Saves, recordID, saveData);
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
                _focusingC.AnimationManger.AnimationTrigger(skillName, true,0.25f);
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
                    _focusingC.AnimationManger.PlayLayerAnim(null, true, 0.25f);
                    IfShowingSkill = false;
                    resetModelPosTween = _focusingC.WholeT.transform.DOMove(modelPos, 1);
                }
            }
        }
    }
}