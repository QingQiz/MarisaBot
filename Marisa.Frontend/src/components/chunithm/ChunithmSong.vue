<script setup lang="ts">
import axios from "axios";
import {ref} from "vue";
import {useRoute} from "vue-router";
import FallbackImage from "../utils/FallbackImage.vue";
import {context_get} from "@/GlobalVars";

const route = useRoute()

let id = ref(route.query.id)

let song        = ref({} as any)
let data_loaded = ref(false)
axios.get(context_get, {params: {id: id.value, name: 'SongData'}})
    .then(response => {
        song.value = response.data
    })
    .finally(() => data_loaded.value = true)

let level_idx_color_map: { [key: string]: string } = {
    "BASIC"      : "rgb(82, 231, 43)",
    "ADVANCED"   : "rgb(255, 168, 1)",
    "EXPERT"     : "rgb(255, 90, 102)",
    "MASTER"     : "rgb(198, 79, 228)",
    "ULTIMA"     : "rgb(0, 0, 0)",
    "WORLD'S END": "rgb(219, 170, 255)",
}

function reduce_bpm(): [number, number[]] {
    let bpms = [] as number[];

    let reg = new RegExp(/([\d.]+)( \(([\d.]+) - ([\d.]+)\))?/);
    song.value.Beatmaps.forEach((c: any) => {
        if (c != null) {
            let m = c.Bpm.match(reg);
            if (m[2] == undefined) {
                bpms.push(parseFloat(m[1]))
            } else {
                bpms.push(parseFloat(m[1]), parseFloat(m[3]), parseFloat(m[4]));
            }
        }
    });

    let domainBpm = bpms[0];
    // sort bpms
    bpms.sort((a: number, b: number) => a - b);
    // remove duplicate bpms
    bpms = bpms.filter((bpm: number, idx: number) => bpm != bpms[idx - 1]);

    return [domainBpm, bpms];
}


</script>

<template>
    <div v-if="!data_loaded">
        <h1>CHUNITHM Song {{ id }}</h1>
    </div>
    <div v-else>
        <div class="flex">
            <div class="p-[20px]">
                <div class="w-[350px] h-[350px]">
                    <FallbackImage :src="[`/assets/chunithm/cover/${song.Id}.png`]"
                                   :fallback="`/assets/chunithm/cover/0.png`"
                                   class="min-h-full"
                    />
                </div>
            </div>
            <div class="header">
                <div>乐曲名</div>
                <div>演唱/作曲</div>
                <div>BPM</div>
                <div>类别</div>
                <div>版本</div>
            </div>
            <div class="data grow">
                <div class="gap-5">
                    <div class="bg-gray-300 px-2 rounded-2xl">
                        ID:{{ song.Id }}
                    </div>
                    {{ song.Title }}
                </div>
                <div>{{ song.Artist }}</div>
                <template v-for="[domain, x] in [reduce_bpm()]">
                    <div >{{ domain }} <template v-if="x.length > 1">({{ x.join('-') }})</template></div>
                </template>
                <div>{{ song.Genre }}</div>
                <div>{{ song.Version }}</div>
            </div>
        </div>
        <div class="chart">
            <template v-for="(c, i) in song.Beatmaps">
                <div v-if="i == 0" class="chart-header">
                    <div>难度</div>
                    <div>定数</div>
                    <div>
                        <div class="bg-green-200 rounded-full px-3">定数VERSE</div>
                    </div>
                    <div>Combo</div>
                    <div>作图者</div>
                </div>
                <div class="chart-data">
                    <div :style="`background-color: ${level_idx_color_map[c['LevelName']]};`" class="text-white">
                        {{ c['LevelName'] }}
                    </div>
                    <div>{{ c.ConstantOld == 0 ? '' : c.ConstantOld }}</div>
                    <div>
                        {{ c.Constant == 0 ? '' : c.Constant }}
                        <div v-if="c.ConstantOld != 0 && c.Constant != c.ConstantOld && c.Constant != 0" class="text-sm">
                            <template v-for="x in [c.Constant - c.ConstantOld]">
                                <div v-if="x > 0" class="text-green-600">
                                    +{{ x.toFixed(2) }}
                                </div>
                                <div v-else class="text-red-500">
                                    {{ x.toFixed(2) }}
                                </div>
                            </template>
                        </div>
                    </div>
                    <div>{{ c.MaxCombo }}</div>
                    <div class="text-center">{{ c.Charter }}</div>
                </div>
            </template>
        </div>
    </div>
</template>

<style scoped lang="postcss">
.line-style {
    @apply border-gray-400 px-3;
    @apply font-fangSong flex-grow;
    @apply flex items-center text-2xl;

    border-bottom-width: 1px;
    white-space: nowrap;
}

.header {
    @apply flex flex-col;
}

.header > div {
    @apply line-style bg-gray-200 justify-center;
}

.data {
    @apply flex flex-col;
}

.data > div {
    @apply line-style font-fangSong;
}

.chart {
    @apply flex flex-col;
}

.chart > div {
    @apply flex-grow;
}

.chart > div > div:nth-child(1) {
    width: 200px;
    max-width: 200px;
}

.chart > div > div:nth-child(2) {
    width: 100px;
    max-width: 100px;
}

.chart > div > div:nth-child(3) {
    width: 150px;
    max-width: 150px;
}
.chart > div > div:nth-child(4) {
    width: 100px;
    max-width: 100px;
}

.chart > div > div:nth-child(5) {
    min-width: 500px;
}

.chart-header {
    @apply header flex-row;
    flex-direction: row;

    & > div {
        @apply py-1 text-lg;
    }
}

.chart-data {
    @apply data flex;
    flex-direction: row;
    min-height: 68px;
}

.chart-data > div {
    @apply justify-center;
}
</style>