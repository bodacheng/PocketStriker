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
        var emptySlots = layer.nineSlot.GetEmptySlots();
        var target = emptySlots.Random();
        endPoint = target._cell.GetComponent<RectTransform>();
        
        MoveElement();
    }
    
    private void MoveElement()
    {
        targetUIElement.position = startPoint.position;
        moveTween = targetUIElement.DOMove(endPoint.position, moveDuration)
            .OnComplete(() => targetUIElement.position = startPoint.position)
            .SetEase(Ease.Linear)
            .SetLoops(-1);
    }
    
    private void OnDestroy()
    {
        moveTween?.Kill();
    }
}
