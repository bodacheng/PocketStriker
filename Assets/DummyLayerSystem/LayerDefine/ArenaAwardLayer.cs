using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ArenaAwardLayer : UILayer
{
    [SerializeField] private ArenaRewardItem arenaRewardItem;
    [SerializeField] private RectTransform itemsParent;
    [SerializeField] private ScrollRect rewardScroll;

    public void SetUp(IDictionary<string, Award> arenaAwards)
    {
        // Refreshing the page must replace rows, including while a previous frame's
        // Destroy calls are still pending. Inactive old rows no longer take layout space.
        foreach (var existing in itemsParent.GetComponentsInChildren<ArenaRewardItem>(true))
        {
            existing.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        if (arenaAwards != null)
            foreach (var kv in arenaAwards.OrderBy(pair => Int32.Parse(pair.Key)))
            {
                // Keeping world scale here enlarged the row under a scaled canvas,
                // while the layout group still allocated its unscaled height.
                var item = Instantiate(arenaRewardItem, itemsParent, false);
                item.Set(Int32.Parse(kv.Key), kv.Value);
            }

        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsParent);
        if (rewardScroll != null)
        {
            rewardScroll.StopMovement();
            rewardScroll.verticalNormalizedPosition = 1;
        }
    }
}
