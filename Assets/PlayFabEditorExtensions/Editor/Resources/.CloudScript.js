///////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Welcome to your first Cloud Script revision!
//
// Cloud Script runs in the PlayFab cloud and has full access to the PlayFab Game Server API 
// (https://api.playfab.com/Documentation/Server), and it runs in the context of a securely
// authenticated player, so you can use it to implement logic for your game that is safe from
// client-side exploits. 
//
// Cloud Script functions can also make web requests to external HTTP
// endpoints, such as a database or private API for your title, which makes them a flexible
// way to integrate with your existing backend systems.
//
// There are several different options for calling Cloud Script functions:
//
// 1) Your game client calls them directly using the "ExecuteCloudScript" API,
// passing in the function name and arguments in the request and receiving the 
// function return result in the response.
// (https://api.playfab.com/Documentation/Client/method/ExecuteCloudScript)
// 
// 2) You create PlayStream event actions that call them when a particular 
// event occurs, passing in the event and associated player profile data.
// (https://api.playfab.com/playstream/docs)
// 
// 3) For titles using the Photon Add-on (https://playfab.com/marketplace/photon/),
// Photon room events trigger webhooks which call corresponding Cloud Script functions.
// 
// The following examples demonstrate all three options.
//
///////////////////////////////////////////////////////////////////////////////////////////////////////

//var playstreamEvent = context.playStreamEvent;
//log.debug(playstreamEvent.Entity);
//var currentPlayerId = playstreamEvent.Entity.Id;

handlers.BundleBought = function (args, context) {

    const key = args.bundleProductId;
    
    var updateUserDataResult = server.UpdateUserReadOnlyData({
        PlayFabId: currentPlayerId,
        Data: {
            [key]: true
        }
    });
}

handlers.GrantContainerIfNotOwnedFromConsole = function (args, context) {
    var playFabItemCategory = args.itemCategory;
    var playFabItemId = args.checkitemId;
    var playFabContainerItemId = args.giveContainerItemId;

    var request = {
        "PlayFabId": currentPlayerId
    };
    
    // 获取玩家的物品列表
    var inventoryResult = server.GetUserInventory(request);
    
    // 检查玩家是否已经拥有了这个物品
    var playerHasItem = false;
    for (var i = 0; i < inventoryResult.Inventory.length; i++) {

        //log.info("playFabItemCategory:"+ inventoryResult.Inventory[i].CatalogVersion);
        if ((inventoryResult.Inventory[i].ItemId == playFabItemId || inventoryResult.Inventory[i].ItemId == playFabContainerItemId)
            &&
            inventoryResult.Inventory[i].CatalogVersion == playFabItemCategory)
        {
            playerHasItem = true;
            break;
        }
    }
    // 如果玩家没有这个物品，发放物品
    if (!playerHasItem) {
        var GrantItemsToUserRequest = {
            "CatalogVersion": playFabItemCategory,
            "PlayFabId" : currentPlayerId,
            "ItemIds" : [playFabContainerItemId]
        };
        server.GrantItemsToUser(GrantItemsToUserRequest);
    }
    return { itemGranted: !playerHasItem };
};

function GrantItemIfNotOwnedByUser(itemId, playFabItemCategory) {
    
    var checkRequest = {
        "PlayFabId": currentPlayerId
    };

    // 获取玩家的物品列表
    var inventoryResult = server.GetUserInventory(checkRequest);

    // 检查玩家是否已经拥有了这个物品
    var playerHasItem = false;
    for (var i = 0; i < inventoryResult.Inventory.length; i++) {
        if (inventoryResult.Inventory[i].ItemId == itemId && inventoryResult.Inventory[i].CatalogVersion == playFabItemCategory)
        {
            playerHasItem = true;
            break;
        }
    }
    // 如果玩家没有这个物品，发放物品
    if (!playerHasItem) {
        var GrantItemsToUserRequest = {
            "CatalogVersion": playFabItemCategory,
            "PlayFabId" : currentPlayerId,
            "ItemIds" : [itemId]
        };
        var GrantItemsToUserResult = server.GrantItemsToUser(GrantItemsToUserRequest);
        return GrantItemsToUserResult.ItemGrantResults;
    }
    return { };
}

function GrantItemsIfNotOwnedByUser(itemIds, playFabItemCategory) {

    var checkRequest = {
        "PlayFabId": currentPlayerId
    };

    // 获取玩家的物品列表
    var inventoryResult = server.GetUserInventory(checkRequest);

    // 创建一个集合用于存放玩家已经拥有的物品ID
    var ownedItems = new Set();
    for (var i = 0; i < inventoryResult.Inventory.length; i++) {
        if (inventoryResult.Inventory[i].CatalogVersion == playFabItemCategory) {
            ownedItems.add(inventoryResult.Inventory[i].ItemId);
        }
    }

    // 检查要发放的物品是否已经被玩家拥有，如果没有，将其添加到要发放的物品列表中
    var itemsToGrant = [];
    for (var i = 0; i < itemIds.length; i++) {
        if (!ownedItems.has(itemIds[i])) {
            itemsToGrant.push(itemIds[i]);
        }
    }

    // 如果存在需要发放的物品，发放物品
    if (itemsToGrant.length > 0) {
        var GrantItemsToUserRequest = {
            "CatalogVersion": playFabItemCategory,
            "PlayFabId" : currentPlayerId,
            "ItemIds" : itemsToGrant
        };
        var GrantItemsToUserResult = server.GrantItemsToUser(GrantItemsToUserRequest);
        return GrantItemsToUserResult.ItemGrantResults;
    }

    return { };
}

function GrantItemToCurrentUser(itemIds, CatalogVersion)
{
    var GrantItemsToUserRequest = {
        "CatalogVersion": CatalogVersion,
        "PlayFabId" : currentPlayerId,
        "ItemIds" : itemIds
    };
    
    var GrantItemsToUserResult = server.GrantItemsToUser(GrantItemsToUserRequest);
    return GrantItemsToUserResult.ItemGrantResults;
}

function GrantItemToCurrentUserAndSetCustomData(itemIds, CatalogVersion, customData)
{
    var GrantItemsToUserRequest = {
        "CatalogVersion": CatalogVersion,
        "PlayFabId" : currentPlayerId,
        "ItemIds" : itemIds
    };
    
    var GrantItemsToUserResult = server.GrantItemsToUser(GrantItemsToUserRequest);

    if (customData) {
        for (var i = 0; i < GrantItemsToUserResult.ItemGrantResults.length; i++) {
            var itemInstanceId = GrantItemsToUserResult.ItemGrantResults[i].ItemInstanceId;
            var UpdateUserInventoryItemDataRequest = {
                "PlayFabId": currentPlayerId,
                "ItemInstanceId": itemInstanceId,
                "Data": customData
            };
            server.UpdateUserInventoryItemCustomData(UpdateUserInventoryItemDataRequest);
        }
    }
    
    //log.info(GrantItemsToUserResult);
    return GrantItemsToUserResult.ItemGrantResults;
}

handlers.advertisementReward = function (args, context) {
    
    if (args.stage != undefined) {

        var newLevelCompleted = Number(args.stage);
        
        // Get the player's existing level ad status data.
        var playerData = server.GetUserReadOnlyData({
            PlayFabId: currentPlayerId,
            Keys: ["LevelAdStatus"]
        });

        // Initialize the LevelAdStatus data if it doesn't exist.
        var levelAdStatus;
        if (!playerData.Data.LevelAdStatus) {
            levelAdStatus = [];
        } else {
            // Parse the LevelAdStatus data into an array.
            levelAdStatus = JSON.parse(playerData.Data.LevelAdStatus.Value || '[]');
        }

        // If the level index is out of range, expand the levelAdStatus array.
        while(newLevelCompleted > levelAdStatus.length) {
            levelAdStatus.push(null);
        }

        // Update the level ad status.
        levelAdStatus[newLevelCompleted - 1] = 1;

        // Save the updated level ad status data.
        server.UpdateUserReadOnlyData({
            PlayFabId: currentPlayerId,
            Data: { "LevelAdStatus": JSON.stringify(levelAdStatus) }
        });
    }
    
    var result = server.AddUserVirtualCurrency(
        {
            PlayFabId :currentPlayerId,
            Amount : args.Amount,
            VirtualCurrency : args.VirtualCurrency
        }
    );
    return { result };
}

handlers.setNoAdsStatus = function(args, context) {
    var playerId = currentPlayerId;

    // 检查验证是否成功，这可以通过传递的参数来判断
    var isVerified = args.isVerified;

    if(isVerified) {
        // 如果验证成功，设置IsVIP为true
        server.UpdateUserData({
            PlayFabId: playerId,
            Data: {"noAds": "1"}
        });
        
        var userSkippedAdRewards = GetUserSkippedAdRewards();
        if (userSkippedAdRewards > 0) {
            var AddUserVirtualCurrencyResult = server.AddUserVirtualCurrency(
                {
                    PlayFabId :currentPlayerId,
                    Amount : userSkippedAdRewards,
                    VirtualCurrency : "DM"
                }
            );
            
            return { rewardDM : userSkippedAdRewards };
        }
    }
    return { rewardDM : 0 };
};

function GetUserSkippedAdRewards() {
    // 获取玩家现有的关卡广告状态数据
    var playerData = server.GetUserReadOnlyData({
        PlayFabId: currentPlayerId,
        Keys: ["LevelAdStatus", "GangbangLevelAdStatus"]
    });
    
    var rewardDM = 0;
    
    if (playerData.Data.LevelAdStatus) {
        // 将LevelAdStatus数据解析为数组
        var levelAdStatus = JSON.parse(playerData.Data.LevelAdStatus.Value || '[]');
        // 遍历levelAdStatus数组，并根据其值计算rewardDM
        for (var index = 0; index < levelAdStatus.length; index++) {
            if (levelAdStatus[index] == 0 || levelAdStatus[index] == "null" || levelAdStatus[index] == null) {
                // 每五个关卡奖励10点，其他奖励5点
                rewardDM += (index + 1) % 5 == 0 ? 10 : 5;
            }
        }
    }
    
    if (playerData.Data.GangbangLevelAdStatus) {
        // 将LevelAdStatus数据解析为数组
        var gangbangLevelAdStatus = JSON.parse(playerData.Data.GangbangLevelAdStatus.Value || '[]');
        // 遍历levelAdStatus数组，并根据其值计算rewardDM
        for (var index = 0; index < gangbangLevelAdStatus.length; index++) {
            if (gangbangLevelAdStatus[index] == 0 || gangbangLevelAdStatus[index] == "null" || gangbangLevelAdStatus[index] == null) {
                // 每五个关卡奖励10点，其他奖励5点
                rewardDM += (index + 1) % 5 == 0 ? 10 : 5;
            }
        }
    }
    
    // 返回计算得到的奖励值
    return rewardDM;
}

handlers.GetSkippedAdRewards = function (args, context) {
    var rewardDM = GetUserSkippedAdRewards();
    return { rewardDM: rewardDM };
}

handlers.SubtractVirtualCurrency = function (args, context) {
    var result = server.SubtractUserVirtualCurrency(
        {
            PlayFabId :currentPlayerId,
            Amount : args.Count,
            VirtualCurrency : args.VirtualCurrencyCode
        }
    );
    return { result };
}

// 给予dev用户基本财产
handlers.grantDevItems = function (args, context) {
    
    var AddUserVirtualCurrencyResult = server.AddUserVirtualCurrency(
        {
            PlayFabId :currentPlayerId,
            Amount : 99999,
            VirtualCurrency : "DM"
        }
    );

    var AddUserVirtualCurrencyResult = server.AddUserVirtualCurrency(
        {
            PlayFabId :currentPlayerId,
            Amount : 99999,
            VirtualCurrency : "GD"
        }
    );

    var catalogItems = server.GetCatalogItems(
        {
            CatalogVersion : "stone"
        }
    );

    let stoneIds = [];
    for (let i = 0; i < catalogItems.Catalog.length; i++) {
        var itemInfo = catalogItems.Catalog[i];
        if (itemInfo.Bundle == null && itemInfo.Container == null)
            stoneIds.push(itemInfo.ItemId);
    }
    var stoneResult = GrantItemsIfNotOwnedByUser(stoneIds , "stone");
    var unitResult = GrantItemsIfNotOwnedByUser(["1","2","4","5","6","7"] , "unit");
    
    return { result: true };
};

// 给予玩家基本财产
handlers.grantBasicItems = function (args, context) {
    
    var AddUserVirtualCurrencyResult = server.AddUserVirtualCurrency(
        {
            PlayFabId :currentPlayerId,
            Amount : args.DM,
            VirtualCurrency : "DM"
        }
    );
    
    var GrantedItems = GrantItemToCurrentUser(args.unit_ids, "unit");
    var GrantedStones = GrantItemToCurrentUser(args.stone_ids, "stone");
    
    return { result: true };
};

// 将被动技能给予角色
// 是控制台内grantitem的附属执行函数
handlers.givePassiveSkill= function (args, context) {

    var request = {
        "PlayFabId": currentPlayerId
    };
    
    var playstreamEvent = context.playStreamEvent;
    var unit_InstanceId = playstreamEvent.InstanceId;
    
    let itemIds = [];
    itemIds.push(args.skill_id);
    var grantResult = GrantItemToCurrentUser(itemIds, "stone");
    
    // 虽然是for循环体但其实只执行一圈
    for (let i = 0; i < grantResult.length; i++)
    {
        var got = grantResult[i];
        var request = {
            "PlayFabId": currentPlayerId,
            "ItemInstanceId": got.ItemInstanceId,
            "Data":  {
                "unitInstanceId": unit_InstanceId,
                "slot": 1,
                "level": 1,
                "born" : true
            }
        };
        server.UpdateUserInventoryItemCustomData(request);
    }
    
    return { result : true };
}

handlers.completedLevel = function (args, context) {
    
    var newLevelCompleted = Number(args.stage);
    var stageType = args.stageType;
    var stageProgressKey = stageType === "gangbang" ? "gangbangProgress" : "stageProgress";
    var playerStatResult = server.UpdatePlayerStatistics(
        {
            PlayFabId: currentPlayerId,
            Statistics: [
                {
                    StatisticName: stageProgressKey,
                    Value: newLevelCompleted
                }
            ]
        }
    );

    var unit_award = -1;
    if (stageType !== "gangbangProgress") {
        switch (newLevelCompleted) {
            case 1:
                unit_award = "1";
                break;
            case 5:
                unit_award = "2";
                break;
            case 20:
                unit_award = "4";
                break;
            case 35:
                unit_award = "7";
                break;
            case 50:
                unit_award = "6";
                break;
            case 100:
                unit_award = "5";
                break;
            default:
                break;
        }
    }
    
    if (unit_award != -1) {
        var award_unit = GrantItemIfNotOwnedByUser(unit_award, "unit");
        return { award_unit };
    }else{
        return null;
    }
};

handlers.claimQuestReward = function (args, context) {
    
    // 传递过来的这个level是玩家试图更新到的进度，但这个数值来自客户端，并不能完全信任
    // 关卡更新机制我们只有一个逻辑就是一次只更新一关

    var stageType = args.stageType;
    var isVip = args.isVip;
    var stageAwardsKey = stageType === "gangbang" ? "gangbang_awards" : "stage_awards";
    var adStatusKey = stageType === "gangbang" ? "GangbangLevelAdStatus" : "LevelAdStatus";
    
    var level = args.level;
    var newLevelCompleted = Number(args.stage);
    var titleDataRequest = { "Keys": stageAwardsKey };
    var titleDataResponse = server.GetTitleData(titleDataRequest);

    var whole = titleDataResponse.Data[stageAwardsKey];
    var objData = JSON.parse(whole);
    var award;
    let g = 0;
    var d = 0;

    for (var i = 0; i < objData.length; i++) {

        if (objData[i].stageKey == newLevelCompleted){
            if(objData[i].hasOwnProperty("award")){
                award = objData[i].award;
                break;
            }
        }
    }

    if (award === undefined) {
        return {
            has_reward: false
        };
    }

    if (award.hasOwnProperty("g")) {
        g = Number(award.g);
    }

    if (award.hasOwnProperty("d")) {
        d = Number(award.d);
    }
    
    // Get the player's existing level ad status data.
    var playerData = server.GetUserReadOnlyData({
        PlayFabId: currentPlayerId,
        Keys: [adStatusKey]
    });

    // Initialize the LevelAdStatus data if it doesn't exist.
    var levelAdStatus;
    if (adStatusKey === "LevelAdStatus") {
        if (!playerData.Data.LevelAdStatus) {
            levelAdStatus = [];
        } else {
            // Parse the LevelAdStatus data into an array.
            levelAdStatus = JSON.parse(playerData.Data.LevelAdStatus.Value || '[]');
        }
    }else{
        if (!playerData.Data.GangbangLevelAdStatus) {
            levelAdStatus = [];
        } else {
            // Parse the LevelAdStatus data into an array.
            levelAdStatus = JSON.parse(playerData.Data.GangbangLevelAdStatus.Value || '[]');
        }
    }
    
    // If the level index is out of range, expand the levelAdStatus array.
    while(newLevelCompleted > levelAdStatus.length) {
        levelAdStatus.push(null);
    }
    
    // Update the level ad status.
    if (levelAdStatus[newLevelCompleted - 1] === null) {
        levelAdStatus[newLevelCompleted - 1] = 0;
        if (isVip !== undefined){
            if (isVip === true) {
                var extraD = newLevelCompleted % 5 === 0 ? 10 : 5,
                d = extraD + d;
            }
        }
    }
    
    // Save the updated level ad status data.
    var newPlayData = server.UpdateUserReadOnlyData({
        PlayFabId: currentPlayerId,
        Data: {[adStatusKey]:JSON.stringify(levelAdStatus)}
    });
    
    if (g > 0) {
        server.AddUserVirtualCurrency({
            PlayFabID: currentPlayerId,
            VirtualCurrency: "GD",
            Amount: g
        });
    }
    
    if (d > 0) {
        server.AddUserVirtualCurrency({
            PlayFabID: currentPlayerId,
            VirtualCurrency: "DM",
            Amount: d
        });
    }
    
    return {
        has_reward: true,
        gold: g,
        diamond: d
    };
};

// 技能石背包只能10个10个的往上买。但是必须应该有一个最大值。这个数字是多少要看这游戏是个什么感觉
handlers.expandBox10 = function (args, context) {
    
    var playerData = server.GetUserReadOnlyData({
        PlayFabId: currentPlayerId,
        Keys: ["stone_box_size"]
    });
    var StoneBoxSize = Number(playerData.Data["stone_box_size"].Value) + Number(10);
    StoneBoxSize = Math.min(Math.max(StoneBoxSize, 0), 200);// 假设最大尺寸是200
    var updateUserDataResult = server.UpdateUserReadOnlyData({
        PlayFabId: currentPlayerId,
        Data: {
            "stone_box_size": StoneBoxSize,
        }
    });
    
    return StoneBoxSize;
};

handlers.claimAllPresentMails = function (args, context) {
    var request = {
        "PlayFabId": currentPlayerId
    };
    var items = server.GetUserInventory(request);
    
    let UnlockedList = [];
    var allDM = 0;
    var allGD = 0;
    for (var i = 0; i < items.Inventory.length; i++) {
        var item = items.Inventory[i];
        if (item.CatalogVersion == "Present")
        {
            var UnlockContainerItemRequest = {
                "PlayFabId": currentPlayerId,
                "CatalogVersion" : item.CatalogVersion,
                "ContainerItemId" : item.ItemId
            };
            var result = server.UnlockContainerItem(UnlockContainerItemRequest);
            if (result.VirtualCurrency.DM) {
                allDM += result.VirtualCurrency.DM;
            }
            if (result.VirtualCurrency.GD) {
                allGD += result.VirtualCurrency.GD;
            }
            UnlockedList.push(result.UnlockedItemInstanceId);
        }
    }
    return {
        diamond: allDM,  
        gold: allGD, 
        UnlockedItemInstanceIds : UnlockedList
    };
}

function between(x, min, max) {
    return x >= min && x <= max;
}

handlers.skillEdit = function (args, context) {

    var inventoryRequest = {
        "PlayFabId": currentPlayerId
    };
    
    
    let memo = [];

    // var items = server.GetUserInventory(inventoryRequest);
    // var legal = true;
    // // 检查是否有被动技能被卸载
    // for (let i = 0; i < args.inputValue.length; i++) {
    //     var requestItem = args.inputValue[i];
    //     for (var y = 0; y < items.Inventory.length; y++) {
    //         var item = items.Inventory[y];
    //         if (item.CatalogVersion === "stone" && requestItem.ItemInstanceId === item.ItemInstanceId)
    //         {
    //             if (item.CustomData.hasOwnProperty("born"))
    //             {
    //                 if (Boolean(item.CustomData.born) == true) {
    //                     legal = between(Number(requestItem.Data.slot), 1, 9);
    //                 }
    //                 if (legal == false) {
    //                     return { changedStone: memo };
    //                 }
    //             }
    //         }
    //     }
    // }
    
    for (let i = 0; i < args.inputValue.length; i++) {
        var requestItem = args.inputValue[i];
        var request = {
            "PlayFabId": currentPlayerId,
            "ItemInstanceId": requestItem.ItemInstanceId,
            "Data": requestItem.Data
        };
        var result = server.UpdateUserInventoryItemCustomData(request);
        memo.push(
            {
                "InstanceId": requestItem.ItemInstanceId, 
                "slot": requestItem.Data.slot,
                "unitInstanceId":requestItem.Data.unitInstanceId
            }
        );
    }
    return { changedStone: memo };
}

handlers.updateStone = function (args, context) {

    var targetStone;
    
    var inventoryRequest = {
        "PlayFabId": currentPlayerId
    };
    var items = server.GetUserInventory(inventoryRequest);

    let currentLv = 0;
    for (var i = 0; i < items.Inventory.length; i++) {
        var item = items.Inventory[i];
        if (item.CatalogVersion === "stone" && item.ItemInstanceId === args.targetItemInstanceId)
        {
            targetStone = item;
            if ('level' in item.CustomData)
            {
                currentLv = Number(item.CustomData.level);
            }else{
                currentLv = 1;
            }
        }
    }
    
    var GD = Number(items.VirtualCurrency["GD"]);
    
    // 是否有足够的金币升级？
    
    let needGD = args.needGD ? Number(args.needGD) : 10;//这是升级到1.4.0版本阶段的一个过度写法
    if (GD < needGD) {
        return {  success: false, stoneId: args.target, level:0};
    }
    currentLv = 1 + currentLv;
    var SubtractUserVirtualResult = server.SubtractUserVirtualCurrency(
        {
            PlayFabId :currentPlayerId,
            Amount : needGD,
            VirtualCurrency : "GD"
        }
    );
    
    var revokeRequest = {
        "PlayFabId": currentPlayerId,
        "Items": args.resources,
    };
    server.RevokeInventoryItems(revokeRequest);
    
    if (targetStone !== null) {
        targetStone.CustomData["level"] = currentLv;
        var levelUpRequest = {
            "PlayFabId": currentPlayerId,
            "ItemInstanceId": targetStone.ItemInstanceId,
            "Data": targetStone.CustomData
        };
        var updateResult = server.UpdateUserInventoryItemCustomData(levelUpRequest);
        return { success: true, stoneId: targetStone.ItemInstanceId, level:currentLv };
    }
    
    return {  success: false, stoneId: args.target, level:0};
}

handlers.ArenaDefendTeamSave = function (args, context) {
    
    let members = [];
    if (args.Team == null) {
        return { success: false };
    }
    
    for (let i = 0; i < args.Team.length; i++) {
        var item = args.Team[i];
        members.push(item);
    }
    
    if (members.length != 3) {
        return { success: false  };
    }
    
    var request = {
        "PlayFabId": currentPlayerId,
        "Data": {
            "DefendTeam": JSON.stringify(members)
        }
    };
    var Result = server.UpdateUserData(request);

    var getRequest = {
        PlayFabId: currentPlayerId,
        StatisticNames: ["arenapoint"],
    };
    var playerStats = server.GetPlayerStatistics(getRequest);
    let arenapoint = -999;
    for (i = 0; i < playerStats.Statistics.length; ++i) {
        if (playerStats.Statistics[i].StatisticName === "arenapoint") {
            arenapoint = playerStats.Statistics[i].Value;
        }
    }
    
    if (arenapoint == -999) {
        var playerStatResult = server.UpdatePlayerStatistics(
            {
                PlayFabId: currentPlayerId,
                Statistics: [
                    {
                        StatisticName: "arenapoint",
                        Value: 0
                    }
                ]
            }
        );
        return {
            success: true,
            messageValue: members,
            arenapoint : 0
        };
    }
    
    return {
        success: true,
        messageValue: members
    };
}

function arenaPlusPoint(mePosition, opponentPosition, mePoint, opponentPoint) {
    let plusPoint = 0;
    if (opponentPosition - mePosition >= 50)
    {
        plusPoint = clamp(opponentPoint - mePoint, 50, 60);
    }
    else
    {
        plusPoint = clamp(opponentPoint - mePoint, 40, 50);
    }
    return plusPoint;
}

function AddTeamInfoItem(Leaderboard, mePosition, mePoint) {
    
    var playerTeamData = server.GetUserData(
        {
            PlayFabId: Leaderboard.PlayFabId,
            Keys: ["DefendTeam","OneWord"]
        }
    );
    
    // 玩家可能未曾保存过防御队伍阵容，对这种玩家不返回。
    if (playerTeamData.Data["DefendTeam"] != null) {
        var item = {
            "PlayerLeaderboardEntry": Leaderboard,
            "Team": JSON.parse(playerTeamData.Data["DefendTeam"].Value),
            "OneWord": playerTeamData.Data["OneWord"] != null ? playerTeamData.Data["OneWord"].Value : "",
            "plusPoint": arenaPlusPoint(mePosition, Leaderboard.Position, mePoint, Leaderboard.StatValue)
        };
        return item;
    }
    return null;
}

function ArrangeMyTeamInfoItem(Leaderboard, DefendTeam, OneWord) {
    var item = {
        "PlayerLeaderboardEntry": Leaderboard,
        "Team": JSON.parse(DefendTeam),
        "OneWord":OneWord,
        "plusPoint":""
    };
    return item;
}

handlers.GetLeaderboardAroundUser = function (args, context) {

    let teamInfos = [];
    var myTeamData = server.GetUserData(
        {
            PlayFabId: currentPlayerId,
            Keys: ["DefendTeam","OneWord"]
        }
    );

    if (myTeamData.Data["DefendTeam"] == null){
        return { teamInfos };
    }
    
    var request = {
        "PlayFabId": currentPlayerId,
        "MaxResultsCount": 4,
        "StatisticName": "arenapoint",
        "ProfileConstraints" : {
            "ShowDisplayName" : true
        }
    };
    
    var result = server.GetLeaderboardAroundUser(request);
    var myTeam;
    for (let i = 0; i < result.Leaderboard.length; i++) {
        if (result.Leaderboard[i].PlayFabId == currentPlayerId){
            myTeam = ArrangeMyTeamInfoItem(result.Leaderboard[i], myTeamData.Data["DefendTeam"].Value, myTeamData.Data["OneWord"] != null ? myTeamData.Data["OneWord"].Value : "");
            if (myTeam != null) {
                teamInfos.push(myTeam);
            }
        }
    }
    
    for (let i = 0; i < result.Leaderboard.length; i++) {
        if (result.Leaderboard[i].PlayFabId != currentPlayerId) {
            var item = AddTeamInfoItem(result.Leaderboard[i], myTeam.PlayerLeaderboardEntry.Position, myTeam.PlayerLeaderboardEntry.StatValue);
            if (item != null) {
                teamInfos.push(item);
            }
        }
    }
    
    // 该返回值内存在元素重复的可能(玩家很少情况下，higherPlayer已经在server.GetLeaderboardAroundUser结果内)
    return { teamInfos };
}

function clamp(value, min, max) {
    if (value <= min) {
        return min;
    } else if (value >= max) {
        return max;
    } else
    return value;
}

function clampMin(value, min) {
    if (value < min) {
        return min;
    }else
        return value;
}

function clampMax(value, max) {
    if (value > max) {
        return max;
    }else
        return value;
}

handlers.GetLeaderboard = function (args, context) {

    var request = {
        "MaxResultsCount": 20, 
        "StatisticName": "arenapoint",
        "ProfileConstraints" : {
            "ShowDisplayName" : true
        },
        "StartPosition" : 0
    };
    var Result = server.GetLeaderboard(request);
    let teamInfos = [];

    for (let i = 0; i < Result.Leaderboard.length; i++) {

        var playerTeamData = server.GetUserData(
            {
                PlayFabId: Result.Leaderboard[i].PlayFabId,
                Keys: ["DefendTeam"]
            }
        );

        // 玩家可能未曾保存过防御队伍阵容，对这种玩家不返回。
        if (playerTeamData.Data["DefendTeam"] != null) {
            var item = {
                "PlayerLeaderboardEntry": Result.Leaderboard[i],
                "Team": JSON.parse(playerTeamData.Data["DefendTeam"].Value)
            };
            teamInfos.push(item);
        }
    }

    return { teamInfos };
}

// 根据自己的分数和对手的分数来判断接下来应该的竞技场加分。
// 自己的分数和对手的分数都是客户端传来的。图省事。
handlers.ArenaPointUp = function (args, context) {
    var getRequest = {
        PlayFabId: currentPlayerId
    };
    
    let mePosition = args.mePosition;
    let opponentPosition = args.opponentPosition;
    let mePoint = args.mePoint;
    let opponentPoint = args.opponentPoint;
    let plusPoint = arenaPlusPoint(mePosition, opponentPosition, mePoint, opponentPoint);
    let shouldPoint = mePoint + plusPoint;
    var playerStatResult = server.UpdatePlayerStatistics({
        PlayFabId: currentPlayerId,
        Statistics: [{
            StatisticName: "arenapoint",
            Value: shouldPoint
        }]
    });
    
    return {
        "currentPoint" : shouldPoint
    };
};

handlers.RankClear = function (args, context) {

    var getRequest = {
        PlayFabId: currentPlayerId,
        StatisticNames: ["arenapoint"],
    };

    var playerStats = server.GetPlayerStatistics(getRequest);
    let arenapoint = -1;
    for (i = 0; i < playerStats.Statistics.length; ++i) {
        if (playerStats.Statistics[i].StatisticName === "arenapoint") {
            arenapoint = playerStats.Statistics[i].Value;
        }
    }
    
    if (arenapoint == -1) {
        return { arenapoint : arenapoint };
    }
    
    let targetPoint = Math.floor(clampMax(arenapoint * 0.3, 90));
    
    server.UpdatePlayerStatistics({
        PlayFabId: currentPlayerId,
        Statistics: [{
            StatisticName: "arenapoint",
            Value: targetPoint
        }]
    });

    return { arenapoint : targetPoint };
}

handlers.DeleteNoDisplayNameUser = function (args, context) {
    var getRequest = {
        PlayFabId: currentPlayerId
    };
    var accountInfo = server.GetUserAccountInfo(getRequest);
    var displayName = accountInfo.UserInfo.TitleInfo.DisplayName;

    var getStatisticRequest = {
        PlayFabId: currentPlayerId,
        StatisticNames: ["stageProgress"],
    };

    var playerStats = server.GetPlayerStatistics(getStatisticRequest);
    let stageProgress = 100;
    for (i = 0; i < playerStats.Statistics.length; ++i) {
        if (playerStats.Statistics[i].StatisticName === "stageProgress") {
            stageProgress = playerStats.Statistics[i].Value;
        }
    }
    
    if ((displayName == "" || displayName == null) && stageProgress < 3) {
        var deleted = server.DeletePlayer(getRequest);
        return { deleted };
    }
    return {};
}

handlers.stoneDropTableInfo= function (args, context) {
    var TableID = args.TableID;
    var result = server.GetRandomResultTables(
        {
            CatalogVersion : "stone",
            TableIDs : [TableID]
        }
    );
    return { result };
}

handlers.initStoneData = function (args, context) {
    var playstreamEvent = context.playStreamEvent;
    let itemInstanceId = playstreamEvent.InstanceId;
    let itemId = playstreamEvent.ItemId;
    if (itemId === "176" || itemId === "183" || itemId === "90" || itemId === "184" || itemId === "179") // 被动获得被动技能石有其他函数帮助更新数据
    {
        return { };
    }
    
    var request = {
        "PlayFabId": currentPlayerId,
        "ItemInstanceId": itemInstanceId,
        "Data":  {
            "unitInstanceId": null,
            "slot": -1,
            "level": 1
        }
    };
    var result = server.UpdateUserInventoryItemCustomData(request);
    return { result };
}

handlers.sendPassResetMail = function(args, context) {

    var email = args.email;
    var result = server.SendAccountRecoveryEmail(
        {
            Email : email
        }
    );
    return { result };
}

handlers.Remove25Stones = function (args, context) {

    var request = {
        "PlayFabId": currentPlayerId
    };

    var items = server.GetUserInventory(request);

    let toRemove = [];
    var deletedCount = 0;
    for (var i = 0; i < items.Inventory.length; i++) {

        if (items.Inventory[i].CatalogVersion != "stone")
            continue;

        var item = {
            "ItemInstanceId": items.Inventory[i].ItemInstanceId,
            "PlayFabId": currentPlayerId
        };
        toRemove.push(item);
        if ((toRemove.length == 25) || (i == items.Inventory.length - 1)) {
            var deleteRequest = {
                "Items": toRemove
            };
            deletedCount += toRemove.length;
            var Result = server.RevokeInventoryItems(deleteRequest);
            break;
        }
    }
    var currentItemCount = Number(items.Inventory.length) - Number(deletedCount);
    return { currentItemCount: currentItemCount};
}

// This an example of a function that calls a PlayFab Entity API. The function is called using the 
// 'ExecuteEntityCloudScript' API (https://api.playfab.com/documentation/CloudScript/method/ExecuteEntityCloudScript).
handlers.makeEntityAPICall = function (args, context) {

    // The profile of the entity specified in the 'ExecuteEntityCloudScript' request.
    // Defaults to the authenticated entity in the X-EntityToken header.
    var entityProfile = context.currentEntity;

    // The pre-defined 'entity' object has functions corresponding to each PlayFab Entity API,
    // including 'SetObjects' (https://api.playfab.com/documentation/Data/method/SetObjects).
    var apiResult = entity.SetObjects({
        Entity: entityProfile.Entity,
        Objects: [
            {
                ObjectName: "obj1",
                DataObject: {
                    foo: "some server computed value",
                    prop1: args.prop1
                }
            }
        ]
    });

    return {
        profile: entityProfile,
        setResult: apiResult.SetResults[0].SetResult
    };
};

// This is a simple example of making a web request to an external HTTP API.
handlers.makeHTTPRequest = function (args, context) {
    var headers = {
        "X-MyCustomHeader": "Some Value"
    };
    
    var body = {
        input: args,
        userId: currentPlayerId,
        mode: "foobar"
    };

    var url = "http://httpbin.org/status/200";
    var content = JSON.stringify(body);
    var httpMethod = "post";
    var contentType = "application/json";

    // The pre-defined http object makes synchronous HTTP requests
    var response = http.request(url, httpMethod, content, contentType, headers);
    return { responseContent: response };
};

// This is a simple example of a function that is called from a
// PlayStream event action. (https://playfab.com/introducing-playstream/)
handlers.handlePlayStreamEventAndProfile = function (args, context) {
    
    // The event that triggered the action 
    // (https://api.playfab.com/playstream/docs/PlayStreamEventModels)
    var psEvent = context.playStreamEvent;
    
    // The profile data of the player associated with the event
    // (https://api.playfab.com/playstream/docs/PlayStreamProfileModels)
    var profile = context.playerProfile;
    
    // Post data about the event to an external API
    var content = JSON.stringify({ user: profile.PlayerId, event: psEvent.EventName });
    var response = http.request('https://httpbin.org/status/200', 'post', content, 'application/json', null);

    return { externalAPIResponse: response };
};


/////// Check In System

// defining these up top so we can easily change these later if we need to.
var CHECK_IN_TRACKER = "CheckInTracker";    				// used as a key on the UserPublisherReadOnlyData
var PROGRESSIVE_REWARD_TABLE = "ProgressiveRewardTable";	// TitleData key that contains the reward details
var PROGRESSIVE_MIN_CREDITS = "MinStreak";					// PROGRESSIVE_REWARD_TABLE property denoting the minium number of logins to be eligible for this item 
var PROGRESSIVE_REWARD = "Reward";							// PROGRESSIVE_REWARD_TABLE property denoting what item gets rewarded at this level
var TRACKER_NEXT_GRANT = "NextEligibleGrant";				// CHECK_IN_TRACKER property containing the time at which we 
var TRACKER_LOGIN_STREAK = "LoginStreak";					// CHECK_IN_TRACKER property containing the streak length
var login_bonus_catalog = "Present";
var normalDMAward = "normalLoginBonusDM";
var extraDMAward = "sevenDaysLoginBonusDM";

handlers.CheckInExample = function(args) {

    var GetUserReadOnlyDataRequest = {
        "PlayFabId": currentPlayerId,
        "Keys": [ CHECK_IN_TRACKER ]
    };
    var GetUserReadOnlyDataResponse = server.GetUserReadOnlyData(GetUserReadOnlyDataRequest);

    // need to ensure that our data field exists
    var tracker = {}; // this would be the first login ever (across any title), so we have to make sure our record exists.
    if(GetUserReadOnlyDataResponse.Data.hasOwnProperty(CHECK_IN_TRACKER))
    {
        tracker = JSON.parse(GetUserReadOnlyDataResponse.Data[CHECK_IN_TRACKER].Value);
    }
    else
    {
        tracker = ResetTracker();

        // write back updated data to PlayFab
        UpdateTrackerData(tracker);

        log.info("This was your first login, Login tomorrow to get a bonus!");
        return JSON.stringify([]);
    }
    
    if(Date.now() > parseInt(tracker[TRACKER_NEXT_GRANT]))
    {
        // Eligible for an item grant.
        //check to ensure that it has been less than 24 hours since the last grant window opened
        var timeWindow = new Date(parseInt(tracker[TRACKER_NEXT_GRANT]));
        timeWindow.setDate(timeWindow.getDate() + 1); // add 1 day 

        if(Date.now() > timeWindow.getTime())
        {
            // streak ended :(			
            tracker = ResetTracker();
            UpdateTrackerData(tracker);

            log.info("Your consecutive login streak has been broken. Login tomorrow to get a bonus!");
            return JSON.stringify([]);
        }

        // streak continues
        tracker[TRACKER_LOGIN_STREAK] += 1;
        var dateObj = new Date(Date.now());
        dateObj.setDate(dateObj.getDate() + 1); // add one day 
        tracker[TRACKER_NEXT_GRANT] = dateObj.getTime();

        // write back updated data to PlayFab
        log.info("Your consecutive login streak increased to: " + tracker[TRACKER_LOGIN_STREAK]);
        UpdateTrackerData(tracker);

        // Get this title's reward table so we know what items to grant. 
        var GetTitleDataRequest = {
            "Keys": [ PROGRESSIVE_REWARD_TABLE ]
        };
        var GetTitleDataResult = server.GetTitleData(GetTitleDataRequest);

        // ---
        if(!GetTitleDataResult.Data.hasOwnProperty(PROGRESSIVE_REWARD_TABLE))
        {
            log.error("Rewards table could not be found. No rewards will be given. Exiting...");
            return JSON.stringify([]);
        }
        else
        {
            // parse our reward table
            var rewardTable = JSON.parse(GetTitleDataResult.Data[PROGRESSIVE_REWARD_TABLE]);

            // find a matching reward 
            var reward;
            for(var level in rewardTable)
            {
                if( tracker[TRACKER_LOGIN_STREAK] >= rewardTable[level][PROGRESSIVE_MIN_CREDITS])
                {
                    reward = rewardTable[level][PROGRESSIVE_REWARD];
                }
            }

            // make grants and pass info back to the client.
            var grantedItems = [];
            if(reward)
            {
                grantedItems = GrantItems(reward, tracker[TRACKER_LOGIN_STREAK]);
            }
            return JSON.stringify(grantedItems);
        }
    }

    return JSON.stringify([]);
};

// 这个位置存在一个浪费了我们很长时间的天坑我们暂时不知道怎么解释，
// 就是tracker[TRACKER_LOGIN_STREAK]没法直接放在返回值里成功给客户端
// 但是不知道为什么这种值貌似是可以用于比较计算但单独拿出来竟说没定义
handlers.CheckIn = function(args) {

    var loginBonusCustomData = {
        "streak": 1
    }
    
    var GetUserReadOnlyDataRequest = {
        "PlayFabId": currentPlayerId,
        "Keys": [CHECK_IN_TRACKER]
    };
    var GetUserReadOnlyDataResponse = server.GetUserReadOnlyData(GetUserReadOnlyDataRequest);
    // need to ensure that our data field exists
    var tracker = {}; // this would be the first login ever (across any title), so we have to make sure our record exists.
    if(GetUserReadOnlyDataResponse.Data.hasOwnProperty(CHECK_IN_TRACKER))
    {
        tracker = JSON.parse(GetUserReadOnlyDataResponse.Data[CHECK_IN_TRACKER].Value);
    }
    else
    {
        tracker = ResetTracker();
        // write back updated data to PlayFab
        UpdateTrackerData(tracker);
        log.info("This was your first login, Login tomorrow to get a bonus!");
        GrantItemToCurrentUserAndSetCustomData([normalDMAward], login_bonus_catalog, loginBonusCustomData);
        
        return {
            message: "FirstLogin",
            award : normalDMAward,
            streak : 1
        };
    }
    
    var nowDate = Date.now();
    // 计算24小时（以毫秒为单位）
    //const millisecondsIn24Hours = 24 * 60 * 60 * 1000;
    // 计算24小时后的时间戳
    //const timestampIn24Hours = nowDate + 3 * millisecondsIn24Hours;
    // 将时间戳转换为日期对象
    //nowDate = new Date(timestampIn24Hours);
    
    if(nowDate > parseInt(tracker[TRACKER_NEXT_GRANT]))
    {
        // Eligible for an item grant.
        // check to ensure that it has been less than 24 hours since the last grant window opened
        var timeWindow = new Date(parseInt(tracker[TRACKER_NEXT_GRANT]));
        timeWindow.setDate(timeWindow.getDate() + 1); // add 1 day 
        
        if(nowDate > timeWindow.getTime()) // 指的是目前这次登陆起码在上次登陆基础上跳过了一天
        {
            // streak ended :(			
            tracker = ResetTracker();
            UpdateTrackerData(tracker);
            log.info("Your consecutive login streak has been broken. Login tomorrow to get a bonus!");
            GrantItemToCurrentUserAndSetCustomData([normalDMAward], login_bonus_catalog,loginBonusCustomData);
            return {
                message: "LoginStreakBroken",
                award : normalDMAward,
                streak : 1
            };
        }else{ // 指的是现在这一天正好是上次登陆日期+1 （ Date.now() == timeWindow.getTime() ），不存在 Date.now() < timeWindow.getTime()
            // streak continues
            tracker[TRACKER_LOGIN_STREAK] += 1;
            var dateObj = new Date(nowDate);
            dateObj.setDate(dateObj.getDate() + 1); // add one day 
            tracker[TRACKER_NEXT_GRANT] = dateObj.getTime();

            // write back updated data to PlayFab
            log.info("Your consecutive login streak increased to: " + tracker[TRACKER_LOGIN_STREAK]);
            UpdateTrackerData(tracker);

            var remainder =  Number(tracker[TRACKER_LOGIN_STREAK] % 7);
            var award;
            if (remainder == 0) {
                award = extraDMAward;
            }else{
                award = normalDMAward;
            }
            loginBonusCustomData["streak"] = tracker[TRACKER_LOGIN_STREAK];
            GrantItemToCurrentUserAndSetCustomData([award], login_bonus_catalog, loginBonusCustomData);
            return {
                message: "LoginStreak",
                award : award,
                streak: tracker[TRACKER_LOGIN_STREAK]
            };
        }
    }
    return {
        message: "AlreadyLoginToday",
        award : null,
        streak: tracker[TRACKER_LOGIN_STREAK]
    };
};

function ResetTracker()
{
    var reset = {};
    reset[TRACKER_LOGIN_STREAK] = 1;
    
    var dateObj = new Date(Date.now());
    dateObj.setDate(dateObj.getDate() + 1); // add one day
    
    reset[TRACKER_NEXT_GRANT] = dateObj.getTime();
    return reset;
}


function UpdateTrackerData(data)
{
    var UpdateUserReadOnlyDataRequest = {
        "PlayFabId": currentPlayerId,
        "Data": {}
    };
    UpdateUserReadOnlyDataRequest.Data[CHECK_IN_TRACKER] = JSON.stringify(data);
    server.UpdateUserReadOnlyData(UpdateUserReadOnlyDataRequest);
}

// handlers.Gotcha = function (args, context) {
//     var request = {
//         "CatalogVersion": args.CatalogVersion,
//         "TableId": args.tableName
//     };
//     var Result = server.EvaluateRandomResultTable(request);
//
//     let itemIds = [];
//     itemIds.push(Result.ResultItemId);
//     var grantRequest = {
//         "PlayFabId": currentPlayerId,
//         "CatalogVersion": args.CatalogVersion,
//         "ItemIds": itemIds
//     };
//
//     var grantResult = server.GrantItemsToUser(grantRequest);
//     return { messageValue: grantResult["ItemGrantResults"] };
// }
//
// handlers.GotchaX9 = function (args, context) {
//
//     let itemIds = [];
//     for (let i = 0; i < 9; i++) {
//         var request = {
//             "CatalogVersion": args.CatalogVersion,
//             "TableId": args.tableName
//         };
//         var Result = server.EvaluateRandomResultTable(request);
//         itemIds.push(Result.ResultItemId);
//     }
//
//     var grantRequest = {
//         "PlayFabId": currentPlayerId,
//         "CatalogVersion": args.CatalogVersion,
//         "ItemIds": itemIds
//     };
//
//     var grantResult = server.GrantItemsToUser(grantRequest);
//
//     return { messageValue: grantResult["ItemGrantResults"] };
// }