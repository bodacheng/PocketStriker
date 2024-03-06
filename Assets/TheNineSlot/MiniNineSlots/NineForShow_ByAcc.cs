using UnityEngine;
using dataAccess;
using Cysharp.Threading.Tasks;
using mainMenu;

public partial class NineForShow : MonoBehaviour
{
    private string instanceID;
    public string InstanceID => instanceID;
    public void ShowStones_Acc(string instanceID)
    {
        this.instanceID = instanceID;
        var skillStoneOfPlayerInfoModels = Stones.GetEquippingStones(this.instanceID);
        
        string A1SkillID = null, A2SkillID = null, A3SkillID = null;
        string B1SkillID = null, B2SkillID = null, B3SkillID = null;
        string C1SkillID = null, C2SkillID = null, C3SkillID = null;
        
        for (var i = 0; i < skillStoneOfPlayerInfoModels.Count; i++)
        {
            switch(skillStoneOfPlayerInfoModels[i].slot)
            {
                case "1":
                    A1SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "2":
                    A2SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "3":
                    A3SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "4":
                    B1SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "5":
                    B2SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "6":
                    B3SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "7":
                    C1SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "8":
                    C2SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
                case "9":
                    C3SkillID = skillStoneOfPlayerInfoModels[i].SkillId;
                break;
            }
        }

        UniTask.WhenAll(        
            ShowStones(
                A1SkillID, A2SkillID, A3SkillID,
                B1SkillID, B2SkillID, B3SkillID,
                C1SkillID, C2SkillID, C3SkillID
            ),
            SkillSetStateRender(
                PreScene.target.postProcessCamera,
                A1SkillID, A2SkillID, A3SkillID,
                B1SkillID, B2SkillID, B3SkillID,
                C1SkillID, C2SkillID, C3SkillID,
                false, true
            )
        ).Forget();
    }
}
