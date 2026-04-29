
public class PFStoreDefine
{
    public string StoreId = "";
    public StoreItem[] Store;
    public string[] SegmentOverrides;
    public _MarketingData MarketingData;

    public class StoreItem
    {
        public string ItemId;
        public VirtualCurrencyPrices VirtualCurrencyPrices;
        public CustomData CustomData;
        public string DisplayPosition;
    }

    public abstract class CustomData
    {
    }

    public class VirtualCurrencyPrices
    {
        public int GD;
        public int RM;
    }

    public class _MarketingData
    {
        public string DisplayName;
        public string Description;
        public string Metadata;
    }
}
