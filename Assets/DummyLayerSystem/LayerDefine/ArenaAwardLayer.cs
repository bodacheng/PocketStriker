using System;
using System.Collections.Generic;
using UnityEngine;

public class ArenaAwardLayer : UILayer
{
    [SerializeField] private ArenaRewardItem arenaRewardItem;
    [SerializeField] private RectTransform itemsParent;

    public void SetUp(IDictionary<string, Award> arenaAwards)
    {
        foreach (var kv in arenaAwards)
        {
            ArenaRewardItem item = Instantiate(arenaRewardItem);
            int arenaPoint = Int32.Parse(kv.Key);
            item.Set(arenaPoint, kv.Value);
            item.transform.SetParent(itemsParent);
        }
    }
}
