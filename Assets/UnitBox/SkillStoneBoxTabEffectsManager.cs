using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace mainMenu
{
    public class SkillStoneBoxTabEffectsManager : MonoBehaviour
    {
        readonly IDictionary<Element, ElementStoneTagsGroup> _btnEffects = new Dictionary<Element, ElementStoneTagsGroup>();
        ElementStoneTagsGroup _focusingEffectsGroup;
        
        async UniTask StartUp(Element element, CancellationToken ct = default)
        {
            if (_btnEffects.ContainsKey(element))
                return;
            var zt = new ElementStoneTagsGroup();
            await zt.IniForSkillStoneBox(element, transform);
            ct.ThrowIfCancellationRequested();
            _btnEffects.Add(element, zt);
            ct.ThrowIfCancellationRequested();
        }

        public void TurnShowingTagEffects(bool on)
        {
            if (!on)
                _focusingEffectsGroup?.CloseTagEffects();
            else
            {
                _focusingEffectsGroup?.OpenTagEffects();
            }
        }
        
        public void CloseShowingTagEffects()
        {
            _focusingEffectsGroup?.CloseTagEffects();
            foreach (var kv in _btnEffects)
            {
                kv.Value.Clear();
            }
        }
        
        public void SetSelectedTabPos(int ex)
        {
            _focusingEffectsGroup?.SetSelectedTabPos(ex);
        }
        
        public async UniTask SwitchElement(Element element, CancellationToken ct)
        {
            ProgressLayer.Loading(string.Empty);
            await StartUp(element, ct);
            ct.ThrowIfCancellationRequested();
            _focusingEffectsGroup?.CloseTagEffects();
            if (_btnEffects.ContainsKey(element))
            {
                _focusingEffectsGroup = _btnEffects[element];
            }else{
                Debug.Log("fatal error element tags");
            }
            ProgressLayer.Close();
        }
        
        public void RefreshTagEffect(Vector3 pos, int sp_level)//按钮切换也可以在这里做文章
        {
            _focusingEffectsGroup.RefreshBoxEffects(sp_level, pos);
        }
        
        public void RefreshSlotEffect(int slotNum ,Vector3 pos, int sp_level)//按钮切换也可以在这里做文章
        {
            _focusingEffectsGroup.RefreshSlotEffects(slotNum, sp_level, pos, transform);
        }
        
        public void SkillButtonExplosion(int spLevel, Vector3 targetPos, Transform parent)
        {
            _focusingEffectsGroup.SkillButtonExplosion(spLevel, targetPos, parent);
        }
    }
}