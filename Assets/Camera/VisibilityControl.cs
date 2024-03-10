using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VisibilityControl : MonoBehaviour
{
    [SerializeField] float radius = 5.0f; // 可见性控制半径
    [SerializeField] LayerMask layerMask; // 可以在Inspector中指定的层掩码
    [SerializeField] int detectInterval = 10;
    private List<Renderer> _hiddenObjects = new List<Renderer>(); // 存储当前被隐藏的物体
    private int _frameCounter = 0;
    
    // 预先分配一个足够大的数组，以存储可能检测到的所有collider
    // 数组的大小取决于你预计场景中可能同时存在的最大collider数量
    readonly Collider[] _hitColliders = new Collider[600]; 
    
    public void LocalUpdate()
    {
        if (_frameCounter < detectInterval)
        {
            _frameCounter++;
            return;
        }
        _frameCounter = 0;
        
        // 获取实际检测到的collider数量
        int numColliders = Physics.OverlapSphereNonAlloc(transform.position, radius, _hitColliders, layerMask);
        List<Renderer> thisFrameDetected = new List<Renderer>();
        for (int i = 0; i < numColliders; i++)
        {
            GameObject obj = _hitColliders[i].gameObject;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false; // 禁用Renderer组件
                thisFrameDetected.Add(renderer); // 将对象添加到隐藏对象集合中
            }
        }
        
        // 检查被隐藏的物体是否已经移出了半径范围
        foreach (var renderer in _hiddenObjects)
        {
            if (!thisFrameDetected.Contains(renderer))
                renderer.enabled = true; // 启用Renderer组件
        }
        
        _hiddenObjects = thisFrameDetected;
    }

    public void Clear()
    {
        _hiddenObjects.Clear();
    }
    
    // 当对象被选中时在Scene视图中绘制一个表示半径的球体
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; // 设置绘制颜色为黄色
        Gizmos.DrawWireSphere(transform.position, radius); // 绘制一个中心在物体位置、半径为radius的线框球体
    }
}
