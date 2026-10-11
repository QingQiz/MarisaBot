"""Generate complete song-title tables from layered game XML without guessing missing groups."""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import re
import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET

DIFFICULTIES = {"BAS": 0, "BSC": 0, "ADV": 1, "EXP": 2, "MAS": 3, "REM": 4}
DIFFICULTY_NAMES = ["BASIC", "ADVANCED", "EXPERT", "MASTER", "Re:MASTER"]
FC = {"FC": (1, "FULL COMBO"), "FCp": (2, "FULL COMBO+"), "AP": (3, "ALL PERFECT"), "APp": (4, "ALL PERFECT+")}
FS = {"FS": (2, "FULL SYNC"), "FSp": (3, "FULL SYNC+"), "FSD": (4, "FULL SYNC DX"), "FSDp": (5, "FULL SYNC DX+")}
RANK = {k: (i, k.replace("p", "+")) for i, k in enumerate(["D", "C", "B", "BB", "BBB", "A", "AA", "AAA", "S", "Sp", "SS", "SSp", "SSS", "SSSp"])}
SCORE_KINDS = {"MUSIC", "RANK", "FULLCOMBO", "SYNC", "DELUXE_SCORE"}


def xml_value(element):
    if not len(element):
        return element.text or ""
    result = {}
    for child in element:
        value = xml_value(child)
        if child.tag not in result:
            result[child.tag] = value
        elif isinstance(result[child.tag], list):
            result[child.tag].append(value)
        else:
            result[child.tag] = [result[child.tag], value]
    return result


def as_list(value):
    return value if isinstance(value, list) else [value] if value else []


def kind(condition):
    return condition.get("kind" + condition.get("Category", ""), "")


def active_conditions(data):
    return [dict(c, slot=slot) for slot, c in data.get("relConds", {}).items()
            if slot.startswith("condition") and isinstance(c, dict) and kind(c) not in ("", "None")]


def identifier(condition, key):
    return int(condition.get(key, {}).get("id", "0"))


def read_resources(archive_path):
    records = collections.defaultdict(dict)
    overrides = []
    configs = {}
    with zipfile.ZipFile(archive_path) as archive:
        candidates = []
        for entry in archive.infolist():
            filename = entry.filename.rsplit("/", 1)[-1].lower()
            if filename not in {"title.xml", "musicgroup.xml", "music.xml", "dataconfig.xml", "romconfig.xml"}:
                continue
            match = re.search(r"/StreamingAssets/([AN]\d{3})/", entry.filename)
            layer = match.group(1) if match else ""
            if not layer and filename != "romconfig.xml":
                continue
            candidates.append((layer, entry))
        # Game loader: A layers, then N option layers, each in lexical order.
        for layer, entry in sorted(candidates, key=lambda item: (item[0], item[1].filename)):
            raw = archive.read(entry)
            data = xml_value(ET.fromstring(raw))
            digest = hashlib.sha256(raw).hexdigest()
            filename = entry.filename.rsplit("/", 1)[-1].lower()
            if filename.endswith("config.xml"):
                configs[entry.filename] = data
                continue
            record = {"path": entry.filename, "layer": layer, "sha256": digest, "data": data}
            rid = int(data["name"]["id"])
            if rid in records[filename]:
                old = records[filename][rid]
                overrides.append({"type": filename, "id": rid, "lower": old["path"], "higher": entry.filename,
                                  "identical": old["sha256"] == digest})
            records[filename][rid] = record
    for filename in records:
        records[filename] = {rid: rec for rid, rec in records[filename].items() if rec["data"].get("disable") != "true"}
    return records, configs, overrides


def group_members(group):
    items = group["data"].get("MusicIds", {}).get("list", {})
    return as_list(items.get("StringID", [])) if isinstance(items, dict) else []


def difficulty_indices(selectors):
    indices = set()
    for condition in selectors:
        difficulty = condition.get("difficultyId", "ANY")
        if difficulty == "ALL":
            indices.update(range(4))
        elif difficulty in DIFFICULTIES:
            indices.add(DIFFICULTIES[difficulty])
    return sorted(indices)


def score_criteria(conditions):
    criteria = []
    for c in conditions:
        for key, mapping, dim in [("fcId", FC, "fc"), ("syncId", FS, "fs"), ("rankId", RANK, "rank")]:
            value = c.get(key, "None" if key != "rankId" else "D")
            if value in {"None", "", "D"}:
                continue
            if value not in mapping:
                return None
            level, label = mapping[value]
            criteria.append((dim, level, "RANK " + label + " 以上" if dim == "rank" else label))
        star = c.get("deluxeScoreId", "STAR_0")
        if star != "STAR_0":
            if not re.fullmatch(r"STAR_[1-5]", star):
                return None
            level = int(star.rsplit("_", 1)[1])
            criteria.append(("dx", level, f"DX 分数 {level}★以上"))
    return list(dict.fromkeys(criteria))


def make_check(conditions, selectors, targets):
    # Best-score payloads cannot establish group history, in-credit history,
    # repeated play counts, or settings/matching state of the same play.
    if len(targets) != 1 or any(c.get("Category") == "Credit" or kind(c) not in SCORE_KINDS for c in conditions):
        return None
    if any(int(c.get("param", "0")) > 1 for c in conditions if kind(c) == "MUSIC"):
        return None
    modes = {c.get("difficultyId", "ANY") for c in selectors}
    if modes - {"ANY", "ALL", *DIFFICULTIES} or ("ANY" in modes and len(modes) > 1):
        return None
    if "ALL" in modes and any(c.get("Category") != "Total" for c in selectors):
        return None
    criteria = score_criteria(conditions)
    if criteria is None or len(criteria) > 1:
        return None
    dim, level = (criteria[0][0], criteria[0][1]) if criteria else ("play", 1)
    indices = difficulty_indices(selectors)
    check = {"dim": dim, "lv": level, "scope": "all" if len(indices) > 1 else "one"}
    if indices:
        check["difficultyIndices"] = indices
    return check


def named_id(c, key):
    item = c.get(key, {})
    return item.get("str") or str(item.get("id", "?"))


def equipped_names(c, key):
    items = c.get(key, {}).get("list", {})
    return " / ".join(item.get("str") or item.get("id", "?") for item in as_list(items.get("StringID", []))) if isinstance(items, dict) else ""


def scene_text(c):
    k = kind(c)
    if k in SCORE_KINDS or k == "MUSIC_GROUP":
        return None
    if k == "PEOPLE":
        return f"{int(c.get('param', '0'))} 人以上同时游玩"
    if k == "MISS":
        return f"恰好 {int(c.get('param', '0'))} MISS"
    if k == "TAP_SPEED1_SONIC":
        speed = c.get("hiSpeedTapTouchId", "")
        return "以 SONIC 流速游玩" if speed == "SPEED_SONIC" else "以 SPEED 1 流速游玩" if speed == "SPEED_10" else f"以流速 {speed} 游玩"
    if k == "MIRROR_MODE":
        mode = {"LR": "左右镜像", "UD": "上下镜像", "UDLR": "上下左右镜像", "NORMAL": "正常镜像设置"}.get(c.get("mirrorModeId"), "镜像设置 " + c.get("mirrorModeId", ""))
        return "以" + mode + "游玩"
    if k in {"RANK_WIN", "RANK_LOSE"}:
        return "对战获胜" if k == "RANK_WIN" else "对战败北"
    if k == "TRACK_SKIP":
        return "触发跳曲"
    if k == "SATELLITE":
        return {"LEFT": "1P 侧", "RIGHT": "2P 侧"}.get(c.get("satelliteId"), "机台侧 " + c.get("satelliteId", ""))
    if k == "MOVIE_BRIGHTNESS":
        value = c.get("moviebrightnessID", "")
        return '以影片亮度「暗」游玩' if value == "Bright_0" else '以影片亮度「亮」游玩' if value == "Bright_4" else "以影片亮度设置 " + value + " 游玩"
    if k == "MUSIC_GENRE_SELECTED":
        return f"从「{named_id(c, 'musicGenreId')}」分类游玩"
    if k == "TODOHUKEN":
        return "在新潟县游玩" if c.get("todohukenId") == "NIIGATA" else "在指定地区游玩：" + c.get("todohukenId", "")
    if k == "TITLE":
        return f"装备称号「{named_id(c, 'titleId')}」游玩"
    if k == "CHARASET":
        return f"设置旅行伙伴「{named_id(c, 'charaId')}」游玩"
    if k == "CHARASET_DP":
        return "玩家分别设置旅行伙伴「" + equipped_names(c, "allEquipCharas") + "」后匹配游玩"
    if k == "MATCHING_FOR_TITLESETPLAYER":
        return f"与装备称号「{named_id(c, 'matchingUserTitle')}」的玩家匹配游玩"
    if k == "ICON":
        return f"设置头像「{named_id(c, 'iconId')}」游玩"
    if k == "FULLCOMBO_COUNT":
        criterion = FC.get(c.get("fcId"), (0, c.get("fcId", "")))[1]
        return f"累计达成 {criterion} {int(c.get('param', '0'))} 次"
    # Keep an unfamiliar constraint explicit; never turn it into plain play.
    return f"{c.get('Category')}.{k}（原始条件待核）"


def make_text(conditions, selectors, targets):
    indices = difficulty_indices(selectors)
    if indices == [0, 1, 2, 3]:
        difficulty = "BASIC～MASTER"
    elif indices == [0, 1, 2, 3, 4]:
        difficulty = "BASIC～Re:MASTER"
    else:
        difficulty = " / ".join(DIFFICULTY_NAMES[i] for i in indices)
    criteria = score_criteria(conditions)
    parts = [difficulty] if difficulty else []
    if criteria is None:
        parts.append("原始成绩条件待核")
    if criteria and not any(kind(c) == "FULLCOMBO_COUNT" for c in conditions):
        parts.extend(c[2] for c in criteria)
    for c in conditions:
        label = scene_text(c)
        if label and label not in parts:
            parts.append(label)
    count = max((int(c.get("param", "0")) for c in selectors if kind(c) == "MUSIC"), default=0)
    counted = count > 1
    if counted:
        parts.append(f"累计游玩 {count} 次" if not any(c.get("Category") == "Credit" for c in conditions) else f"游玩 {count} 次")
    if criteria == [] and not any(scene_text(c) for c in conditions) and not counted:
        parts.append("游玩")
    if any(c.get("Category") == "Credit" for c in conditions):
        parts.insert(0, "单次投币内")
    if any(kind(c) == "MUSIC_GROUP" for c in conditions) or len(targets) > 1:
        names = [name + (" [SD]" if sid < 10000 else " [DX]") for sid, name in sorted(targets.items())]
        parts.append("组内全部：" + " / ".join(names))
    return " · ".join(parts)


def generate(records, region="JPN"):
    titles, groups, music = (records.get(k, {}) for k in ("title.xml", "musicgroup.xml", "music.xml"))
    tables = collections.defaultdict(list)
    provenance = {}
    unresolved = []
    for tid, title in sorted(titles.items()):
        data = title["data"]
        conditions = active_conditions(data)
        targets = {}
        selectors = []
        group_sources = []
        missing_groups = []
        for c in conditions:
            sid = identifier(c, "musicId")
            if sid:
                targets[sid] = named_id(c, "musicId")
                selectors.append(c)
            gid = identifier(c, "musicGroupId")
            if kind(c) == "MUSIC_GROUP" and gid:
                selectors.append(c)
                if gid not in groups:
                    missing_groups.append(gid)
                    continue
                group = groups[gid]
                members = group_members(group)
                group_sources.append({"id": gid, "xml": group["path"], "sha256": group["sha256"], "members": members})
                for member in members:
                    if int(member["id"]):
                        targets[int(member["id"])] = member.get("str", "")
        if not targets and not missing_groups:
            continue
        for sid in targets:
            if sid in music:
                targets[sid] = music[sid]["data"]["name"]["str"]
        source = {"region": region, "version": data.get("releaseTagName", {}).get("str", "")}
        provenance[str(tid)] = {"name": data["name"]["str"], "rare": data.get("rareType", "Normal"), **source,
            "norm": data.get("normText", ""), "titleXml": title["path"], "titleSha256": title["sha256"],
            "eventName": data.get("eventName"), "netOpenName": data.get("netOpenName"),
            "activeConditions": conditions, "groupSources": group_sources,
            "musicSources": [{"id": sid, "xml": music[sid]["path"], "layer": music[sid]["layer"]} for sid in sorted(targets) if sid in music]}
        if missing_groups:
            unresolved.append({"id": tid, **provenance[str(tid)], "missingGroupIds": sorted(set(missing_groups))})
            continue
        check = make_check(conditions, selectors, targets)
        indices = difficulty_indices(selectors)
        entry = {"id": tid, "name": data["name"]["str"], "rare": data.get("rareType", "Normal"),
                 "text": make_text(conditions, selectors, targets), "norm": data.get("normText", ""),
                 "diffIdx": indices[0] if len(indices) == 1 else -1, "check": check, "source": source,
                 "condition": {"category": data.get("relConds", {}).get("category", ""),
                               "songIds": sorted(targets), "groupIds": sorted({g["id"] for g in group_sources}),
                               "difficultyIndices": indices}}
        for sid in sorted(targets):
            tables[str(sid)].append(entry)
    return dict(sorted(tables.items(), key=lambda item: int(item[0]))), provenance, unresolved


def read_catalog(path):
    payload = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    if isinstance(payload, dict):
        payload = payload.get("songIds", payload.get("catalog", {}).get("songIds", []))
    if not isinstance(payload, list) or not payload:
        raise ValueError("Catalog must be public music_data or an explicit nonempty songIds list")
    return {int(item["id"] if isinstance(item, dict) else item) for item in payload}


def write_json(path, payload):
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resources", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, nargs="+", required=True)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[2] / "Marisa.Frontend/public/assets/maimai")
    args = parser.parse_args()
    records, configs, overrides = read_resources(args.resources)
    region = next((d.get("region") for p, d in configs.items() if p.lower().endswith("romconfig.xml")), "JPN")
    tables, sources, unresolved = generate(records, region)
    catalog_ids = set().union(*(read_catalog(p) for p in args.catalog))
    current = {sid: entries for sid, entries in tables.items() if int(sid) in catalog_ids}
    pending = {sid: entries for sid, entries in tables.items() if int(sid) not in catalog_ids}
    args.output.mkdir(parents=True, exist_ok=True)
    for name, payload in [("song_titles.json", current), ("song_titles_pending.json", pending),
                          ("song_titles_source.json", sources), ("song_titles_unresolved.json", unresolved)]:
        write_json(args.output / name, payload)
    manifest = {"schemaVersion": 2, "resourceArchive": args.resources.name, "region": region,
                "catalog": {"songIds": sorted(catalog_ids), "snapshots": [{"file": p.name, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in args.catalog]},
                "counts": {"currentSongKeys": len(current), "currentAssociations": sum(map(len, current.values())),
                           "pendingSongKeys": len(pending), "pendingAssociations": sum(map(len, pending.values())),
                           "resolvableAssociations": sum(map(len, tables.values())), "unresolvedTitles": len(unresolved)},
                "overrides": overrides,
                "notes": ["Current/pending is a public-catalog snapshot, not a title availability guarantee.",
                          "Both databases are merged at lookup by complete song ID; future releases need no title migration.",
                          "JPN source conditions and rarity are retained; N layers may contain historical third-party music.",
                          "norm is original display prose; structured activeConditions/group members are authoritative.",
                          "Unresolved musicGroup references are never inferred from names."]}
    write_json(args.output / "song_titles_manifest.json", manifest)
    print(json.dumps(manifest["counts"], ensure_ascii=False))


if __name__ == "__main__":
    main()
