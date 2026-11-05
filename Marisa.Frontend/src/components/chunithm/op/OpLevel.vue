<script setup lang="ts">
import {computed} from "vue";
import {GroupSongInfo, Score} from "../utils/summary_t";
import {useOpData, shouldSkip} from "./op_common";
import OpBoard, {type OpRow} from "./OpBoard.vue";

const { data_fetched, songs, GetScore } = useOpData();

const filteredSongs = computed(() => songs.value.filter(s => !shouldSkip(s)));
const allScores = computed(() => filteredSongs.value.map(s => GetScore(s.Item3.Id, s.Item2)));

// 分组键取的是 Levels[i]，也就是等级数字（"13+"、"14"）；难度名在 DiffNames，别混。
// 所以排序要按数值比，否则跨位数时会错（"9+" 会排到 "14" 前面）；同值时 "13+" > "13"。
function levelRank(label: string): number {
    const m = /^(\d+(?:\.\d+)?)(\+?)$/.exec(label.trim());
    if (!m) return -1;                          // 认不出来的排最后
    return parseFloat(m[1]) + (m[2] ? 0.5 : 0);
}

const rows = computed<OpRow[]>(() => {
    const map = new Map<string, { group: GroupSongInfo[], scores: Score[] }>();
    for (const s of filteredSongs.value) {
        const lvLabel = s.Item3.Levels[s.Item2];
        if (!map.has(lvLabel)) map.set(lvLabel, { group: [], scores: [] });
        map.get(lvLabel)!.group.push(s);
        map.get(lvLabel)!.scores.push(GetScore(s.Item3.Id, s.Item2));
    }
    return [...map.keys()]
        .sort((a, b) => {
            const ra = levelRank(a), rb = levelRank(b);
            if (ra !== rb) return rb - ra;
            return b.localeCompare(a);
        })
        .map(label => ({ label, ...map.get(label)! }));
});
</script>

<template>
    <OpBoard v-if="data_fetched" :charts="filteredSongs" :scores="allScores" :rows="rows"/>
</template>
