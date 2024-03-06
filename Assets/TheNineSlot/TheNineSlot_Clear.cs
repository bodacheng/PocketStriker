using UnityEngine;
using dataAccess;
using DummyLayerSystem;

namespace mainMenu
{
    public partial class TheNineSlot : MonoBehaviour
    {
        // 强制清空九宫槽 包括固有技能，用于程序处理
        void ForceClearAll()
        {
            foreach (var _slot in AllSlot)
            {
                if (_slot != null && _slot._cell != null)
                    _slot._cell.RemoveToTemp();
            }
        }
        
        // 清空角色技能编辑
        public void ClearSkillEquip()
        {
            if (PreScene.target.Focusing == null)
                return;

            var info = PreScene.target.Focusing;
            var originSkillInfo = Stones.GetOriginSkillOfUnit(info.id);
            foreach (var _slot in AllSlot)
            {
                var sK = _slot._cell.GetItem();
                if (sK == null)
                {
                    continue;
                }
                if (originSkillInfo == null || (originSkillInfo != null && (sK.instanceId != originSkillInfo.InstanceId)))
                {
                    _slot._cell.RemoveToTemp();
                }
            }
            NineSlotsStatusRefresh();
            var skillEdit = UILayerLoader.Get<SkillEditLayer>();
            skillEdit.stonesBox.RestFilter();
        }
    }
}
