namespace mainMenu
{
    public abstract class MSceneProcess : SceneProcess
    {
        public MainSceneStep Step;
        protected MissionWatcher missionWatcher;
        private bool _loaded = false;
        
        protected void SetLoaded(bool value)
        {
            _loaded = value;
        }
        public bool GetLoaded()
        {
            return _loaded;
        }
        
        public override bool CanEnterOtherProcess()
        {
            return _loaded;
        }
    }

    public enum MainSceneStep
    {
        None = 0,
        Setting = -1,
        MailBox = 10,
        MailDetail = 11,
        FrontPage = 1,
        SelfFightFront = 4,
        TeamEditFront = 2,
        UnitList = 5,
        UnitSkillEdit = 16,
        SkillStoneList = 15,
        StoneUpdateConfirm = 14,
        SkillStones_Sell = 100,
        GotchaFront = 6,
        GotchaResult = 24,
        DropTableInfo = 26,
        Ranking = 25,
        ArenaAward = 27,
        
        ShopTop = 201,

        QuestInfo = 8,
        ArcadeFront = 9,
        GangBangFront = 13,
        Arena = 3
    }
}