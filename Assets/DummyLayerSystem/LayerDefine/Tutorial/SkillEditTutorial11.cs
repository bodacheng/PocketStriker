using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class SkillEditTutorial11 : MonoBehaviour
{
    [SerializeField] RectTransform targetUIElement;
    [SerializeField] RectTransform startPoint;
    [SerializeField] RectTransform endPoint;
    [SerializeField] float moveDuration = 1f;
    
    private Tween moveTween;
    
    private void Start()
    {
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
        if (moveTween != null)
        {
            moveTween.Kill();
        }
    }
}
