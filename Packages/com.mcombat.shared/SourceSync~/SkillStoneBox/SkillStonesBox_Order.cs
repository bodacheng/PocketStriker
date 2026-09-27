using System;
using System.Collections.Generic;
using UnityEngine;
using dataAccess;
using UnityEngine.UI;

namespace mainMenu
{
    public partial class SkillStonesBox : MonoBehaviour
    {
        int _orderType = 0;

        private int OrderType
        {
            get => _orderType;
            set
            {
                _orderType = value;
                if (_orderType > 2)
                {
                    _orderType = 0;
                }
            }
        }

        // 功能本身直接放按钮上，但text要适配到SkillStonesBox上。
        public void SwitchOrder()
        {
            OrderType++;
            Selected?.SetActive(false);
            RefreshVisibleStones(false);
        }

        void Order(List<string> targets)
        {
            if (targets == null || targets.Count <= 1)
            {
                UpdateOrderLabel();
                return;
            }
            switch (OrderType)
            {
                case 0: // 以技能ID
                    orderButtonText.text = "Default";
                    targets.Sort(CompareByRecordIdAscending);
                    break;
                case 1: // 等级降序
                    orderButtonText.text = "Level DES";
                    targets.Sort((a, b) => CompareByLevel(a, b, ascending:false));
                    break;
                case 2: // 等级升序
                    orderButtonText.text = "Level ASC";
                    targets.Sort((a, b) => CompareByLevel(a, b, ascending:true));
                    break;
                default:
                    OrderType = 0;
                    Order(targets);
                    return;
            }
        }

        void UpdateOrderLabel()
        {
            switch (OrderType)
            {
                case 0:
                    orderButtonText.text = "Default";
                    break;
                case 1:
                    orderButtonText.text = "Level DES";
                    break;
                case 2:
                    orderButtonText.text = "Level ASC";
                    break;
                default:
                    orderButtonText.text = "Default";
                    break;
            }
        }

        int CompareByRecordIdAscending(string first, string second)
        {
            var left = Stones.Get(first);
            var right = Stones.Get(second);
            return CompareByRecordIdAscending(left, right);
        }

        int CompareByLevel(string first, string second, bool ascending)
        {
            var left = Stones.Get(first);
            var right = Stones.Get(second);
            var nullCompare = CompareNull(left, right);
            if (nullCompare != 0)
                return nullCompare;

            var result = left.Level.CompareTo(right.Level);
            if (result == 0)
                result = CompareByRecordIdAscending(left, right);
            return ascending ? result : -result;
        }

        int CompareByRecordIdAscending(StoneOfPlayerInfo left, StoneOfPlayerInfo right)
        {
            var nullCompare = CompareNull(left, right);
            if (nullCompare != 0)
                return nullCompare;

            var leftId = left.SkillId;
            var rightId = right.SkillId;
            var leftHasInt = int.TryParse(leftId, out var leftInt);
            var rightHasInt = int.TryParse(rightId, out var rightInt);

            if (leftHasInt && rightHasInt)
                return leftInt.CompareTo(rightInt);
            if (leftHasInt)
                return -1;
            if (rightHasInt)
                return 1;
            return string.Compare(leftId, rightId, StringComparison.Ordinal);
        }

        static int CompareNull(object left, object right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            return 0;
        }
    }
}
