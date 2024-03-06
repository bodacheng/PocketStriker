using System.Collections.Generic;

// 程序目标
// 1. 模拟我们赔了的钱是去哪里了，以及银行如何从散户的外汇交易中获得利益

namespace FXKnowledge
{
    class Country
    {
        private string key;
        private Bank bank;
        private CenterBank _centerBank;
        private List<Trader> traders;
    }
    
    // 存储银行
    class Bank
    {
        private double amount;
        private float interest_rate;
    }
    
    class CenterBank
    {
        private IDictionary<string, double> fxWallet = new Dictionary<string, double>();
        private float interest_rate;
        private float stock_rate;
        
        public float ExchangeRate(string useKey, string targetKey)
        {
            return 0.6f;
        }
    }
    
    class Trader
    {
        /// <summary>
        /// 购买外汇的话你手上不同的钱按理说是在不同国家的银行里，否则没法解释隔夜利息这一说
        /// </summary>
        private IDictionary<string, double> wallet = new Dictionary<string, double>();
        
        void FXTrade(string useKey, string targetKey, double target_amount, CenterBank centerBank)
        {
            double CurrentM = wallet[useKey];
            double CurrentT = wallet[targetKey];
            float exchangeRate = centerBank.ExchangeRate(useKey, targetKey);
            double useMoneyM = target_amount / exchangeRate;
            wallet[useKey] = CurrentM - useMoneyM;
            wallet[targetKey] = CurrentT + target_amount;
        }
    }
    
    public class EconomicWorld
    {
        private Country CountryA, CountryB;
    }
}
