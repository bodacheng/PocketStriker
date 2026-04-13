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
            {"EventBattleTop", "DummyLayerSystem/EventBattleTop"},
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
            {"InBattleEvolution", "DummyLayerSystem/InBattleEvolution"},
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
            {"TeamSingleSelectLayer", "DummyLayerSystem/TeamSingleSelectLayer"},
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
        private static Transform _fullScreenHanger;
        public static void SetHanger(Transform target)
        {
            SetHanger(target, target);
        }

        public static void SetHanger(Transform target, Transform fullScreenHanger)
        {
            _hanger = target;
            _fullScreenHanger = fullScreenHanger;
        }

        private static RectTransform effectBg;
        public static void SetEffectBg(RectTransform _effectBg)
        {
            effectBg = _effectBg;
        }

        private static readonly List<UILayer> Queues = new List<UILayer>();
        
        public static void Clear(string except = null)
        {
            PruneInvalidEntries();
            var toRemove = new List<UILayer>();
            foreach (var queue in Queues)
            {
                if (queue != null && except != queue.Index)
                {
                    toRemove.Add(queue);
                }
            }
            
            foreach (var layer in toRemove)
            {
                if (layer != null && layer.gameObject != null)
                {
                    Remove(layer.Index);
                }
            }

            if (_hanger != null)
                EnsureMosakAtBottom(_hanger);
            if (_fullScreenHanger != null)
                EnsureMosakAtBottom(_fullScreenHanger);
        }
    
        static UILayer _Get(string key)
        {
            return Queues.Find(x => x.Index == key);
        }
        
        public static T Get<T>() where T : UILayer
        {
            PruneInvalidEntries();
            var key = typeof(T).Name;
            var target = Queues.Find(x => x != null && !x.IsClosing && x.Index == key);
            if (target != null)
            {
                return target as T;
            }

            return RecoverExistingLayer<T>(key);
        }

        static T RecoverExistingLayer<T>(string key) where T : UILayer
        {
            var foundLayers = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            T selected = null;
            foreach (var layer in foundLayers)
            {
                if (layer == null || layer.IsClosing)
                    continue;

                if (selected == null || LayerScore(layer, key) > LayerScore(selected, key))
                {
                    selected = layer;
                }
            }

            if (selected == null)
                return default;

            selected.Index = key;
            selected.IsClosing = false;
            if (!Queues.Contains(selected))
            {
                Queues.Add(selected);
            }

            foreach (var layer in foundLayers)
            {
                if (layer == null || layer.IsClosing || layer == selected)
                    continue;

                Debug.LogWarning($"[UILayerLoader] Duplicate {key} detected. Keeping {DescribeLayer(selected)}, removing {DescribeLayer(layer)}.");
                MarkClosingAndDestroy(layer);
            }

            Debug.LogWarning($"[UILayerLoader] Recovered untracked {DescribeLayer(selected)}.");
            return selected;
        }
        
        public static T Load<T>(bool insertToTop = false, string key = null, bool loadToFullScreen = false) where T : UILayer
        {
            var targetHanger = loadToFullScreen ? _fullScreenHanger : _hanger;
            if (targetHanger == null)
                return default;
            var className = typeof(T).Name;
            var layerName = key ?? className;
            var existed = Get<T>();
            if (existed != null)
            {
                existed.IsClosing = false;
                existed.Index = className;
                if (existed.transform.parent != targetHanger)
                {
                    existed.transform.SetParent(targetHanger.transform, false);
                    ResizeLayerRect(existed);
                }
                ApplySiblingOrder(existed.transform, insertToTop, loadToFullScreen);
                EnsureMosakAtBottom(targetHanger);
                return existed;
            }
            
            var path = Paths[layerName];
            var UILayerPrefab = Resources.Load<UILayer>(path);
            var t = GameObject.Instantiate(UILayerPrefab);
            t.Index = className;
            t.IsClosing = false;
            t.transform.SetParent(targetHanger.transform);
            t.transform.localPosition = Vector3.zero;
            ApplySiblingOrder(t.transform, insertToTop, loadToFullScreen);
            ResizeLayerRect(t);
            t.ResizeAreas();
            Queues.Add(t);
            var returnValue = t as T;
            
            if (effectBg != null)
            {
                effectBg.transform.SetParent(targetHanger);
                effectBg.transform.SetAsLastSibling();
            }

            EnsureMosakAtBottom(targetHanger);
            
            return returnValue;
        }
        
        public static void Pop()
        {
            PruneInvalidEntries();
            if (Queues.Count > 0)
            {
                var uiLayer = Queues[Queues.Count - 1];
                if (uiLayer != null)
                {
                    MarkClosingAndDestroy(uiLayer);
                }
                Queues.RemoveAt(Queues.Count - 1);

                if (_hanger != null)
                    EnsureMosakAtBottom(_hanger);
                if (_fullScreenHanger != null)
                    EnsureMosakAtBottom(_fullScreenHanger);
            }
        }

        public static void Remove<T>()
        {
            string layerName = typeof(T).Name;
            Remove(layerName);
        }
    
        static void Remove(string index)
        {
            PruneInvalidEntries();
            var toRemoveIndex = -1;
            for (var i = 0; i < Queues.Count; i++)
            {
                var uiLayer = Queues[i];
                if (uiLayer != null && uiLayer.Index == index)
                {
                    toRemoveIndex = i;
                }
            }
            
            if (toRemoveIndex >= 0)
            {
                var layer = Queues[toRemoveIndex];
                if (layer != null)
                    MarkClosingAndDestroy(layer);
                Queues.RemoveAt(toRemoveIndex);

                if (_hanger != null)
                    EnsureMosakAtBottom(_hanger);
                if (_fullScreenHanger != null)
                    EnsureMosakAtBottom(_fullScreenHanger);
            }
        }

        private static void ApplySiblingOrder(Transform layerTransform, bool insertToTop, bool loadToFullScreen)
        {
            if (layerTransform == null)
                return;

            if (loadToFullScreen)
            {
                if (insertToTop)
                {
                    layerTransform.SetAsLastSibling();
                }
                else
                {
                    layerTransform.SetAsFirstSibling();
                }
                return;
            }

            if (insertToTop)
            {
                layerTransform.SetAsLastSibling();
            }
        }

        private static void EnsureMosakAtBottom(Transform targetHanger)
        {
            if (targetHanger == null)
                return;

            var mosakTransform = targetHanger.Find("mosak");
            if (mosakTransform != null)
            {
                mosakTransform.SetAsFirstSibling();
            }
        }

        static void ResizeLayerRect(UILayer layer)
        {
            if (layer == null)
                return;

            var rt = layer.GetComponent<RectTransform>();
            if (rt == null)
                return;

            rt.anchorMax = Vector2.one;
            rt.anchorMin = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localPosition = Vector3.zero;
            rt.localScale = Vector3.one;
        }

        static void MarkClosingAndDestroy(UILayer layer)
        {
            if (layer == null)
                return;

            layer.IsClosing = true;
            if (layer.gameObject != null)
            {
                layer.gameObject.SetActive(false);
                GameObject.Destroy(layer.gameObject);
            }
        }

        static void PruneInvalidEntries()
        {
            for (var i = Queues.Count - 1; i >= 0; i--)
            {
                if (Queues[i] == null)
                {
                    Queues.RemoveAt(i);
                }
            }
        }

        static int LayerScore(UILayer layer, string key)
        {
            if (layer == null)
                return int.MinValue;

            var score = 0;
            if (layer.gameObject.activeInHierarchy)
                score += 4;
            if (layer.transform.parent == _hanger || layer.transform.parent == _fullScreenHanger)
                score += 2;
            if (layer.Index == key)
                score += 1;
            return score;
        }

        static string DescribeLayer(UILayer layer)
        {
            if (layer == null)
                return "<null>";

            return $"{layer.GetType().Name}#{layer.GetInstanceID()} path={GetPath(layer.transform)} active={layer.gameObject.activeInHierarchy}";
        }

        static string GetPath(Transform target)
        {
            if (target == null)
                return "<missing>";

            var path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }

            return path;
        }

        internal static void NotifyDestroyed(UILayer layer)
        {
            for (var i = Queues.Count - 1; i >= 0; i--)
            {
                var queued = Queues[i];
                if (queued == null || ReferenceEquals(queued, layer))
                {
                    Queues.RemoveAt(i);
                }
            }
        }
    }
}
