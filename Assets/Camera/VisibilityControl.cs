using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VisibilityControl : MonoBehaviour
{
    [SerializeField] float radius = 5f; // SphereCast的半径
    [SerializeField] float detectDis = 50f; // 可见性控制半径
    [SerializeField] LayerMask layerMask; // 可以在Inspector中指定的层掩码
    [SerializeField] LayerMask unitLayer;
    [SerializeField] int wallLayer;
    [SerializeField] int detectInterval = 10;
    [SerializeField] int rayCastMax = 20;
    private List<Renderer> _hiddenObjects = new List<Renderer>(); // 存储当前被隐藏的物体
    private int _frameCounter = 0;
    
    // 预先分配一个足够大的数组，以存储可能检测到的所有collider
    // 数组的大小取决于你预计场景中可能同时存在的最大collider数量
    private RaycastHit[] _hitColliders;
    private readonly List<Renderer> thisFrameDetected = new List<Renderer>();
    private List<RaycastHit> hitList = new List<RaycastHit>();
    private readonly List<RaycastHit> unitTargets = new List<RaycastHit>();
    private readonly List<RaycastHit> wallTargets = new List<RaycastHit>();
    void Start()
    {
        _hitColliders = new RaycastHit[rayCastMax];
    }

    public void LocalUpdate()
    {
        if (_frameCounter < detectInterval)
        {
            _frameCounter++;
            return;
        }
        _frameCounter = 0;
        
        Ray ray = new Ray(transform.position, transform.forward);
        
        // 获取实际检测到的collider数量
        int numColliders = Physics.SphereCastNonAlloc(ray, radius, _hitColliders, detectDis, layerMask);
        // 雅典城场景1对1模式下加上角色大概能检测到不到30个对象
        
        if (numColliders > 0)
        {
            hitList = new List<RaycastHit>(_hitColliders.Take(numColliders)); // 直接使用数组的切片
            foreach (var hit in hitList)
            {
                if ((unitLayer & (1 << hit.collider.gameObject.layer)) != 0)
                    unitTargets.Add(hit);
                if (hit.collider.gameObject.layer == wallLayer)
                    wallTargets.Add(hit);
            }
            
            if (unitTargets.Count > 0 && wallTargets.Count > 0)
            {
                var minDis = unitTargets.Min(x=> Vector3.Distance(x.transform.position, transform.position));
                foreach (var wall in wallTargets)
                {
                    if (Vector3.Distance(wall.transform.position, transform.position) < minDis)
                    {
                        var renderer = wall.transform.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.enabled = false; // 禁用Renderer组件
                            thisFrameDetected.Add(renderer); // 将对象添加到隐藏对象集合中
                        }
                    }
                }
            }
        }
        
        // 检查被隐藏的物体是否已经移出了半径范围
        foreach (var renderer in _hiddenObjects)
        {
            if (!thisFrameDetected.Contains(renderer))
                renderer.enabled = true; // 启用Renderer组件
        }
        
        _hiddenObjects = new List<Renderer>(thisFrameDetected);
        thisFrameDetected.Clear();
        unitTargets.Clear();
        wallTargets.Clear();
    }

    public void Clear()
    {
        _hiddenObjects.Clear();
    }
}
