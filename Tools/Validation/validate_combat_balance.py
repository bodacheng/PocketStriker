#!/usr/bin/env python3
"""Audit authored combat values and tutorial budgets without Unity or a network.

HP budgets use the same level formula and master-skill weights as production.
This checks configuration integrity and intended opening progression; it does
not infer win rates from HP or replace the natural-combat Play-mode smoke.
"""

import csv
import json
import math
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]


def scalar(text, name, default=None):
    match = re.search(r"^  " + re.escape(name) + r": ([^\n]+)$", text, re.MULTILINE)
    if not match:
        if default is not None:
            return default
        raise AssertionError(f"Missing combat value: {name}")
    return float(match[1])


def read_table(name, key):
    with (ROOT / "Assets/ExternalAssets/Config" / name).open(newline="") as source:
        return {row[key]: row for row in csv.DictReader(source)}


def level_multiplier(level, settings):
    power = scalar(settings, "levelPowerAverager")
    if level <= 1 or power <= 0:
        return max(level, 0)
    extra = level - 1
    before = max(scalar(settings, "levelDiminishStart") - 1, 0)
    if extra <= before:
        return 1 + extra * power
    beyond = extra - before
    return 1 + before * power + beyond * power / (1 + beyond / max(scalar(settings, "levelDiminishingRange"), 0.01))


def main():
    units = read_table("mst_unit.csv", "recordId")
    skills = read_table("mst_skill.csv", "id")
    settings = (ROOT / "Assets/Setting/Data.asset").read_text()
    common = (ROOT / "Assets/Setting/CommonSetting.asset").read_text()
    limits = [int(scalar(common, f"gangbangModeMaxUnitPerTeam{i}")) for i in range(1, 4)]
    assert limits == [12, 24, 48], f"Incorrect per-team count choices: {limits}"
    preparation = (ROOT / "Assets/Resources/DummyLayerSystem/FightPrepareLayer/FightPrepareLayer_gb.prefab").read_text()
    labels = re.findall(r"      propertyPath: m_Text\n      value: (\d+)\n", preparation)
    assert sorted(map(int, labels)) == limits, f"Preparation prefab count labels disagree: {labels}"
    assert scalar(settings, "Team1Invincible") == 0, "Tutorial difficulty must allow natural damage"
    hp_coefficient = scalar(settings, "HP_coefficient")
    attack_coefficient = scalar(settings, "AT_coefficient")
    assert hp_coefficient > 0 and attack_coefficient > 0

    # Addresses, filenames and stage-mode rows are independent. Resolve the
    # published Addressables GUIDs before using any asset as a runtime budget.
    directories = ("Stage", "EvolutionStages", "Gangbang", "EventStage")
    paths = [path for directory in directories
             for path in sorted((ROOT / "Assets/ExternalAssets" / directory).glob("*.asset"))]
    by_guid = {}
    for path in paths:
        guid = re.search(r"^guid: (\w+)$", path.with_suffix(".asset.meta").read_text(), re.MULTILINE)[1]
        assert guid not in by_guid, f"Duplicate combat asset GUID: {guid}"
        by_guid[guid] = path
    stage_catalog = (ROOT / "Assets/AddressableAssetsData/AssetGroups/Stage.asset").read_text()
    published = {}
    for guid, address, entry in re.findall(r"  - m_GUID: (\w+)\n    m_Address: ([^\n]+)\n(.*?)(?=  - m_GUID:|\Z)", stage_catalog, re.DOTALL):
        assert "    - quest\n" in entry and guid in by_guid, f"Invalid quest address {address}: {guid}"
        number = int(address)
        assert number not in published, f"Duplicate quest address: {address}"
        published[number] = by_guid[guid]
    assert sorted(published) == list(range(1, 121)), "Published quests must be continuous from 1 to 120"
    assert len(set(published.values())) == len(published), "Published quests share an asset"
    for number in (1, 2):
        assert published[number] == ROOT / f"Assets/ExternalAssets/Stage/{number}.asset", f"Opening quest {number} points to the wrong difficulty asset"
    for number in (3, 5, 10):
        assert published[number] == ROOT / f"Assets/ExternalAssets/EvolutionStages/{number}.asset", f"Opening evolution quest {number} points to the wrong difficulty asset"
    stage_modes = {int(key): int(list(row.values())[1]) for key, row in read_table("stage_mode.csv", "STAGE_ID").items() if key.isdigit()}
    published_by_path = {path: number for number, path in published.items()}

    report = {"passed": False, "teamLimits": limits, "assetsChecked": 0, "unitsChecked": 0,
              "skillReferencesChecked": 0, "tutorials": [], "publishedStages": [],
              "scope": "All 328 authored adventure, evolution, legacy Group and event assets; the 120 published quests resolved by Addressables GUID; master skill/unit references, HP/attack weights, positive levels/HP rates, valid AI/gauge values, beginner budgets and early progression.",
              "limitation": "Configuration checks only. Initial server-granted inventory is not stored in the repository. A level-1 human with nine HP-weight-3 skills is the comparison budget; natural combat is tested separately."}
    opening = {}
    for directory in directories:
        for path in sorted((ROOT / "Assets/ExternalAssets" / directory).glob("*.asset")):
            text = path.read_text()
            level = scalar(text, "stageRefLevel")
            rates = [scalar(text, "team1HpRate"), scalar(text, "team2HpRate")]
            assert all(math.isfinite(value) and value > 0 for value in [level, *rates]), path
            assert scalar(text, "team1CGMode") in (0, 1, 2), path
            assert scalar(text, "team2CGMode") in (0, 1, 2), path
            assert scalar(text, "team1AIMode") in (0, 1), path
            assert scalar(text, "team2AIMode") in (0, 1), path
            assert scalar(text, "dumbAIDecisionDelay") >= 0, path
            assert 0 <= scalar(text, "dreamComboAIRateNum", 5) <= 100, path
            authored_units = re.split(r"^  - id:.*\n", text.split("  team1HpRate:")[0], flags=re.MULTILINE)[1:]
            assert authored_units, path
            enemy_hp = []
            all_skills = []
            for unit in authored_units:
                unit_id = re.search(r"^    r_id: (.+)$", unit, re.MULTILINE)[1].strip()
                assert unit_id in units, f"{path}: unknown unit {unit_id}"
                ids = re.findall(r"^      [abc][123]: (.*)$", unit, re.MULTILINE)
                assert len(ids) == 9 and all(skill_id in skills for skill_id in ids), f"{path}: invalid skill set"
                records = [skills[skill_id] for skill_id in ids]
                # Utility, guard and movement skills can deal zero damage.
                assert all(math.isfinite(float(row["HP_WEIGHT"])) and float(row["HP_WEIGHT"]) > 0
                           and math.isfinite(float(row["ATTACK_WEIGHT"])) and float(row["ATTACK_WEIGHT"]) >= 0 for row in records), path
                all_skills.extend(records)
                enemy_hp.append(sum(float(row["HP_WEIGHT"]) for row in records) * hp_coefficient
                                * level_multiplier(level, settings) * rates[1])
                report["unitsChecked"] += 1
                report["skillReferencesChecked"] += len(ids)
            report["assetsChecked"] += 1
            number = published_by_path.get(path)
            if number is not None:
                report["publishedStages"].append({"stage": number, "source": str(path.relative_to(ROOT)),
                    "mode": stage_modes[number], "enemyCount": len(authored_units), "level": level,
                    "enemyHpRate": rates[1], "enemyAiMode": int(scalar(text, "team2AIMode")),
                    "enemyDreamRate": int(scalar(text, "dreamComboAIRateNum", 5))})
                if number <= 10:
                    opening[number] = text
                if number > 2 and stage_modes[number] in (1, 2):
                    assert rates[1] == 1 and scalar(text, "dreamComboAIRateNum") <= 20, f"{path}: ordinary teams inherited evolution boss difficulty"
            if number in (1, 2):
                assert level == 1 and len(enemy_hp) == number, path
                assert scalar(text, "team2AIMode") == 1 and scalar(text, "dumbAIDecisionDelay") >= 30, path
                assert scalar(text, "dreamComboAIRateNum") == 0, path
                assert all(int(row["SP_LEVEL"]) == 0 and row["ATTACK_TYPE"] == "GR" for row in all_skills), path
                assert all(len(set(re.findall(r"^      [abc][123]: (.*)$", unit, re.MULTILINE))) == 9
                           for unit in authored_units), path
                hero_hp = 9 * 3 * hp_coefficient * rates[0]
                ratio = sum(enemy_hp) / hero_hp
                assert rates[0] > 1 and ratio < 0.6, f"{path}: enemy budget overwhelms the starter"
                report["tutorials"].append({"stage": number, "heroLevel": 1, "heroHpBudget": hero_hp,
                                           "enemyHp": enemy_hp, "enemyTotalToHeroHp": ratio,
                                           "aiDecisionDelayFrames": int(scalar(text, "dumbAIDecisionDelay"))})

    assert report["assetsChecked"] == 328 and len(report["publishedStages"]) == 120
    report["publishedStages"].sort(key=lambda row: row["stage"])
    report["tutorials"].sort(key=lambda row: row["stage"])
    assert report["tutorials"][0]["enemyTotalToHeroHp"] < report["tutorials"][1]["enemyTotalToHeroHp"]
    assert scalar(opening[3], "team2AIMode") == 1 and scalar(opening[3], "dreamComboAIRateNum") == 0
    assert scalar(opening[5], "team2AIMode") == 1 and scalar(opening[5], "dreamComboAIRateNum") <= 5
    assert scalar(opening[10], "dreamComboAIRateNum") <= 10
    assert scalar(opening[3], "team2HpRate") == 0.7
    assert scalar(opening[5], "team2HpRate") == 0.8
    assert scalar(opening[10], "team2HpRate") == 1.25
    report["passed"] = True
    output = ROOT / "Logs/CombatBalance/config-report.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n")
    print(f"PASS: {report['assetsChecked']} combat assets, {report['unitsChecked']} units, "
          f"{report['skillReferencesChecked']} skill references; 12/24/48 counts and tutorial budgets")
    for row in report["tutorials"]:
        print(f"Stage {row['stage']}: player HP {row['heroHpBudget']:g}; enemy HP {row['enemyHp']}; "
              f"total/player {row['enemyTotalToHeroHp']:.3f}")


if __name__ == "__main__":
    main()
