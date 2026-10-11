"""Independently verify generated maititle assets against effective SDEZ XML.

Does not import the generator; derives the graph and safe score predicates from
XML. Prints a compact summary and optionally writes a JSON evidence report.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
RANK = dict(zip(("D", "C", "B", "BB", "BBB", "A", "AA", "AAA", "S", "Sp", "SS", "SSp", "SSS", "SSSp"), range(14)))
FC = {"FC": 1, "FCp": 2, "AP": 3, "APp": 4}
FS = {"FS": 2, "FSp": 3, "FSD": 4, "FSDp": 5}
DIFF = {"BAS": 0, "ADV": 1, "EXP": 2, "MAS": 3, "REM": 4}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_catalog(path):
    payload = read_json(path)
    if isinstance(payload, dict):
        payload = payload.get("songIds", payload.get("catalog", {}).get("songIds", []))
    if not isinstance(payload, list) or not payload:
        raise ValueError(f"{path}: expected music_data records or a nonempty songIds list")
    return {int(item["id"] if isinstance(item, dict) else item) for item in payload}


def xdict(element):
    if not list(element):
        return element.text or ""
    result = {}
    for child in element:
        value = xdict(child)
        if child.tag not in result:
            result[child.tag] = value
        elif isinstance(result[child.tag], list):
            result[child.tag].append(value)
        else:
            result[child.tag] = [result[child.tag], value]
    return result


def kind(condition):
    return condition.get("kind" + condition["Category"], "None")


def safe_check(title, song_id, targets, group_ids):
    conditions = title["conditions"]
    if len(targets) != 1 or group_ids:
        return None
    if any(c["Category"] == "Credit" for c in conditions):
        return None
    allowed = {"Track": {"MUSIC", "RANK", "FULLCOMBO", "SYNC", "DELUXE_SCORE"}, "Total": {"MUSIC"}}
    if any(kind(c) not in allowed.get(c["Category"], set()) for c in conditions):
        return None
    selectors = [c for c in conditions if kind(c) == "MUSIC" and int(c["musicId"]["id"]) == song_id]
    if not selectors:
        return None
    if any(c["Category"] == "Total" and int(c["param"]) > 1 for c in conditions):
        return None
    difficulty_modes = {c["difficultyId"] for c in selectors}
    if difficulty_modes == {"ANY"}:
        scope, indices = "one", None
    elif len(difficulty_modes) == 1 and next(iter(difficulty_modes)) in DIFF:
        scope, indices = "one", [DIFF[next(iter(difficulty_modes))]]
    elif difficulty_modes <= {"ALL", "REM"} and "ALL" in difficulty_modes and all(c["Category"] == "Total" for c in selectors):
        scope, indices = "all", [0, 1, 2, 3] + ([4] if "REM" in difficulty_modes else [])
    else:
        return None
    predicates = []
    for c in conditions:
        k = kind(c)
        if c["Category"] == "Total" and k == "MUSIC":
            if c["fcId"] != "None":
                predicates.append(("fc", FC.get(c["fcId"])))
            if c["syncId"] != "None":
                predicates.append(("fs", FS.get(c["syncId"])))
            if c["rankId"] != "D":
                predicates.append(("rank", RANK.get(c["rankId"])))
        elif k == "FULLCOMBO":
            predicates.append(("fc", FC.get(c["fcId"])))
        elif k == "SYNC":
            predicates.append(("fs", FS.get(c["syncId"])))
        elif k == "RANK":
            predicates.append(("rank", RANK.get(c["rankId"])))
        elif k == "DELUXE_SCORE":
            token = c["deluxeScoreId"]
            predicates.append(("dx", int(token.split("_")[-1]) if token.startswith("STAR_") else None))
    predicates = set(predicates) or {("play", 1)}
    # Different predicates cannot be safely reconstructed from separately stored
    # historical maxima; mixed thresholds per difficulty are also not flattened.
    if len(predicates) != 1:
        return None
    dim, level = next(iter(predicates))
    if level is None:
        return None
    return {"dim": dim, "lv": level, "scope": scope, "difficultyIndices": indices}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resources", type=Path, required=True, help="SDEZ resource ZIP")
    parser.add_argument("--assets", type=Path, default=Path(__file__).resolve().parents[2] / "Marisa.Frontend/public/assets/maimai")
    parser.add_argument("--catalog", type=Path, nargs="+", required=True, help="Public music_data or songIds snapshots")
    parser.add_argument("--report", type=Path, help="Optional detailed JSON evidence report")
    args = parser.parse_args()
    effective = {"Title.xml": {}, "MusicGroup.xml": {}, "Music.xml": {}}
    source_region = "JPN"
    # Layer detection is independent of archive root folder and game version.
    with zipfile.ZipFile(args.resources) as archive:
        paths = []
        configs = []
        for path in archive.namelist():
            if path.rsplit("/", 1)[-1].lower() == "romconfig.xml":
                configs.append(path)
            layer = re.search(r"/StreamingAssets/([AN]\d{3})/", "/" + path)
            if layer and path.rsplit("/", 1)[-1] in effective:
                paths.append((layer.group(1), path))
        if configs:
            source_region = xdict(ET.fromstring(archive.read(sorted(configs)[0]))).get("region", "JPN")
        for _, path in sorted(paths):
            raw = archive.read(path)
            data = xdict(ET.fromstring(raw))
            item_id = int(data["name"]["id"])
            effective[path.rsplit("/", 1)[-1]][item_id] = {"data": data, "path": path, "sha256": hashlib.sha256(raw).hexdigest()}
    for table in effective.values():
        for item_id in [k for k, v in table.items() if v["data"].get("disable") == "true"]:
            del table[item_id]
    groups = {}
    for item_id, record in effective["MusicGroup.xml"].items():
        raw_members = record["data"].get("MusicIds", {}).get("list", {}).get("StringID", [])
        groups[item_id] = raw_members if isinstance(raw_members, list) else [raw_members] if raw_members else []
    expected, titles, unresolved = {}, {}, []
    for title_id, record in effective["Title.xml"].items():
        data = record["data"]
        conditions = [dict(c, slot=s) for s, c in data["relConds"].items() if s.startswith("condition") and kind(c) != "None"]
        if not conditions:
            continue
        targets, group_ids = set(), set()
        for c in conditions:
            song_id = int(c["musicId"]["id"])
            if song_id:
                targets.add(song_id)
            if kind(c) == "MUSIC_GROUP":
                group_id = int(c["musicGroupId"]["id"])
                if not group_id:
                    continue
                group_ids.add(group_id)
                if group_id not in groups:
                    unresolved.append((title_id, group_id))
                else:
                    targets.update(int(m["id"]) for m in groups[group_id] if int(m["id"]))
        title = dict(record, conditions=conditions, targets=targets, group_ids=group_ids)
        titles[title_id] = title
        for song_id in targets:
            expected[(song_id, title_id)] = title
    active = read_json(args.assets / "song_titles.json")
    pending = read_json(args.assets / "song_titles_pending.json")
    provenance = read_json(args.assets / "song_titles_source.json")
    stored_unresolved = read_json(args.assets / "song_titles_unresolved.json")
    manifest = read_json(args.assets / "song_titles_manifest.json")
    actual = {(int(k), int(entry["id"])): entry for db in (active, pending) for k, entries in db.items() for entry in entries}
    errors = []
    warnings = []
    expected_source_ids = {tid for _, tid in expected} | {tid for tid, _ in unresolved}
    if set(map(int, provenance)) != expected_source_ids:
        errors.append({"kind": "provenance_title_set_mismatch", "expectedCount": len(expected_source_ids), "actualCount": len(provenance)})
    if {(int(t["id"]), int(gid)) for t in stored_unresolved for gid in t["missingGroupIds"]} != set(unresolved):
        errors.append({"kind": "unresolved_group_references_mismatch"})
    for title_id in sorted(expected_source_ids & set(map(int, provenance))):
        source = provenance[str(title_id)]
        original = titles[title_id]
        for field, value in (("titleXml", original["path"]), ("titleSha256", original["sha256"]), ("activeConditions", original["conditions"]), ("norm", original["data"]["normText"])):
            if source.get(field) != value:
                errors.append({"kind": "provenance_source_mismatch", "titleId": title_id, "field": field})
        for group_source in source.get("groupSources", []):
            group_id = int(group_source["id"])
            original_group = effective["MusicGroup.xml"].get(group_id)
            if original_group is None or group_source.get("members") != groups[group_id] or group_source.get("xml") != original_group["path"] or group_source.get("sha256") != original_group["sha256"]:
                errors.append({"kind": "group_provenance_mismatch", "titleId": title_id, "groupId": group_id})
    overlap = sorted(set(active) & set(pending))
    if overlap:
        errors.append({"kind": "active_pending_key_overlap", "songIds": overlap})
    missing = sorted(set(expected) - set(actual))
    extra = sorted(set(actual) - set(expected))
    if missing or extra:
        errors.append({"kind": "association_graph_mismatch", "missing": missing, "extra": extra})
    pair_total = sum(len(entries) for db in (active, pending) for entries in db.values())
    if pair_total != len(actual):
        errors.append({"kind": "duplicate_associations", "total": pair_total, "unique": len(actual)})
    catalog_ids = set().union(*(read_catalog(path) for path in args.catalog))
    expected_counts = {"currentSongKeys": len(active), "currentAssociations": sum(map(len, active.values())), "pendingSongKeys": len(pending), "pendingAssociations": sum(map(len, pending.values())), "resolvableAssociations": len(expected), "unresolvedTitles": len({tid for tid, _ in unresolved})}
    if manifest.get("counts") != expected_counts or set(manifest.get("catalog", {}).get("songIds", [])) != catalog_ids or manifest.get("region") != source_region:
        errors.append({"kind": "manifest_count_catalog_region_mismatch", "expectedCounts": expected_counts})
    expected_snapshots = [{"file": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()} for path in args.catalog]
    if manifest.get("catalog", {}).get("snapshots") != expected_snapshots:
        errors.append({"kind": "manifest_snapshot_digest_mismatch"})
    wrong_active = sorted(set(map(int, active)) - catalog_ids)
    wrong_pending = sorted(set(map(int, pending)) & catalog_ids)
    if wrong_active or wrong_pending:
        errors.append({"kind": "catalog_partition_mismatch", "wrongActive": wrong_active, "wrongPending": wrong_pending})
    score_check_count = 0
    for pair in sorted(set(expected) & set(actual)):
        song_id, title_id = pair
        title = expected[pair]
        original = title["data"]
        entry = actual[pair]
        for field, source in (("name", original["name"]["str"]), ("rare", original["rareType"]), ("norm", original["normText"])):
            if entry.get(field) != source:
                errors.append({"kind": "metadata_mismatch", "pair": pair, "field": field, "expected": source, "actual": entry.get(field)})
        source = entry.get("source", {})
        if source.get("region") != source_region or source.get("version") != original["releaseTagName"]["str"]:
            errors.append({"kind": "missing_source_region_version", "pair": pair, "actual": source})
        normalized = entry.get("condition", {})
        if normalized.get("songIds") != sorted(title["targets"]) or normalized.get("groupIds") != sorted(title["group_ids"]) or normalized.get("category") != original["relConds"]["category"]:
            errors.append({"kind": "normalized_relation_mismatch", "pair": pair, "actual": normalized})
        independently_safe = safe_check(title, song_id, title["targets"], title["group_ids"])
        check = entry.get("check")
        if check:
            score_check_count += 1
            if independently_safe is None:
                errors.append({"kind": "unsafe_automatic_predicate", "pair": pair, "actual": check})
            else:
                for field in ("dim", "lv", "scope"):
                    if check.get(field) != independently_safe[field]:
                        errors.append({"kind": "score_predicate_mismatch", "pair": pair, "field": field, "expected": independently_safe[field], "actual": check.get(field)})
                indices = check.get("difficultyIndices")
                if independently_safe["difficultyIndices"] is not None and indices != independently_safe["difficultyIndices"]:
                    errors.append({"kind": "difficulty_set_mismatch", "pair": pair, "expected": independently_safe["difficultyIndices"], "actual": indices})
        elif independently_safe:
            warnings.append({"kind": "safe_predicate_left_unavailable", "pair": pair, "expected": independently_safe})
        for condition in title["conditions"]:
            if kind(condition) == "MISS" and ("≤" in entry["text"] or "以内" in entry["text"]):
                errors.append({"kind": "miss_exact_count_wrong_text", "pair": pair, "text": entry["text"]})
            if kind(condition) == "TAP_SPEED1_SONIC" and condition["hiSpeedTapTouchId"] == "SPEED_10" and "SONIC" in entry["text"]:
                errors.append({"kind": "speed_one_sonic_wrong_text", "pair": pair, "text": entry["text"]})
    specific = {}
    for song_id in (10030, 11818, 1818, 0, 11394):
        specific[str(song_id)] = [entry for (sid, _), entry in actual.items() if sid == song_id]
    for song_id, entries in specific.items():
        source_ids = sorted(tid for sid, tid in expected if sid == int(song_id))
        if sorted(entry["id"] for entry in entries) != source_ids:
            errors.append({"kind": "known_example_source_mismatch", "songId": int(song_id), "expectedTitleIds": source_ids})
    summary = {"xmlTitles": len(effective["Title.xml"]), "xmlGroups": len(groups), "resolvablePairs": len(expected), "resolvableSongKeys": len({p[0] for p in expected}), "provenanceTitles": len(provenance), "unresolvedGroupReferences": len(unresolved), "activeKeys": len(active), "pendingKeys": len(pending), "activeAssociations": sum(map(len, active.values())), "pendingAssociations": sum(map(len, pending.values())), "automaticChecks": score_check_count, "errors": len(errors), "warnings": len(warnings)}
    report = {"summary": summary, "errors": errors, "warnings": warnings, "knownExamples": specific, "unresolvedGroupReferences": unresolved}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    if errors:
        print(json.dumps(errors[:20], ensure_ascii=False, indent=2))
        raise SystemExit(1)


if __name__ == "__main__":
    main()
