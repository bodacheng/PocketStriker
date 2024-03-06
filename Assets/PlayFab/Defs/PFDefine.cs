using System.Collections.Generic;
using UnityEngine;
using Skill;
using System.Collections;
using Log;
using Json;
using Newtonsoft.Json;
using System.Linq;

public class PFDefine
{
    public string CatalogVersion = "";
    public Item[] Catalog;
    public string[] DropTables = new string[] { };

    public class Item
    {
        public string ItemId;
        public string ItemClass;
        public string CatalogVersion;
        public string DisplayName;
        public string Description;
        public string VirtualCurrencyPrices;
        public string RealCurrencyPrices;
        public string[] Tags = new string[] { };
        public string CustomData;
        public Consumable Consumable;
        public string Container;
        public string Bundle;
        public bool CanBecomeCharacter;
        public bool IsStackable;
        public bool IsTradable;
        public string ItemImageUrl;
        public bool IsLimitedEdition;
        public int InitialLimitedEditionCount;
        public string ActivatedMembership;
    }

    public class SK_CustomData
    {
        public string exp;
        public string equipingC;
    }

    public class C_CustomData
    {
        public string zokusei { get; set; }

        /// <summary>
        /// PlayFab 的定义文件，在CustomData这一个项目上，存在一个很蛋疼的问题
        /// 那就是正确的格式是包含转义符的，每一个引号前面都有个\，否则会拖进去updatejson的时候会报错
        /// 我们并没有把这个问题的实质理解透，只是尝试去硬卡playfab的规则，做法就是在制作定义文件时候，
        /// 把CustomData直接作为个字符串处理，然后用下面这个式子硬给搞成playfab要求的那个格式。也就是所有引号前面加\
        /// </summary>
        /// <returns></returns>
        public string AsPlayFabVer()
        {
            string value = "{" + "\"zokusei\"" + ":" + "\"" + zokusei + "\"" + "}";
            return value;
        }
    }

    public class Consumable
    {
        public string UsageCount;
        public string UsagePeriod;
        public string UsagePeriodGroup;
    }
}