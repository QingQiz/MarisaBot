<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {computed} from "vue";
import {GroupSongInfo, Score} from "../utils/summary_t";
import {useOpData, shouldSkip} from "./op_common";

const { data_fetched, songs, GetScore } = useOpData();

const filteredSongs = computed(() => songs.value.filter(s => !shouldSkip(s)))
const allScores = computed(() => filteredSongs.value.map(s => GetScore(s.Item3.Id, s.Item2)))

// 按定数 (Item1) 分组, 0.1 一档
const groups = computed(() => {
    const map = new Map<number, { songs: GroupSongInfo[], scs: Score[] }>();
    for (const s of filteredSongs.value) {
        const k = Math.round(s.Item1 * 10) / 10;
        if (!map.has(k)) map.set(k, { songs: [], scs: [] });
        map.get(k)!.songs.push(s);
        map.get(k)!.scs.push(GetScore(s.Item3.Id, s.Item2));
    }
    const result: { label: string, group: GroupSongInfo[], scores: Score[] }[] = [];
    const keys = [...map.keys()].sort((a, b) => b - a);
    for (const k of keys) {
        const v = map.get(k)!;
        result.push({ label: k.toFixed(1), group: v.songs, scores: v.scs });
    }
    return result;
});
</script>

<template>
    <div v-if="data_fetched" class="container">
        <div class="op-container">
            <div>ALL</div>
            <OverPower :scores="allScores" :group="filteredSongs" :detail="true" :show-difficulty="true"/>
        </div>
        <template v-for="g in groups" :key="g.label">
            <div class="op-container">
                <div>{{ g.label }}</div>
                <OverPower :scores="g.scores" :group="g.group" :detail="true" :show-difficulty="true"/>
            </div>
        </template>
    </div>
</template>

<style scoped lang="postcss">
.container {
    max-width: unset;
    width: 1200px;
    padding: 50px;
    @apply flex flex-col gap-16;
}
.op-container {
    @apply flex items-center;
    & div:first-child {
        @apply text-6xl w-[180px];
    }
}
</style>
