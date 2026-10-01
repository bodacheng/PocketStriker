using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;
using UnityEngine.UI;

public static partial class PocketStrikerUILiveReview
{
    // Optional deterministic latency around real local Addressables loads. All
    // selections/returns still use native buttons and the logged-in account.
    sealed class IconDelay
    {
        readonly UniTaskCompletionSource release = new UniTaskCompletionSource();
        public readonly List<SKStoneItem> results = new List<SKStoneItem>();
        public int started, pending;
        public async UniTask<SKStoneItem> Load(string id)
        {
            started++; pending++;
            try
            {
                var item = await dataAccess.Stones.GenerateStoneModel(id, false);
                if (item != null) results.Add(item);
                await release.Task;
                return item;
            }
            finally { pending--; }
        }
        public void Release() => release.TrySetResult();
    }
    static void SetIconDelay(object target, IconDelay delay) => target.GetType().GetField("iconLoaderForValidation", Fields)
        .SetValue(target, delay == null ? null : (Func<string, UniTask<SKStoneItem>>)delay.Load);

    static async UniTask AsyncIconNavigation()
    {
        report.cases.Add(new Case { name = "collection-delayed-selection-and-exit" });
        report.cases.Add(new Case { name = "stone-detail-delayed-selection-and-exit" });
        await Check("collection-delayed-selection-and-exit", async () =>
        {
            await Home(); await Click(Tab("fighterTab")); await Page<UnitOptionLayer>(MainSceneStep.UnitList,30);
            var layer = Layer<UnitOptionLayer>(); var nine = layer._NineForShow;
            var icons = Layer<UnitsLayer>().GetComponentsInChildren<HeroIcon>().Where(x => x.iconButton.IsInteractable()).Take(2).ToArray();
            Require(icons.Length == 2,"Two visible account characters are needed for selection regression.");
            var old = new IconDelay();
            try
            {
                SetIconDelay(nine,old); await Click(icons[0].iconButton); Require(old.started == 9,"Character click did not request a grid.");
                SetIconDelay(nine,null); await Click(icons[1].iconButton);
                await Wait(() => GridMatchesAccount(nine),20,"latest account character skills");
                old.Release(); await Wait(() => old.pending == 0,20,"old character icons completed"); await Stable();
                Require(GridMatchesAccount(nine),"Old character icons changed the latest character's grid.");
                Require(old.results.All(x => x == null),"Superseded character icons leaked.");
                for(int i=0;i<4;i++) { await Click(icons[i%2].iconButton,true); await Wait(() => GridMatchesAccount(nine),15,"repeated account character selection"); }
                await Screenshot("collection-latest-skill-grid");
            }
            finally { if(nine != null) SetIconDelay(nine,null); old.Release(); }
            var exiting = new IconDelay();
            try
            {
                SetIconDelay(nine,exiting); await Click(icons[0].iconButton);
                await Home(); await Wait(() => nine == null,10,"collection destroyed on navigation");
                exiting.Release(); await Wait(() => exiting.pending == 0,20,"exited character icons completed"); await Stable();
                Require(exiting.results.All(x => x == null),"Icons survived their closed account collection.");
                await Click(Tab("fighterTab")); await Page<UnitOptionLayer>(MainSceneStep.UnitList,30);
                await Wait(() => GridMatchesAccount(Layer<UnitOptionLayer>()._NineForShow),20,"collection reentry skills");
                CheckDuplicates();
            }
            finally { if(nine != null) SetIconDelay(nine,null); exiting.Release(); }
            current.note = "Real account/native character clicks, four alternating repeated clicks, controlled delayed old grid, exit while loading, late-object disposal and reentry. No account skill data changed.";
        });
        await Check("stone-detail-delayed-selection-and-exit", async () =>
        {
            await Home(); await Click(Tab("stoneTab")); await Page<StoneListLayer>(MainSceneStep.SkillStoneList,35);
            var layer = Layer<StoneListLayer>(); var detail = layer.SkillStoneDetail;
            var cells = layer.box.GetComponentsInChildren<StoneCell>().Where(x => x.GetItem() != null && x.btn.IsInteractable())
                .GroupBy(x => x.GetItem()._SkillConfig.RECORD_ID).Select(x => x.First()).Take(2).ToArray();
            Require(cells.Length == 2,"Two visible distinct account stones are required.");
            var old = new IconDelay();
            try
            {
                SetIconDelay(detail,old); await Click(cells[0].btn); Require(old.started == 1,"Stone click did not request detail.");
                SetIconDelay(detail,null); await Click(cells[1].btn);
                await Wait(() => DetailMatchesAccount(layer),20,"latest stone icon and description");
                old.Release(); await Wait(() => old.pending == 0,20,"old stone icon completed"); await Stable();
                Require(DetailMatchesAccount(layer),"Old stone icon replaced current selection.");
                Require(old.results.All(x => x == null),"Superseded detail icon survived.");
                for(int i=0;i<4;i++) { await Click(cells[i%2].btn,true); await Wait(() => DetailMatchesAccount(layer),15,"repeated stone detail selection"); }
                await Screenshot("stone-detail-latest-selection");
            }
            finally { if(detail != null) SetIconDelay(detail,null); old.Release(); }
            var exiting = new IconDelay();
            try
            {
                SetIconDelay(detail,exiting); await Click(cells[0].btn); await Home();
                await Wait(() => detail == null,10,"stone detail destroyed on navigation");
                exiting.Release(); await Wait(() => exiting.pending == 0,20,"exited stone icon completed"); await Stable();
                Require(exiting.results.All(x => x == null),"Late stone icon survived its account page.");
                await Click(Tab("stoneTab")); await Page<StoneListLayer>(MainSceneStep.SkillStoneList,35); CheckDuplicates();
            }
            finally { if(detail != null) SetIconDelay(detail,null); exiting.Release(); }
            current.note = "Real account/native stone clicks, four alternating repeated clicks, real icons with controlled latency, current text/icon agreement, navigation before load completion and clean reentry. No merges or purchases.";
        });
    }
    static bool GridMatchesAccount(NineForShow nine)
    {
        if(nine == null || PreScene.target.Focusing == null) return false;
        var expected = dataAccess.Stones.GetEquippingStones(PreScene.target.Focusing.id).ToDictionary(x => int.Parse(x.slot),x => x.SkillId);
        var buttons = nine.AllButton();
        for(int i=0;i<buttons.Count;i++)
        {
            var icons = buttons[i].GetComponentsInChildren<SKStoneItem>();
            if(expected.TryGetValue(i+1,out var id)) { if(icons.Length != 1 || icons[0]._SkillConfig.RECORD_ID != id) return false; }
            else if(icons.Length != 0) return false;
        }
        return true;
    }
    static bool DetailMatchesAccount(StoneListLayer layer)
    {
        var stone = dataAccess.Stones.Get(layer.TargetStoneID); if(stone == null) return false;
        var detail = layer.SkillStoneDetail;
        var icons = Field<RectTransform>(detail,"iconShowT").GetComponentsInChildren<SKStoneItem>();
        var config = SkillConfigTable.GetSkillConfigByRecordId(stone.SkillId);
        return icons.Length == 1 && icons[0]._SkillConfig.RECORD_ID == stone.SkillId
            && Field<Text>(detail,"keyName").text == config.REAL_NAME;
    }
}
