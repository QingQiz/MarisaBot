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

// 分组键取的是 Levels[i]，也就是等级数字（"13+"、"14"）；难度名在 DiffNames，别混。
// 所以排序要按数值比，否则跨位数时会错（"9+" 会排到 "14" 前面）；同值时 "13+" > "13"。
function levelRank(label: string): number {
    const m = /^(\d+(?:\.\d+)?)(\+?)$/.exec(label.trim());
    if (!m) return -1;                          // 认不出来的排最后
    return parseFloat(m[1]) + (m[2] ? 0.5 : 0);
}

const groups = computed(() => {
    const map = new Map<string, { songs: GroupSongInfo[], scs: Score[] }>();
    for (const s of filteredSongs.value) {
        const lvLabel = s.Item3.Levels[s.Item2];
        if (!map.has(lvLabel)) map.set(lvLabel, { songs: [], scs: [] });
        map.get(lvLabel)!.songs.push(s);
        map.get(lvLabel)!.scs.push(GetScore(s.Item3.Id, s.Item2));
    }
    const result: { label: string, group: GroupSongInfo[], scores: Score[] }[] = [];
    // 等级倒序
    const labels = [...map.keys()].sort((a, b) => {
        const ra = levelRank(a), rb = levelRank(b);
        if (ra !== rb) return rb - ra;
        return b.localeCompare(a);
    });
    for (const label of labels) {
        const v = map.get(label)!;
        result.push({ label, group: v.songs, scores: v.scs });
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
