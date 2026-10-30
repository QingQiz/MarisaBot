<script setup lang="ts">
import {computed} from "vue";
import {GroupSongInfo, Score} from "../utils/summary_t";
import {useOpData, shouldSkip} from "./op_common";
import OpBoard, {type OpRow} from "./OpBoard.vue";

const { data_fetched, songs, GetScore } = useOpData();

const filteredSongs = computed(() => songs.value.filter(s => !shouldSkip(s)));
const allScores = computed(() => filteredSongs.value.map(s => GetScore(s.Item3.Id, s.Item2)));

// 按定数 (Item1) 分组，0.1 一档
const rows = computed<OpRow[]>(() => {
    const map = new Map<number, { group: GroupSongInfo[], scores: Score[] }>();
    for (const s of filteredSongs.value) {
        const k = Math.round(s.Item1 * 10) / 10;
        if (!map.has(k)) map.set(k, { group: [], scores: [] });
        map.get(k)!.group.push(s);
        map.get(k)!.scores.push(GetScore(s.Item3.Id, s.Item2));
    }
    return [...map.keys()]
        .sort((a, b) => b - a)
        .map(k => ({ label: k.toFixed(1), ...map.get(k)! }));
});
</script>

<template>
    <OpBoard v-if="data_fetched" :charts="filteredSongs" :scores="allScores" :rows="rows"/>
</template>
