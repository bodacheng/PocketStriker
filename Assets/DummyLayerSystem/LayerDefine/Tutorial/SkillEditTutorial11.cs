using DG.Tweening;
using UnityEngine;

public class SkillEditTutorial11 : MonoBehaviour
{
    [SerializeField] RectTransform targetUIElement;
    [SerializeField] RectTransform startPoint;
    [SerializeField] RectTransform endPoint;
    [SerializeField] float moveDuration = 1f;
    
    private Tween moveTween;

    public void SetTargets(RectTransform source, RectTransform destination)
    {
        if (startPoint == source && endPoint == destination && moveTween != null && moveTween.IsActive()) return;
        moveTween?.Kill();
        startPoint = source;
        endPoint = destination;
        if (targetUIElement != null) targetUIElement.gameObject.SetActive(source != null && destination != null);
        if (isActiveAndEnabled) MoveElement();
    }
    
    void OnEnable()
    {
        MoveElement();
    }
    
    private void MoveElement()
    {
        if (targetUIElement == null || startPoint == null || endPoint == null)
        {
            return;
        }

        moveTween?.Kill();
        float progress = 0;
        moveTween = DOTween.To(() => progress, value =>
            {
                progress = value;
                if (targetUIElement != null && startPoint != null && endPoint != null)
                    targetUIElement.position = Vector3.Lerp(startPoint.position, endPoint.position, progress);
            }, 1, moveDuration)
            .SetLink(gameObject)
            .SetEase(Ease.Linear)
            .SetLoops(-1);
    }
    
    private void OnDestroy()
    {
        moveTween?.Kill();
    }

    private void OnDisable()
    {
        moveTween?.Kill();
        moveTween = null;
    }
}
