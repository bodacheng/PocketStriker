using System.Collections.Generic;
using UnityEngine;

public enum Team
{
    none = 0,
    player1 = 1,
    player2 = 2,
    player3 = 3,
    player4 = 4,
}

public class TeamConfig
{
    public readonly Team myTeam;
    public readonly List<Team> myEnemies;

    public string playID = "-1";
    public int mylayer;
    public int myWeaponLayer;
    public int myShieldLayer;

    public LayerMask myTeamLayerMask;
    public LayerMask myTeamAndMyEnemy;
    public LayerMask enemyLayerMask;
    public LayerMask enemyWeaponLayerMask;
    public LayerMask enemyShieldLayerMask;
    public LayerMask mySensorAndWeaponTargetLayerMask;

    public static readonly TeamConfig DefaultSet = new TeamConfig("-1", Team.none, new List<Team>());

    public TeamConfig(string id, Team myTeam, List<Team> myEnemies)
    {
        playID = id;
        this.myTeam = myTeam;
        this.myEnemies = myEnemies ?? new List<Team>();
        RefreshMyLayers();
    }

    public void RefreshMyLayers()
    {
        mylayer = 0;
        myWeaponLayer = 0;
        myShieldLayer = 0;
        myTeamLayerMask = 0;
        myTeamAndMyEnemy = 0;
        enemyLayerMask = 0;
        enemyWeaponLayerMask = 0;
        enemyShieldLayerMask = 0;
        mySensorAndWeaponTargetLayerMask = 0;

        ApplyOwnLayers(myTeam);

        if (myEnemies.Count == 0)
        {
            return;
        }

        for (var i = 0; i < myEnemies.Count; i++)
        {
            ApplyEnemyLayers(myEnemies[i]);
        }
    }

    void ApplyOwnLayers(Team team)
    {
        switch (team)
        {
            case Team.player1:
                mylayer = 9;
                myTeamLayerMask = 1 << 9;
                myWeaponLayer = 11;
                myShieldLayer = 15;
                break;
            case Team.player2:
                mylayer = 10;
                myTeamLayerMask = 1 << 10;
                myWeaponLayer = 12;
                myShieldLayer = 16;
                break;
        }
    }

    void ApplyEnemyLayers(Team team)
    {
        switch (team)
        {
            case Team.player1:
                enemyLayerMask |= 1 << 9;
                enemyWeaponLayerMask |= 1 << 11;
                enemyShieldLayerMask |= 1 << 15;
                mySensorAndWeaponTargetLayerMask |= (1 << 9) | (1 << 11) | (1 << 15) | (1 << 14);
                myTeamAndMyEnemy |= (1 << 10) | (1 << 9);
                break;
            case Team.player2:
                enemyLayerMask |= 1 << 10;
                enemyWeaponLayerMask |= 1 << 12;
                enemyShieldLayerMask |= 1 << 16;
                mySensorAndWeaponTargetLayerMask |= (1 << 10) | (1 << 12) | (1 << 16) | (1 << 14);
                myTeamAndMyEnemy |= (1 << 10) | (1 << 9);
                break;
            default:
                enemyLayerMask = 31;
                enemyWeaponLayerMask = 31;
                enemyShieldLayerMask = 31;
                mySensorAndWeaponTargetLayerMask = 31;
                break;
        }
    }
}
