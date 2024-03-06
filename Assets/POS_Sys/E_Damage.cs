namespace HittingDetection
{
    public class E_Damage
    {
        FightParamsReference Attacker_Health;
        FightParamsReference Damaged_Health;
        public Position_set Position_set;
        
        public E_Damage() { }
        public E_Damage(FightParamsReference Attacker_Health, Position_set Position_set)
        {
            this.Attacker_Health = Attacker_Health;
            this.Position_set = Position_set;
        }
        
        public FightParamsReference GetAttackerHealthBody()
        {
            return Attacker_Health;
        }
        
        public void SetAttackerHealthBody(FightParamsReference BO_Health)
        {
            Attacker_Health = BO_Health;
        }
        
        public void SetDamagedHealthBody(FightParamsReference b)
        {
            Damaged_Health = b;
        }
        
        public FightParamsReference GetDamagedHealthBody()
        {
            return Damaged_Health;
        }
    }
}
