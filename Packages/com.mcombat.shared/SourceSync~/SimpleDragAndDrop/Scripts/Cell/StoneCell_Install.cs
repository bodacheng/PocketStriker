using UnityEngine;
using UnityEngine.EventSystems;

public partial class StoneCell : MonoBehaviour, IDropHandler
{
    public static void Install(StoneCell from, StoneCell to)
    {
        switch(to.cellPhase)
        {
            case CellPhase.NineSlotCell:
                switch(from.cellPhase)
                {
                    case CellPhase.SkillStoneBoxCell:
                    case CellPhase.NineSlotCell:
                        if (to._myDadItem == null)
                        {
                            SVCenter.MoveItemFromTo(from, to);
                            break;
                        }else{
                            SVCenter.SwapItemFromTo(from, to);
                            break;
                        }
                }
            break;
            case CellPhase.SKLevelUpMSlot:
                if (to._myDadItem == null)
                    SVCenter.MoveItemFromTo(from, to);
                else
                    SVCenter.SwapItemFromTo(from, to);
            break;
            case CellPhase.SkillStoneBoxCell:
                switch(from.cellPhase)
                {
                    case CellPhase.NineSlotCell:
                        SVCenter.StoneRemoveFromSlotToCell(from, to);
                    break;
                    case CellPhase.SKLevelUpMSlot:
                        SVCenter.StoneRemoveFromSlotToCell(from, to);
                    break;
                }
            break;
            case CellPhase.StoneMergeSlot:
                if (to._myDadItem == null)
                    SVCenter.MoveItemFromTo(from, to);
                else
                    SVCenter.SwapItemFromTo(from, to);
            break;
        }
    }
}