namespace MCombat.Shared.AI
{
    public sealed class AiDecisionThrottle
    {
        int _decisionDelayCount;

        public int DecisionDelay { get; set; }

        public bool ShouldRun(AIMode mode)
        {
            if (mode == AIMode.Aggressive)
            {
                return true;
            }

            _decisionDelayCount++;
            if (_decisionDelayCount > DecisionDelay)
            {
                _decisionDelayCount = 0;
            }

            return _decisionDelayCount == 0;
        }

        public void Reset()
        {
            _decisionDelayCount = 0;
        }
    }
}
