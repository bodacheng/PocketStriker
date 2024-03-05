using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class BoundaryControlByGod : MonoBehaviour {
    
    [SerializeField] List<ParticleSystem> BattleRingPSs;
    [SerializeField] float BattleRingRadius = 20f;
    
    ParticleSystem BattleRingPS;
    GameObject battleGround;
    public static float _BattleRingRadius;
    public static BoundaryControlByGod target;
    
    void Awake()
    {
        target = this;
        _BattleRingRadius = BattleRingRadius;
    }
    
    void Start()
    {
        int choose = Random.Range(0, BattleRingPSs.Count);
        for (int i = 0; i < BattleRingPSs.Count; i++)
        {
            if (i == choose)
            {
                BattleRingPS = BattleRingPSs[i];
                BattleRingPS.gameObject.SetActive(true);
            }else{
                BattleRingPSs[i].gameObject.SetActive(false);
            }
        }
    }
    
    private readonly int _currentBackGroundNum = -1;
    public async UniTask ChangeBackGround(int number)
    {
        if (_currentBackGroundNum != number)
        {
            if (battleGround != null)
                Destroy(this.battleGround);
            
            battleGround = await AddressablesLogic.LoadObject("battleGround/" +number);
            if (battleGround != null)
            {
                battleGround.GetComponent<BattleGround>().Set();
            }
        }
    }
}

//public void SUOQUANER(int aliveMemberCount)
//{
//    float targetBattleGroundRingRadius = 30;
//    switch (aliveMemberCount)
//    {
//        case 7:
//            targetBattleGroundRingRadius = 20;
//            break;
//        case 6:
//            targetBattleGroundRingRadius = 15;
//            break;
//        case 5:
//            targetBattleGroundRingRadius = 10;
//            break;
//        case 4:
//            targetBattleGroundRingRadius = 7;
//            break;
//        case 3:
//            targetBattleGroundRingRadius = 7;
//            break;
//        case 2:
//            break;
//        default:
//            break;
//    }
//    ChangeMagicRingRadius(targetBattleGroundRingRadius);
//}

//public IDictionary<Team, List<Data_Center>> AllMembers;//双方队伍人员字典，和netfightscene模块里同名变量统一。
//float distanceFromCharToCenter;
//public void RoundBattleFieldNormalControl(Vector3 battleRingCenter)
//{
//    if (AllMembers == null)
//        return;
//    foreach (KeyValuePair<Team, List<Data_Center>> pair in AllMembers)
//    {
//        foreach (Data_Center oneBoy in pair.Value)
//        {
//            if (!oneBoy.IsDead.Value)
//            {
//                battleRingCenter.y = oneBoy.WholeT.position.y;
//                distanceFromCharToCenter = (oneBoy.WholeT.position - battleRingCenter).magnitude;
//                if (distanceFromCharToCenter > BattleRingRadius)
//                {
//                    oneBoy._BasicPhysicSupport.hiddenMethods.onBattleGroundBundary = true;
//                    oneBoy.WholeT.position = Vector3.Lerp(oneBoy.WholeT.position, battleRingCenter,Time.deltaTime); //Vector3.Lerp(oneBoy.WholeT.position, battleRingCenter,Time.deltaTime * (distanceFromCharToCenter - BattleRingRadius) * 0.4f);
//                    oneBoy._BasicPhysicSupport.hiddenMethods.antiWallDirection = battleRingCenter - oneBoy.WholeT.position;
//                }
//                else
//                {
//                    oneBoy._BasicPhysicSupport.hiddenMethods.onBattleGroundBundary = false;
//                }
//            }
//            else
//            {
//                oneBoy._BasicPhysicSupport.hiddenMethods.onBattleGroundBundary = false;
//            }
//        }
//    }
//}
