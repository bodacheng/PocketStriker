using System;
using System.Collections.Generic;
using System.Linq;
using PlayFab.ClientModels;

// Compile the real shop process and read-only-data client against controllable
// UI/transport doubles so callback order can be exercised without Unity or a login.
internal static class ShopLoadingTests
{
    private static int checks;

    public static int Main()
    {
        try
        {
            var shop = new ShopTop();
            shop.ProcessEnter();
            Check(shop.GetLoaded() && DummyLayerSystem.UILayerLoader.Shop.Initialized,
                "shop opens before catalog completion and allows navigation");
            Check(PlayFab.PlayFabClientAPI.Pending.Count == 0 && Visible().Count == 0,
                "pending catalog shows no stone offers or empty-key backend query");

            IAPManager.Publish(new CatalogItem { ItemId = "owned" }, new CatalogItem { ItemId = "new" },
                null, new CatalogItem(), new CatalogItem { ItemId = "new" });
            var request = TakeRequest();
            Check(request.Keys.SequenceEqual(new[] { "owned", "new" }), "catalog filters invalid and duplicate product IDs");
            request.Succeed(new Dictionary<string, UserDataRecord> { ["owned"] = new UserDataRecord() });
            Check(Visible().SequenceEqual(new[] { "new" }), "ready catalog shows only unpurchased products");

            IAPManager.Publish(); // The IAP catalog failure path publishes an empty list.
            Check(shop.GetLoaded() && Visible().Count == 0 && PlayFab.PlayFabClientAPI.Pending.Count == 0,
                "failed or empty catalog keeps a navigable empty shop");
            IAPManager.Publish(new CatalogItem { ItemId = "unconfirmed" });
            TakeRequest().Fail();
            Check(shop.GetLoaded() && Visible().Count == 0 && PlayFabReadClient.Errors == 1,
                "purchase-history failure keeps products hidden without blocking navigation");

            IAPManager.Publish(new CatalogItem { ItemId = "stale" });
            var stale = TakeRequest();
            IAPManager.Publish(new CatalogItem { ItemId = "latest" });
            var latest = TakeRequest();
            var cacheBefore = PlayFabReadClient.ShowStoneBundleIds.ToArray();
            stale.Succeed(null);
            Check(Visible().Count == 0 && PlayFabReadClient.ShowStoneBundleIds.SequenceEqual(cacheBefore),
                "older catalog callback cannot change UI or purchase-history cache");
            latest.Succeed(null);
            Check(Visible().SequenceEqual(new[] { "latest" }), "latest request handles absent purchase-history data");

            IAPManager.Publish(new CatalogItem { ItemId = "after-exit" });
            var afterExit = TakeRequest();
            shop.ProcessEnd();
            var loadsBefore = DummyLayerSystem.UILayerLoader.ShopLoads;
            afterExit.Succeed(null);
            Check(DummyLayerSystem.UILayerLoader.Shop == null && DummyLayerSystem.UILayerLoader.ShopLoads == loadsBefore &&
                  PlayFabReadClient.ShowStoneBundleIds.SequenceEqual(new[] { "latest" }),
                "late success after exit neither recreates shop nor changes cache");
            IAPManager.Publish(new CatalogItem { ItemId = "while-closed" });
            Check(PlayFab.PlayFabClientAPI.Pending.Count == 0, "closed shop unsubscribes from catalog changes");

            shop.ProcessEnter();
            var oldVisit = TakeRequest();
            shop.ProcessEnd();
            shop.ProcessEnter();
            var newVisit = TakeRequest();
            oldVisit.Fail();
            Check(PlayFabReadClient.Errors == 1 && Visible().Count == 0, "late error from previous visit is ignored");
            newVisit.Succeed(null);
            Check(Visible().SequenceEqual(new[] { "while-closed" }), "reentered shop accepts only its current callback");
            IAPManager.Publish(new CatalogItem { ItemId = "once" });
            Check(PlayFab.PlayFabClientAPI.Pending.Count == 1, "reentry retains one catalog subscription");
            shop.ProcessEnd();

            Console.WriteLine("PASS: " + checks + " shop catalog/lifecycle checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static List<string> Visible() => DummyLayerSystem.UILayerLoader.Shop.Visible;
    private static PlayFab.PendingRead TakeRequest() => PlayFab.PlayFabClientAPI.Pending.Dequeue();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("FAILED: " + message);
        checks++;
    }
}

namespace PlayFab.ClientModels
{
    public sealed class CatalogItem { public string ItemId; }
    public sealed class GetUserDataRequest { public string PlayFabId; public List<string> Keys; }
    public sealed class UserDataRecord { }
    public sealed class GetUserDataResult { public Dictionary<string, UserDataRecord> Data; }
}
namespace PlayFab
{
    public sealed class PlayFabError { }
    public sealed class PendingRead
    {
        public List<string> Keys;
        public Action<GetUserDataResult> Success;
        public Action<PlayFabError> Error;
        public void Succeed(Dictionary<string, UserDataRecord> data) => Success(new GetUserDataResult { Data = data });
        public void Fail() => Error(new PlayFabError());
    }
    public static class PlayFabClientAPI
    {
        public static readonly Queue<PendingRead> Pending = new Queue<PendingRead>();
        public static void GetUserReadOnlyData(GetUserDataRequest request, Action<GetUserDataResult> success,
            Action<PlayFabError> error) => Pending.Enqueue(new PendingRead { Keys = request.Keys, Success = success, Error = error });
    }
}
public static class IAPManager
{
    public static List<CatalogItem> StoneProductCatalog = new List<CatalogItem>();
    public static event Action StoneProductCatalogChanged;
    public static void Publish(params CatalogItem[] items)
    {
        StoneProductCatalog = items.ToList();
        StoneProductCatalogChanged?.Invoke();
    }
}
public partial class PlayFabReadClient
{
    public static int Errors;
    public static readonly Dictionary<string, string> MyTimeLimitBundleBoughtLog = new Dictionary<string, string>();
    public static void ErrorReport(PlayFab.PlayFabError error) => Errors++;
}
public sealed class PlayerAccountInfo
{
    public static readonly PlayerAccountInfo Me = new PlayerAccountInfo();
    public bool noAdsState;
    public string PlayFabId = "test-player";
}
public static class PlayFabSetting { public const string _timeLimitBuyCode = "test-sale"; }
public sealed class TimeLimitedBuyData { public string startTime; public string endTime; public string eventID; }
public enum Element { Null }
public sealed class BackGroundPS
{
    public static readonly BackGroundPS target = new BackGroundPS();
    public void ChangeBGByElement(Element element) { }
}
public sealed class UpperInfoBar
{
    public void Setup(object a, object b, object c, object d, bool noAds) { }
}
namespace mainMenu
{
    public enum MainSceneStep { ShopTop }
    public abstract class MSceneProcess
    {
        public MainSceneStep Step;
        private bool loaded;
        protected void SetLoaded(bool value) => loaded = value;
        public bool GetLoaded() => loaded;
        public abstract void ProcessEnter();
        public abstract void ProcessEnd();
    }
    public sealed class ShopTopLayer
    {
        public bool Initialized;
        public List<string> Visible = new List<string>();
        public void Initialize() => Initialized = true;
        public void ShowTimeLimitedBundle() { }
        public void ShowStoneBundle(List<string> ids) => Visible = ids.ToList();
    }
}
namespace DummyLayerSystem
{
    public static class UILayerLoader
    {
        public static mainMenu.ShopTopLayer Shop;
        public static int ShopLoads;
        public static T Load<T>() where T : new()
        {
            var layer = new T();
            if (layer is mainMenu.ShopTopLayer shop) { Shop = shop; ShopLoads++; }
            return layer;
        }
        public static void Remove<T>()
        {
            if (typeof(T) == typeof(mainMenu.ShopTopLayer)) Shop = null;
        }
    }
}
