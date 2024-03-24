using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using dataAccess;

namespace mainMenu
{
    public partial class SkillStonesBox : MonoBehaviour
    {
        [Header("格子")]
        [SerializeField] StoneCell cellPrefab;
        
        [Header("选中框")]
        [SerializeField] GameObject selectedFrame;

        [SerializeField] GridLayoutGroup grid;
        [SerializeField] ScrollRect scrollRect;
        public GridLayoutGroup Grid=> grid;
        public ScrollRect ScrollRect => scrollRect;
        public static GameObject Selected;
        readonly IDictionary<int, StoneCell> _cellsDic = new Dictionary<int, StoneCell>();

        public void SetBoxHeight(float sizeNeedToRemain)
        {
            var stoneBoxRect = transform.GetComponent<RectTransform>();
            var temp = PosCal.CanvasHeight - sizeNeedToRemain;
            temp =  (grid.cellSize.y + grid.spacing.y) * 
                    Mathf.Floor(temp / (grid.cellSize.y + grid.spacing.y));
            stoneBoxRect.sizeDelta = new Vector2(stoneBoxRect.sizeDelta.x, temp);
        }
        
        public void GenerateCells(int extraCellNum = 0)
        {
            foreach (var kv in _cellsDic)
            {
                kv.Value.gameObject.SetActive(false);
            }
            
            var hang = 1;
            var cellCount = BoxLength();
            cellCount += extraCellNum;
            cellCount = ((cellCount / grid.constraintCount) + 1) * grid.constraintCount;
            for (int i = 0; i < cellCount; i++)
            {
                if (!_cellsDic.ContainsKey(i))
                {
                    var cell = Instantiate(cellPrefab);
                    cell.cellPhase = StoneCell.CellPhase.SkillStoneBoxCell;
                    _cellsDic.Add(i, cell);
                }
                
                //CellsDictionary[i].RemoveItemWithOutDestroy();//根据之前经验，这个东西有出错的可能
                _cellsDic[i].gameObject.SetActive(true);
                if (_cellsDic[i].transform.parent != grid.transform)
                {
                    _cellsDic[i].transform.SetParent(grid.transform);
                    _cellsDic[i].transform.localPosition = Vector3.zero;
                    _cellsDic[i].transform.localScale = Vector3.one;
                }
                
                _cellsDic[i]._selected.SetActive(false);
            }
            
            hang = cellCount / grid.constraintCount + 1;
            
            var viewPortHeight = PosCal.AdjustedViewPortHeight(scrollRect.GetComponent<RectTransform>().rect.height, grid.cellSize.y, grid.spacing.y);
            viewPortHeight += 20;// 因为有的格子有角色使用图标，所以留出一些空间。这是个主观数值，和那个角色图标的尺寸有关
            scrollRect.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewPortHeight);
            
            var gridRect = grid.GetComponent<RectTransform>();
            gridRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, (grid.cellSize.y + grid.spacing.y) * hang);
        }
        
        public void AddFeatureToCells(Action<StoneCell> action)
        {
            foreach (var cell in _cellsDic)
            {
                cell.Value.btn.ClearAllEvents();
                action.Invoke(cell.Value);
            }
        }
        
        StoneCell GetFirstEmptyCell()
        {
            foreach (KeyValuePair<int, StoneCell> keyValuePair in _cellsDic)
            {
                if (keyValuePair.Value.GetItem() != null)
                    continue;
                return keyValuePair.Value;
            }
            return null;
        }

        public void ReturnStoneToBox(SKStoneItem item)
        {
            if (item._SkillConfig.SP_LEVEL == FocusingExType)
            {
                var dragAndDropCell = GetFirstEmptyCell();
                if (dragAndDropCell != null)
                {
                    dragAndDropCell.AddItem(item);
                }
                else
                {
                    Debug.Log("走到这儿的话说明已经是bug了。");
                    RemoveToTemp(item);
                }
            }
            else{
                //如果尝试归还背包的技能石必杀等级与显示中的不一致，则直接使其非显示。
                RemoveToTemp(item);
            }
        }
        
        void RemoveToTemp(SKStoneItem item)
        {
            item._using = false;
            item.gameObject.transform.SetParent(PreScene.target.stonesTempContainer);
        }

        static int BoxLength()
        {
            var returnValue = 0;
            var C_Types = Units.GetTypeList();
            for (var i = 0; i < C_Types.Count; i++)
            {
                var filterForm0 = new StoneFilterForm
                {
                    Type = C_Types[i],
                    ExType = new[] { 0 },
                };
                var filterForm1 = new StoneFilterForm
                {
                    Type = C_Types[i],
                    ExType = new[] { 1 },
                };
                var filterForm2 = new StoneFilterForm
                {
                    Type = C_Types[i],
                    ExType = new[] { 2 },
                };
                var filterForm3 = new StoneFilterForm
                {
                    Type = C_Types[i],
                    ExType = new[] { 3 },
                };
                
                var skillStonesOfTypeNormal = Stones.TargetStonesFromAccount(filterForm0, null);
                var skillStonesOfTypeEx1 = Stones.TargetStonesFromAccount(filterForm1, null);
                var skillStonesOfTypeEx2 = Stones.TargetStonesFromAccount(filterForm2, null);
                var skillStonesOfTypeEx3 = Stones.TargetStonesFromAccount(filterForm3, null);
                
                returnValue = Mathf.Max(returnValue, skillStonesOfTypeNormal.Count, skillStonesOfTypeEx1.Count, skillStonesOfTypeEx2.Count, skillStonesOfTypeEx3.Count);
            }
            return returnValue;
        }
    }
}

