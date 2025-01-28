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
</script>

<template>
    <div v-if="data_fetched" class="container">
        <div class="op-container">
            <div>ALL</div>
            <OverPower :scores="songs.map((x: GroupSongInfo) => GetScore(x.Item3.Id, x.Item2))" :group="songs"
                       :detail="true"/>
        </div>
        <template v-for="range in GetConstRange()">
            <template v-for="s in [GetSongsByConstRange(range[0], range[1])]">
                <div v-if="s.length != 0" class="op-container">
                    <div>{{ range[2] }}</div>
                    <OverPower :scores="s.map((x: GroupSongInfo) => GetScore(x.Item3.Id, x.Item2))" :group="s"
                               :detail="true"/>
                </div>
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