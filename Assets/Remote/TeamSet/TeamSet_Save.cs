using System;
using Newtonsoft.Json;
using PlayFab.ClientModels;
using System.Collections.Generic;

namespace dataAccess
{
    public static partial class TeamSet
    {
        // 缺乏合理验证环节
        public static void SaveTeamSet(string Mode, Action<bool> success)
        {
            var form = new TeamPos();
            switch (Mode)
            {
                case "arcade":
                    form.f = Default.GetInstanceIdOnPos(0);
                    form.l = Default.GetInstanceIdOnPos(1);
                    form.r = Default.GetInstanceIdOnPos(2);
                    break;
                case "gangbang":
                    form.f = Gangbang.GetInstanceIdOnPos(0);
                    form.l = Gangbang.GetInstanceIdOnPos(1);
                    form.r = Gangbang.GetInstanceIdOnPos(2);
                    break;
                case "arena":
                    return; // arena模式的队伍编辑不再和arcade相同
                    form.f = Arena3V3.GetInstanceIdOnPos(0);
                    form.l = Arena3V3.GetInstanceIdOnPos(1);
                    form.r = Arena3V3.GetInstanceIdOnPos(2);
                    break;
            }
            
            var targetModeCode = "";
            switch (Mode)
            {
                case "arcade":
                    targetModeCode = "arcade";
                    break;
                case "arena":
                    targetModeCode = "arena";
                    break;
                case "gangbang":
                    targetModeCode = "gangbang";
                    break;
            }
            PlayFabReadClient.UpdateUserData(
                new UpdateUserDataRequest()
                {
                    Data = new Dictionary<string, string>()
                    {
                        { targetModeCode, JsonConvert.SerializeObject(form) }
                    }
                },
                ()=>success(true),
                ()=>success(false)
            );
        }
    }
}