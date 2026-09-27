using DG.Tweening;
using DummyLayerSystem;
using NoSuchStudio.Common;
using UnityEngine;

public class SkillEditTutorial11 : MonoBehaviour
{
    [SerializeField] RectTransform targetUIElement;
    [SerializeField] RectTransform startPoint;
    [SerializeField] RectTransform endPoint;
    [SerializeField] float moveDuration = 1f;
    
    private Tween moveTween;
    
    void OnEnable()
    {
        var layer = UILayerLoader.Get<SkillEditLayer>();
        if (layer == null || layer.nineSlot == null)
        {
            return;
        }

        var emptySlots = layer.nineSlot.GetEmptySlots();
        if (emptySlots == null || emptySlots.Count == 0)
        {
            return;
        }

        var target = emptySlots.Random();
        if (target == null || target._cell == null)
        {
            return;
        }

        endPoint = target._cell.GetComponent<RectTransform>();
        MoveElement();
    }
    
    private void MoveElement()
    {
        if (targetUIElement == null || startPoint == null || endPoint == null)
        {
            return;
        }

        targetUIElement.position = startPoint.position;
        moveTween?.Kill();
        moveTween = targetUIElement.DOMove(endPoint.position, moveDuration)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (targetUIElement == null || startPoint == null)
                {
                    return;
                }

                targetUIElement.position = startPoint.position;
            })
            .SetEase(Ease.Linear)
            .SetLoops(-1);
    }
    
    private void OnDestroy()
    {
        moveTween?.Kill();
    }
}
