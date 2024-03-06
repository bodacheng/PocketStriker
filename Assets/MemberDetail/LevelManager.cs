//using UnityEngine;
//using UnityEngine.UI;
//using dataAccess;
//using Api.Dto.Model;

//// 对这个模块来说，只要总是能锁定到要升级的角色就可以了。一个号码就可以。
//namespace mainMenu
//{
//    public class LevelManager : MonoBehaviour
//    {
//        public PreScene preparingScene;

//        public Button plusEX;
//        public Button minusEX;
//        public Button confirmEX;

//        public Text levelText;
//        public Slider EXbar;

//        int focusingCharCurrentEx;
//        int focusingCharCurrentLevel;

//        public void turnOnUI(bool _on)
//        {
//            plusEX.gameObject.SetActive(_on);
//            minusEX.gameObject.SetActive(_on);
//            confirmEX.gameObject.SetActive(_on);
//            levelText.gameObject.SetActive(_on);
//            EXbar.gameObject.SetActive(_on);
//        }

//        public void exButtonFeaturesIni(GetMonsterOfPlayerDetailModel focusingCharacterDataInfo, float canDistributeExAmount)
//        {
//            focusingCharCurrentEx = focusingCharacterDataInfo.experience;
//            EXbar.value = levelExpDefine.percentOfCurrentExpOfLevel(focusingCharCurrentEx);
//            levelText.text = levelExpDefine.levelOfExp(focusingCharCurrentEx).ToString();
//            minusEX.gameObject.SetActive(false);

//            UnityEngine.Events.UnityAction plusEXfeature = () =>
//            {
//                if (canDistributeExAmount > 0)
//                {
//                    canDistributeExAmount -= 1;
//                    focusingCharCurrentEx += 1;
//                    if (focusingCharCurrentEx > focusingCharacterDataInfo.experience)
//                    {
//                        minusEX.gameObject.SetActive(true);
//                    }
//                    else
//                    {
//                        minusEX.gameObject.SetActive(false);
//                    }
//                }
//                levelText.text = levelExpDefine.levelOfExp(focusingCharCurrentEx).ToString();
//                EXbar.value = levelExpDefine.percentOfCurrentExpOfLevel(focusingCharCurrentEx);
//            };
//            plusEX.onClick.RemoveAllListeners();
//            plusEX.onClick.AddListener(plusEXfeature);

//            UnityEngine.Events.UnityAction minusEXfeature = () =>
//            {
//                canDistributeExAmount += 1;
//                focusingCharCurrentEx -= 1;
//                if (focusingCharCurrentEx > focusingCharacterDataInfo.experience)
//                {
//                    minusEX.gameObject.SetActive(true);
//                }
//                else
//                {
//                    minusEX.gameObject.SetActive(false);
//                }
//                levelText.text = levelExpDefine.levelOfExp(focusingCharCurrentEx).ToString();
//                EXbar.value = levelExpDefine.percentOfCurrentExpOfLevel(focusingCharCurrentEx);
//            };
//            minusEX.onClick.RemoveAllListeners();
//            minusEX.onClick.AddListener(minusEXfeature);

//            UnityEngine.Events.UnityAction ConfirmlevelUp = () =>
//             {
//                 preparingScene.mainProcessRunner.TriggerMainProcess
//                     (AccountCharsSet.instance.PlusExpForAccountChar(focusingCharacterDataInfo.monsterOfPlayerId, focusingCharacterDataInfo.experience));
//             };
//            UnityEngine.Events.UnityAction levelUpCancel = () =>
//             {
//                 focusingCharCurrentEx = focusingCharacterDataInfo.experience;
//                 EXbar.value = levelExpDefine.percentOfCurrentExpOfLevel(focusingCharCurrentEx);
//                 levelText.text = levelExpDefine.levelOfExp(focusingCharCurrentEx).ToString();
//                 minusEX.gameObject.SetActive(false);
//             };

//            UnityEngine.Events.UnityAction Confirmfeature = () =>
//            {
//                LoadingCanvas.target.ArrangeValiationWindow(ConfirmlevelUp, levelUpCancel, "升级？");
//            };
//            confirmEX.onClick.RemoveAllListeners();
//            confirmEX.onClick.AddListener(Confirmfeature);
//        }
//    }
//}