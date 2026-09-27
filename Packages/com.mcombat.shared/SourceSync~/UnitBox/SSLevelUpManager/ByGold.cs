using UnityEngine;
using dataAccess;

// 智慧果实消耗
// public partial class SSLevelUpManager : MonoBehaviour
// {
//     int CurrentGoldExaust;
//     public void PlusTargetLevel()
//     {
//         StoneOfPlayerInfo StoneInfoModel = Stones.Get(targetInstanceId);
//         if (StoneInfoModel == null)
//             return;
//         
//         LevelExpConfig.Current current = LevelExpConfig.GetCurrentInfo(CurrentAddExp() + StoneInfoModel.EXP);
//         // +号代表直接把技能石升到下一级所需要的经验全数补充上，不够的话就把当前所有剩余的金币加上
//         if (StoneExpManager.GoldToExp(Currencies.CoinCount) >= current.expToNextLevel)
//         {
//             CurrentGoldExaust += StoneExpManager.ExpToGold(current.expToNextLevel);
//         }
//         else
//         {
//             CurrentGoldExaust += Currencies.CoinCount;
//         }
//         RefreshSkillLevelUpModule();
//     }
//     
//     public void MinusTargetLevel()
//     {
//         StoneOfPlayerInfo StoneInfoModel = Stones.Get(targetInstanceId);
//         if (StoneInfoModel == null)
//         {
//             RefreshSkillLevelUpModule();
//             return;
//         }
//         
//         LevelExpConfig.Current current = LevelExpConfig.GetCurrentInfo(CurrentAddExp() + StoneInfoModel.EXP);
//         
//         if (current.currentLevel == 1)
//         {
//             CurrentGoldExaust = 0;
//             RefreshSkillLevelUpModule();
//             return;
//         }
//         
//         if (current.expRemain > 0)
//         {
//             if (CurrentGoldExaust >= StoneExpManager.ExpToGold(current.expRemain))
//             {
//                 CurrentGoldExaust -= StoneExpManager.ExpToGold(current.expRemain);
//             }
//             else
//             {
//                 CurrentGoldExaust = 0;
//             }
//         }
//         else{
//             if (CurrentGoldExaust >= StoneExpManager.ExpToGold(LevelExpConfig.GetLevelExp(current.currentLevel)))
//             {
//                 CurrentGoldExaust -= StoneExpManager.ExpToGold(LevelExpConfig.GetLevelExp(current.currentLevel));
//             }else{
//                 CurrentGoldExaust = 0; 
//             }
//         }
//         RefreshSkillLevelUpModule();
//     }
// }
