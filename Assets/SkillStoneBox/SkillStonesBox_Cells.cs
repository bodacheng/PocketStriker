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
        readonly List<StoneCell> _cells = new List<StoneCell>();
        int _extraCellNum;
        int _activeCellCount;

        public void SetBoxHeight(float sizeNeedToRemain)
        {
            var gridLayoutGroup = BoxRoot != null ? BoxRoot.GetComponent<GridLayoutGroup>() : null;
            gridLayoutGroup ??= grid;
            var stoneBoxRect = transform.GetComponent<RectTransform>();
            var temp = PosCal.CanvasHeight - sizeNeedToRemain;
            temp =  (gridLayoutGroup.cellSize.y + gridLayoutGroup.spacing.y) *
                    Mathf.Floor(temp / (gridLayoutGroup.cellSize.y + gridLayoutGroup.spacing.y)) - gridLayoutGroup.spacing.y;
            stoneBoxRect.sizeDelta = new Vector2(stoneBoxRect.sizeDelta.x, temp);
        }

        public void GenerateCells(int extraCellNum = 0)
        {
            _extraCellNum = Mathf.Max(0, extraCellNum);
            foreach (var cell in _cells)
            {
                if (cell == null)
                    continue;
                cell.RemoveToTemp();
                cell.gameObject.SetActive(false);
            }

            var poolCellCount = CalculateRequiredCellCount(BoxLength());
            EnsureCellPoolSize(poolCellCount);
            var viewPortHeight = PosCal.AdjustedViewPortHeight(scrollRect.GetComponent<RectTransform>().rect.height, grid.cellSize.y, grid.spacing.y);
            viewPortHeight += 20;// 因为有的格子有角色使用图标，所以留出一些空间。这是个主观数值，和那个角色图标的尺寸有关
            scrollRect.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewPortHeight);

            UpdateVisibleCells(_currentStoneInstanceIds.Count);
        }

        StoneCell GetOrCreateCell(int index)
        {
            if (index < _cells.Count)
            {
                if (_cells[index] == null)
                {
                    _cells[index] = Instantiate(cellPrefab);
                    _cells[index].cellPhase = StoneCell.CellPhase.SkillStoneBoxCell;
                }
                return _cells[index];
            }

            var cell = Instantiate(cellPrefab);
            cell.cellPhase = StoneCell.CellPhase.SkillStoneBoxCell;
            _cells.Add(cell);
            return cell;
        }

        int CalculateRequiredCellCount(int stoneCount)
        {
            var need = Mathf.Max(stoneCount, 0) + _extraCellNum;
            need = Mathf.Max(need, grid.constraintCount);
            return Mathf.CeilToInt((float)need / grid.constraintCount) * grid.constraintCount;
        }

        void EnsureCellPoolSize(int requiredCount)
        {
            for (var i = 0; i < requiredCount; i++)
            {
                var cell = GetOrCreateCell(i);
                SetupCellTransform(cell);
            }
        }

        void SetupCellTransform(StoneCell cell)
        {
            if (cell == null)
                return;
            var boxRoot = BoxRoot;
            if (boxRoot == null)
                return;
            if (cell.transform.parent != boxRoot)
            {
                cell.transform.SetParent(boxRoot);
                cell.transform.localPosition = Vector3.zero;
                cell.transform.localScale = Vector3.one;
            }
        }

        void UpdateVisibleCells(int targetStoneCount)
        {
            var requiredCellCount = CalculateRequiredCellCount(targetStoneCount);
            EnsureCellPoolSize(requiredCellCount);
            _activeCellCount = requiredCellCount;

            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (cell == null)
                    continue;

                var shouldBeActive = i < requiredCellCount;
                if (shouldBeActive)
                {
                    cell.gameObject.SetActive(true);
                    cell._selected.SetActive(false);
                    cell.UpdateMyItem();
                }
                else
                {
                    cell.RemoveToTemp();
                    cell.gameObject.SetActive(false);
                }
            }

            UpdateGridHeight(requiredCellCount);
        }

        void UpdateGridHeight(int cellCount)
        {
            var rows = Mathf.CeilToInt((float)cellCount / grid.constraintCount);
            var rowHeight = grid.cellSize.y + grid.spacing.y;
            var height = rows * rowHeight - grid.spacing.y;
            var gridRect = grid.GetComponent<RectTransform>();
            gridRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        public void AddFeatureToCells(Action<StoneCell> action)
        {
            foreach (var cell in _cells)
            {
                if (cell == null)
                    continue;
                cell.btn.ClearAllEvents();
                action.Invoke(cell);
            }
        }

        StoneCell GetFirstEmptyCell()
        {
            foreach (var cell in _cells)
            {
                if (cell == null)
                    continue;
                if (!cell.gameObject.activeSelf)
                    continue;
                if (cell.GetItem() != null)
                    continue;
                return cell;
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
                    SVCenter.PlayDropSe();
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

        /// <summary>
        /// 将技能石从九宫槽卸载回背包，自动切换到对应EX页并放入第一个空格子，同时滚动到该格子。
        /// </summary>
        public void PlaceStoneFromSlot(SKStoneItem item)
        {
            if (item == null)
                return;

            item._using = false;
            item.transform.SetParent(PreScene.target.stonesTempContainer);

            var targetEx = item._SkillConfig.SP_LEVEL;
            if (FocusingExType != targetEx)
            {
                PressTab(targetEx);
            }
            else
            {
                RefreshVisibleStones(true);
            }

            // 重新排布并定位格子
            UpdateVisibleCells(_currentStoneInstanceIds.Count);
            RefreshVisibleStones(false);

            var targetCell = FindCellByInstanceId(item.instanceId) ?? GetFirstEmptyCell();
            if (targetCell != null)
            {
                // 如果刷新时未放入，则补充
                if (targetCell.GetItem() == null)
                {
                    targetCell.AddItem(item);
                }
                SVCenter.PlayDropSe();
                ScrollToCell(targetCell);
            }
            else
            {
                Debug.LogWarning("No empty cell found when placing stone from slot.");
            }
        }

        StoneCell FindCellByInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
            foreach (var cell in _cells)
            {
                if (cell == null || !cell.gameObject.activeSelf)
                    continue;
                var item = cell.GetItem();
                if (item != null && item.instanceId == instanceId)
                    return cell;
            }
            return null;
        }

        void ScrollToCell(StoneCell cell)
        {
            if (cell == null || scrollRect == null)
                return;
            var contentRect = grid.GetComponent<RectTransform>();
            var viewportRect = scrollRect.viewport != null ? scrollRect.viewport.rect : scrollRect.GetComponent<RectTransform>().rect;
            var contentHeight = contentRect.rect.height;
            var viewHeight = viewportRect.height;
            if (viewHeight <= 0)
                return;
            if (contentHeight <= viewHeight)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            var index = _cells.IndexOf(cell);
            if (index < 0)
                return;
            var row = index / grid.constraintCount;
            var cellHeight = grid.cellSize.y + grid.spacing.y;
            var targetY = row * cellHeight;
            var normalized = 1f - Mathf.Clamp01(targetY / (contentHeight - viewHeight));
            scrollRect.verticalNormalizedPosition = normalized;
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
