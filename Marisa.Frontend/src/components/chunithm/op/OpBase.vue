<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {ref, computed} from "vue";
import {GroupSongInfo, Score} from "../utils/summary_t";
import axios from "axios";
import {context_get} from "@/GlobalVars";
import {useRoute} from "vue-router";

const route = useRoute()
const id    = ref(route.query.id)

const data_fetched = ref(false)
const songs  = ref([] as GroupSongInfo[])
const scores = ref({} as { [key: string]: Score })

axios.all([
    axios.get(context_get, {params: {id: id.value, name: 'OverPowerSongs'}}),
    axios.get(context_get, {params: {id: id.value, name: 'OverPowerScores'}}),
]).then(data => {
    songs.value  = data[0].data
    scores.value = data[1].data
}).finally(() => {
    data_fetched.value = true
})

function GetScore(id: number, level: number) {
    return scores.value[`(${id}, ${level})`]
}

function shouldSkip(s: GroupSongInfo): boolean {
    if (s.Item2 !== 3 && s.Item2 !== 4) return true;
    return s.Item3.Constants[s.Item2] < 10;
}

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
            <OverPower :scores="allScores" :group="filteredSongs" :detail="true"/>
        </div>
        <template v-for="g in groups" :key="g.label">
            <div class="op-container">
                <div>{{ g.label }}</div>
                <OverPower :scores="g.scores" :group="g.group" :detail="true"/>
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
