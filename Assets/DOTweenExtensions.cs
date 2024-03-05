using UniRx;
using System;
using DG.Tweening;

public static class DOTweenExtensions
{
    public static IObservable<Tween> OnCompleteAsObservable(this Tween tween)
    {
        return Observable.Create<Tween>(o =>
        {
            tween.OnComplete(() =>
            {
                o.OnNext(tween);
                o.OnCompleted();
            });
            return Disposable.Create(() =>
            {
                tween.Kill();
            });
        });
    }

    public static IObservable<Sequence> PlayAsObservable(this Sequence sequence)
    {
        return Observable.Create<Sequence>(o =>
        {
            sequence.OnComplete(() =>
            {
                o.OnNext(sequence);
                o.OnCompleted();
            });
            sequence.Play();
            return Disposable.Create(() =>
            {
                sequence.Kill();
            });
        });
    }
}