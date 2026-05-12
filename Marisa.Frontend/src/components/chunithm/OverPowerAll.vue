<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {ref} from "vue";
import {GroupSongInfo, Score} from "./utils/summary_t";
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

function GetSongsByConstRange(a: number, b: number) {
    return songs.value.filter(x => x.Item1 >= a && x.Item1 < b)
}

function GetConstRange(): [number, number, string][] {
    let res = [];

    for (let i = 10; i < 16; i += 0.5) {
        res.push([i, i + 0.5, Math.floor(i) == i ? i.toString() : Math.floor(i).toString() + '+'] as [number, number, string]);
    }

    res.reverse();

    return res;
}

function GetFiltered(group: GroupSongInfo[]): [GroupSongInfo[], Score[]] {
    const scoreList = group.map((x: GroupSongInfo) => GetScore(x.Item3.Id, x.Item2));
    return FilterDuplicateIds(group, scoreList);
}

const SCORE_SEGMENTS: { threshold: number; opBase: (c: number) => number; cellLength: (c: number) => number; cellValue: number }[] = [
    { threshold: 800000,   opBase: () => 0,            cellLength: (c: number) => 6000 / (c - 5),  cellValue: 0.05  },
    { threshold: 900000,   opBase: (c: number) => (c - 5) / 2, cellLength: (c: number) => 2000 / (c - 5), cellValue: 0.05 },
    { threshold: 975000,   opBase: (c: number) => c - 5,        cellLength: 150,                     cellValue: 0.05 },
    { threshold: 1000000,  opBase: (c: number) => c,             cellLength: 250,                     cellValue: 0.05 },
    { threshold: 1005000,  opBase: (c: number) => c + 1,         cellLength: 10,                      cellValue: 0.005 },
    { threshold: 1007500,  opBase: (c: number) => c + 1.5,       cellLength: 5,                       cellValue: 0.005 },
    { threshold: Infinity, opBase: (c: number) => c + 2,         cellLength: 10 / 3,                  cellValue: 0.005 },
];

function getOpS(constT: number, score: number): number {
    if (score < 500000) return 0;

    let prevThreshold = 500000;
    for (const seg of SCORE_SEGMENTS) {
        if (score < seg.threshold) {
            const scoreDiff = score - prevThreshold;
            const cellNum = Math.floor(scoreDiff / seg.cellLength(constT));
            return (seg.opBase(constT) * 5 + cellNum * seg.cellValue) * 200;
        }
        prevThreshold = seg.threshold;
    }
    return 0;
}

function CalcOverPower(song: GroupSongInfo, score: Score): number {
    if (!score || score.score == 0) return 0;
    const s = getOpS(score.ds, score.score) / 200;
    let r = score.fc == 'fullcombo' || score.fc == 'fullchain' || score.fc == 'fullchain2' ? 5000 : 0;
    if (score.fc == 'alljustice') r = 10000;
    if (score.score == 101_0000) r = 12500;
    const e = score.score <= 100_7500 ? 0 : (score.score - 100_7500) * 15;
    return s * 5 + r + e;
}

function FilterDuplicateIds(group: GroupSongInfo[], scoreList: Score[]): [GroupSongInfo[], Score[]] {
    const bestMap = new Map<number, { song: GroupSongInfo; score: Score; op: number }>();

    for (let i = 0; i < group.length; i++) {
        const song = group[i];
        const score = scoreList[i];
        const op = CalcOverPower(song, score);
        const existing = bestMap.get(song.Item3.Id);
        if (!existing || op > existing.op) {
            bestMap.set(song.Item3.Id, { song, score, op });
        }
    }

    const filteredSongs: GroupSongInfo[] = [];
    const filteredScores: Score[] = [];
    bestMap.forEach(v => {
        filteredSongs.push(v.song);
        filteredScores.push(v.score);
    });

    return [filteredSongs, filteredScores];
}
</script>

<template>
    <div v-if="data_fetched" class="container">
        <div class="op-container">
            <div>ALL</div>
            <OverPower :scores="GetFiltered(songs)[1]" :group="GetFiltered(songs)[0]"
                       :detail="true"/>
        </div>
        <template v-for="range in GetConstRange()">
            <template v-for="s in [GetSongsByConstRange(range[0], range[1])]">
                <template v-for="[filteredSongs, filteredScores] in [GetFiltered(s)]">
                    <div v-if="filteredSongs.length != 0" class="op-container">
                        <div>{{ range[2] }}</div>
                        <OverPower :scores="filteredScores" :group="filteredSongs"
                                   :detail="true"/>
                    </div>
                </template>
            </template>
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