using System;
using System.Collections.Generic;
using UnityEngine;

namespace DummyLayerSystem
{
    internal static class UILayerLoader
    {
        static readonly IDictionary<string, string> Paths = new Dictionary<string, string>()
        {
            {"LowerMainBar", "DummyLayerSystem/LowerMainBar"},
            {"NickNameLayer", "DummyLayerSystem/NickNameLayer"},
            {"UpperInfoBar", "DummyLayerSystem/UpperInfoBar"},
            {"FrontLayer", "DummyLayerSystem/FrontLayer"},
            {"ArcadeTop", "DummyLayerSystem/ArcadeTop"},
            {"ArenaLayer", "DummyLayerSystem/ArenaLayer"},
            {"ArenaAwardLayer", "DummyLayerSystem/ArenaAwardLayer"},
            {"ArenaNewSeason", "DummyLayerSystem/ArenaNewSeason"},
            {"RankingLayer", "DummyLayerSystem/RankingLayer"},
            {"MailBox", "DummyLayerSystem/MailBox"},
            {"MailDetailView", "DummyLayerSystem/MailDetailView"},
            {"ArenaFightOver", "DummyLayerSystem/ArenaFightOver"},
            {"CommonFightResult", "DummyLayerSystem/CommonFightResult"},
            {"TitleScreenLayer", "DummyLayerSystem/TitleScreenLayer"},
            {"TitleBgLayer", "DummyLayerSystem/TitleBgLayer"},
            {"ImageBg", "DummyLayerSystem/ImageBg"},
            {"FightResultAnimLayer", "DummyLayerSystem/FightResultAnimLayer"},
            {"CountDownLayer", "DummyLayerSystem/CountDownLayer"},
            {"FightingStepLayer", "DummyLayerSystem/FightingStepLayer"},
            {"SettingLayer", "DummyLayerSystem/SettingLayer"},
            {"AskIfLinkDeviceLayer", "DummyLayerSystem/AskIfLinkDeviceLayer"},
            {"UnitsLayer", "DummyLayerSystem/UnitsLayer"},
            {"PopupLayer", "DummyLayerSystem/PopupLayer"},
            {"HighLightLayer", "DummyLayerSystem/HighLightLayer"},
            {"ProgressLayer", "DummyLayerSystem/ProgressLayer"},
            {"UnitInstructionLayer", "DummyLayerSystem/UnitInstructionLayer"},
            {"SelfFightLayer", "DummyLayerSystem/SelfFightLayer"},
            {"FightPrepareLayer", "DummyLayerSystem/FightPrepareLayer"},
            {"TeamEditLayer", "DummyLayerSystem/TeamEditLayer"},
            {"SkillShowLayer", "DummyLayerSystem/SkillShowLayer"},
            {"GotchaLayer", "DummyLayerSystem/GotchaLayer"},
            {"GotchaResultLayer", "DummyLayerSystem/GotchaResultLayer"},
            {"DropTableInfoLayer", "DummyLayerSystem/DropTableInfoLayer"},
            {"StoneListLayer", "DummyLayerSystem/StoneListLayer"},
            {"SkillEditLayer", "DummyLayerSystem/SkillEditLayer"},
            {"SkillEditTipLayer", "DummyLayerSystem/SkillEditTipLayer"},
            {"UnitOptionLayer", "DummyLayerSystem/UnitOptionLayer"},
            {"StoneMergeLayer", "DummyLayerSystem/StoneMergeLayer"},
            {"ShopTopLayer", "DummyLayerSystem/ShopTopLayer"},
            {"BoxExpandHelperLayer", "DummyLayerSystem/BoxExpandHelperLayer"},
            {"BoxOverLoadFixLayer", "DummyLayerSystem/BoxOverLoadFixLayer"},
            {"ReturnLayer", "DummyLayerSystem/ReturnLayer"},
            {"LoginLayer", "DummyLayerSystem/LoginLayer"},
            {"FightScenePauseSupport", "DummyLayerSystem/FightScenePauseSupport"},
            {"BuyNoAds", "DummyLayerSystem/BuyNoAds"},
            {"StoneUpdatesConfirm", "DummyLayerSystem/StoneUpdatesConfirm"}
        };

        private static Transform _hanger;
        public static void SetHanger(Transform target)
        {
            _hanger = target;
        }

        private static RectTransform effectBg;
        public static void SetEffectBg(RectTransform _effectBg)
        {
            effectBg = _effectBg;
        }

        private static readonly List<UILayer> Queues = new List<UILayer>();
        
        public static void Clear(string except = null)
        {
            var toRemove = new List<UILayer>();
            foreach (var queue in Queues)
            {
                if (except != queue.Index)
                {
                    toRemove.Add(queue);
                }
            }
            
            foreach (var layer in toRemove)
            {
                Queues.Remove(layer);
                if (layer != null && layer.gameObject != null)
                    Remove(layer.Index);
            }
        }
    
        static UILayer _Get(string key)
        {
            return Queues.Find(x => x.Index == key);
        }
        
        public static T Get<T>()
        {
            var target = Queues.Find(x => x.Index == typeof(T).Name);
            if (target != null)
                return (T) Convert.ChangeType(target, typeof(T));
            else
            {
                return default;
            }
        }
        
        public static T Load<T>(bool insertToTop = false)
        {
            if (_hanger == null)
                return default;
            var layerName = typeof(T).Name;
            var existed = Get<T>();
            if (existed != null)
            {
                if (insertToTop)
                {
                    var target = existed as GameObject;
                    target?.transform.SetAsLastSibling();
                }
                return existed;
            }
            
            var path = Paths[layerName];
            var UILayerPrefab = Resources.Load<UILayer>(path);
            var t = GameObject.Instantiate(UILayerPrefab);
            t.Index = layerName;
            t.transform.SetParent(_hanger.transform);
            t.transform.localPosition = Vector3.zero;
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMax = Vector2.one;
            rt.anchorMin = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localPosition = Vector3.zero;
            rt.localScale = Vector3.one;
            t.ResizeAreas();
            Queues.Add(t);
            var returnValue = (T) Convert.ChangeType(t, typeof(T));
            
            if (effectBg != null)
            {
                effectBg.transform.SetParent(_hanger);
                effectBg.transform.SetAsLastSibling();
            }
            
            return returnValue;
        }
        
        public static void Pop()
        {
            if (Queues.Count > 0)
            {
                var uiLayer = Queues[Queues.Count - 1];
                if (uiLayer != null)
                {
                    uiLayer.OnDestroy();
                    GameObject.Destroy(uiLayer);
                }
                Queues.RemoveAt(Queues.Count - 1);
            }
        }

        public static void Remove<T>()
        {
            string layerName = typeof(T).Name;
            Remove(layerName);
        }
    
        static void Remove(string index)
        {
            var toRemoveIndex = -1;
            for (var i = 0; i < Queues.Count; i++)
            {
                var uiLayer = Queues[i];
                if (uiLayer.Index == index)
                {
                    toRemoveIndex = i;
                }
            }
            
            if (toRemoveIndex >= 0)
            {
                var layer = Queues[toRemoveIndex];
                if (layer != null)
                    GameObject.Destroy(layer.gameObject);
                Queues.RemoveAt(toRemoveIndex);
            }
        }
    }
}
