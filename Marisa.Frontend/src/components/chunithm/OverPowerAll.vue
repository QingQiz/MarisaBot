<script setup lang="ts">
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {ref, computed} from "vue";
import {GroupSongInfo, Score} from "./utils/summary_t";
import {calcOverPower} from "./utils/overpower";
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

const maxConstMap = computed(() => {
    const map = new Map<number, number>();
    for (const s of songs.value) {
        const id = s.Item3.Id;
        if (!map.has(id) || map.get(id)! < s.Item1) {
            map.set(id, s.Item1);
        }
    }
    return map;
})

function GetSongsByConstRange(a: number, b: number) {
    return songs.value.filter(x => {
        const maxC = maxConstMap.value.get(x.Item3.Id) || 0;
        return maxC >= a && maxC < b;
    })
}

function filterBestOP(entries: GroupSongInfo[]): { group: GroupSongInfo[], scores: Score[] } {
    const map = new Map<number, { song: GroupSongInfo, score: Score | null, op: number }>();

    for (const e of entries) {
        const score = GetScore(e.Item3.Id, e.Item2);
        const key = e.Item3.Id;

        if (!score) {
            // 无分: 保留定数最高的 entry
            if (!map.has(key)) {
                map.set(key, { song: e, score: null, op: 0 });
            } else if (!map.get(key)!.score && e.Item1 > map.get(key)!.song.Item1) {
                map.set(key, { song: e, score: null, op: 0 });
            }
            continue;
        }

        const op = calcOverPower(score);
        if (!map.has(key) || (map.get(key)!.op || -1) < op) {
            map.set(key, { song: e, score, op });
        }
    }

    const group: GroupSongInfo[] = [], scs: Score[] = [];
    for (const [, v] of map) { group.push(v.song); scs.push(v.score as Score); }
    return { group, scores: scs };
}

function GetConstRange(): [number, number, string][] {
    let res = [];

    for (let i = 10; i < 16; i += 0.5) {
        res.push([i, i + 0.5, Math.floor(i) == i ? i.toString() : Math.floor(i).toString() + '+'] as [number, number, string]);
    }

    res.reverse();

    return res;
}
</script>

<template>
    <div v-if="data_fetched" class="container">
        <template v-for="f in [filterBestOP(songs)]">
            <div class="op-container">
                <div>ALL</div>
                <OverPower :scores="f.scores" :group="f.group" :detail="true"/>
            </div>
        </template>
        <template v-for="range in GetConstRange()">
            <template v-for="s in [GetSongsByConstRange(range[0], range[1])]">
                <template v-for="f in [filterBestOP(s)]">
                    <div v-if="f.group.length != 0" class="op-container">
                        <div>{{ range[2] }}</div>
                        <OverPower :scores="f.scores" :group="f.group" :detail="true"/>
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