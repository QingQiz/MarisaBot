<script setup lang="ts">
import {computed} from "vue";
import {useOpData} from "./op/op_common";
import OpBoard, {type OpRow} from "./op/OpBoard.vue";

const { data_fetched, songs, maxConstMap, filterBestOP } = useOpData();

function GetSongsByConstRange(a: number, b: number) {
    return songs.value.filter(x => {
        const maxC = maxConstMap.value.get(x.Item3.Id) || 0;
        return maxC >= a && maxC < b;
    });
}

function GetConstRange(): [number, number, string][] {
    const res: [number, number, string][] = [];

    for (let i = 10; i < 16; i += 0.5) {
        res.push([i, i + 0.5, Math.floor(i) == i ? i.toString() : Math.floor(i).toString() + '+']);
    }

    res.reverse();

    return res;
}

const allBest = computed(() => filterBestOP(songs.value));

const rows = computed<OpRow[]>(() =>
    GetConstRange()
        .map(([a, b, label]) => {
            const best = filterBestOP(GetSongsByConstRange(a, b));
            return {label, group: best.group, scores: best.scores};
        })
        .filter(r => r.group.length > 0));

const pageTitle = computed(() => "OVER POWER · 全谱面");
</script>

<template>
    <OpBoard v-if="data_fetched" :charts="allBest.group" :scores="allBest.scores"
             :rows="rows"/>
</template>
