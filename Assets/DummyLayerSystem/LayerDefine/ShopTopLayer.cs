using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace mainMenu
{
    public class ShopTopLayer : UILayer
    {
        [SerializeField] AdsBtnRender adsBtnRender;
        [SerializeField] ScrollRect productParent;
        [SerializeField] ProductCell[] stoneBundleProductCells;
        [SerializeField] ProductCell noAdsCell;
        [SerializeField] float cellHeight = 450;
        [SerializeField] TimeLimitedBundleCell timeLimitedBundleCell;
        
        public void Initialize()
        {
            ShowNoAdsProduct();
            adsBtnRender.SetupForMainScene();
            RefreshSize();
        }

        public void ShowTimeLimitedBundle()
        {
            timeLimitedBundleCell.ShowTimeLimitedBundle(PlayFabReadClient.TimeLimitedBuyData);
            RefreshSize();
        }

        public void ShowNoAdsProduct()
        {
            noAdsCell.gameObject.SetActive(!PlayerAccountInfo.Me.noAdsState);
        }

        void RefreshSize()
        {
            var rectTransform = productParent.content.GetComponent<RectTransform>();
            int activeChildCount = 0;
            // 遍历所有子物体
            for (int i = 0; i < rectTransform.childCount; i++)
            {
                Transform child = rectTransform.GetChild(i);

                // 检查子物体的激活状态
                if (child.gameObject.activeInHierarchy)
                {
                    activeChildCount++;
                }
            }
            productParent.content.sizeDelta = new Vector2(productParent.content.sizeDelta.x, cellHeight * activeChildCount);
            productParent.horizontalNormalizedPosition = 0;
        }

        public void ShowStoneBundle(List<string> showTargetProductIds)
        {
            foreach (var productCell in stoneBundleProductCells)
            {
                productCell.gameObject.SetActive(showTargetProductIds.Contains(productCell.productId));
            }
            RefreshSize();
        }

        public void DisableStoneBundle(string productId)
        {
            foreach (var productCell in stoneBundleProductCells)
            {
                if (productId == productCell.productId)
                    productCell.gameObject.SetActive(false);
            }
            RefreshSize();
        }
    }
}