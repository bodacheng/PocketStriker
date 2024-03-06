using UnityEngine;
using dataAccess;

namespace mainMenu
{
    public partial class TheNineSlot : MonoBehaviour
    {
        public void ReadANineAndTwo(string unitInstanceId)
        {
            ForceClearAll();
            
            if (unitInstanceId == null)
            {
                Debug.Log("严重错误");
                return;
            }
            var equipments = Stones.GetEquippingStones(unitInstanceId);
            for (var i = 1; i <= 9; i++)
            {
                AllSlot[i - 1]._cell.RemoveToTemp();
            }

            for (var i = 0; i < equipments.Count; i++)
            {
                var usingPosInt = int.Parse(equipments[i].slot);
                if (equipments[i].InstanceId != null)
                {
                    AllSlot[usingPosInt - 1].TakeASkillStoneFromBoxToSlot(equipments[i].InstanceId);
                }
                AllSlot[usingPosInt - 1]._cell.UpdateMyItem();
            }
            NineSlotsStatusRefresh();
        }
        
        // 撤销编辑
        public void ResetNineSlot()
        {
            ReadANineAndTwo(PreScene.target.Focusing.id);
            ValidateWarn();
        }
    }
}