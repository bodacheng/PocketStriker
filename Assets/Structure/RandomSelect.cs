using UnityEngine;
using System.Collections.Generic;

public static class RandomSelect
{
    // 从最小值和最大值范围之间取不重复的随机selectCount个值
    public static List<int> Get(int RangeMin,int RangeMax, int selectCount)
    {
        List<int> target = new List<int>();
        while(target.Count != selectCount)
        {
            int one = Random.Range(RangeMin, RangeMax + 1);
            if (!target.Contains(one))
            {
                target.Add(one);
            }
        }
        return target;
    }
}