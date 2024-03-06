// using UnityEngine;
// using mainMenu;
// using UnityEngine.UI;
// using System.Collections.Generic;
// using dataAccess;
// using UniRx;
//
// public class StoneDeleteManger : MonoBehaviour
// {
//     public Text CurrentSelectedCount;
//     public RectTransform SkillInfoT, SelectionInfoT;
//     public SkillStonesBox SkillStonesBox;
//     public Button EnterDeleteModeButton;
//     
//     [Space(7)]
//     [Header("确认，取消")]
//     public Button confirm, cancel;
//     SingleAssignmentDisposable autoHide;
//     
//     readonly List<StoneOfPlayerInfo> selectedForDelete = new List<StoneOfPlayerInfo>();
//     
//     public void EnterDeleteMode()
//     {
//         SkillStonesBox._Selected.SetActive(false);
//         foreach (KeyValuePair<int, StoneCell> KV in SkillStonesBox.CellsDic)
//         {
//             Button button = KV.Value.GetComponent<Button>();
//             button.onClick.AddListener(delegate { SelectForDelete(KV.Value); });
//         }
//         
//         SkillInfoT.gameObject.SetActive(false);
//         SelectionInfoT.gameObject.SetActive(true);
//         EnterDeleteModeButton.gameObject.SetActive(false);
//         
//         confirm.onClick.RemoveAllListeners();
//         // confirm.onClick.Add.... 删除用函数暂时未写
//         
//         autoHide = new SingleAssignmentDisposable
//         {
//             Disposable = Observable.EveryUpdate().Subscribe(_ =>
//                 {
//                     if (!confirm.gameObject.activeSelf)
//                     {
//                         if (selectedForDelete.Count > 0)
//                         {
//                             confirm.gameObject.SetActive(true);
//                             cancel.gameObject.SetActive(true);
//                         }
//                     }else{
//                         if (selectedForDelete.Count == 0)
//                         {
//                             confirm.gameObject.SetActive(false);
//                             cancel.gameObject.SetActive(false);
//                         }
//                     }
//                 }
//             )
//         };
//     }
//     
//     public void ExitDeleteMode()
//     {
//         SkillInfoT.gameObject.SetActive(true);
//         SelectionInfoT.gameObject.SetActive(false);
//         EnterDeleteModeButton.gameObject.SetActive(true);
//         CurrentSelectedCount.text = "";
//         ClearSelect();
//         
//         StoneListLayer StoneListLayer = StoneListLayer.Open();
//         SkillStonesBox.AddFeatureToCells(StoneListLayer.CellFeature_StoneShow);
//         autoHide.Dispose();
//     }
//     
//     // 按钮函数
//     public void ClearSelect()
//     {
//         CurrentSelectedCount.text = "";
//         selectedForDelete.Clear();
//         confirm.gameObject.SetActive(false);
//         cancel.gameObject.SetActive(false);
//         RefreshSelectedRender();
//     }
//     
//     // 显示正选择中的技能石
//     void RefreshSelectedRender()
//     {
//         foreach(KeyValuePair<int, StoneCell> KV in SkillStonesBox.CellsDic)
//         {
//             StoneCell cell = KV.Value;
//             if (cell.GetItem() == null)
//             {
//                 cell._selected.SetActive(false);
//                 continue;
//             }
//             StoneOfPlayerInfo skillStoneOfPlayerInfoModel = Stones.Get(cell.GetItem().instanceId);
//             if (skillStoneOfPlayerInfoModel != null)
//             {
//                 if (selectedForDelete.Contains(skillStoneOfPlayerInfoModel))
//                 {
//                     cell._selected.SetActive(true);
//                 }else{
//                     cell._selected.SetActive(false);
//                 }
//             }
//         }
//     }
//     
//     // 在集体删除技能石多选模式下单击技能石格。未选中时点击为选中，选中时点击则取消
//     void SelectForDelete(StoneCell cell)
//     {
//         if (cell.GetItem() != null)
//         {
//             StoneOfPlayerInfo StoneOInfo = Stones.Get(cell.GetItem().instanceId);
//             if (StoneOInfo != null)
//             {
//                 if (selectedForDelete.Contains(StoneOInfo))
//                 {
//                     RemoveStoneForDelete(cell);
//                 }else{
//                     SelectStoneForDelete(cell);
//                 }
//             }
//         }
//     }
//     
//     // 选择以集体删除
//     void SelectStoneForDelete(StoneCell cell)
//     {
//         if (cell.GetItem() != null)
//         {
//             StoneOfPlayerInfo skillStoneOfPlayerInfoModel = Stones.Get(cell.GetItem().instanceId);
//             if (skillStoneOfPlayerInfoModel != null)
//             {
//                 selectedForDelete.Add(skillStoneOfPlayerInfoModel);
//                 CurrentSelectedCount.text = "选中" + selectedForDelete.Count + "个技能石";
//                 cell._selected.SetActive(true);
//             }
//         }
//     }
//     
//     // 取消选择
//     void RemoveStoneForDelete(StoneCell cell)
//     {
//         if (cell.GetItem() != null)
//         {
//             StoneOfPlayerInfo skillStoneOfPlayerInfoModel = Stones.Get(cell.GetItem().instanceId);
//             if (skillStoneOfPlayerInfoModel != null)
//             {
//                 selectedForDelete.Remove(skillStoneOfPlayerInfoModel);
//                 CurrentSelectedCount.text = "选中" + selectedForDelete.Count + "个技能石";
//                 cell._selected.SetActive(false);
//             }
//         }
//     }
// }