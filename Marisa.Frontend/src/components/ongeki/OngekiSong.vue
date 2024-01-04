<script setup lang="ts">
import axios from "axios";
import { ref } from "vue";
import { useRoute } from "vue-router";
import FallbackImage from "../utils/FallbackImage.vue";

const route = useRoute()

let id = route.params.id

let song = ref({} as any)
let data_loaded = ref(false)

axios.get('/assets/ongeki/ongeki.json')
    .then(response => {
        song.value = response.data.find((song: any) => song.Id == id)
    })
    .finally(() => data_loaded.value = true)

let level_idx_map: { [key: number]: string } = {
    0: "Basic",
    1: "Advanced",
    2: "Expert",
    3: "Master",
    4: "Lunatic",
}

let level_idx_color_map: { [key: number]: string } = {
    0: "rgb(82, 231, 43)",
    1: "rgb(255, 168, 1)",
    2: "rgb(255, 90, 102)",
    3: "rgb(198, 79, 228)",
    4: "rgb(219, 170, 255)",
}

function reduce_bpm(): number[] {
    let bpms = [] as number[];

    song.value.Charts.forEach((c: any) => {
        if (c != null) {
            c.Bpm.split('\t').forEach((bpm: string) => {
                bpms.push(parseFloat(bpm));
            });
        }
    });
    // sort bpms
    bpms.sort((a: number, b: number) => a - b);
    // remove duplicate bpms
    bpms = bpms.filter((bpm: number, idx: number) => bpm != bpms[idx - 1]);

    return bpms;
}

</script>

<template>
    <div v-if="!data_loaded">
        <h1>Ongeki Song {{ id }}</h1>
    </div>
    <div v-else class="flex flex-col gap-[20px]">
        <div class="flex">
            <div class="p-[20px]">
                <div class="w-[350px]">
                    <FallbackImage :src="[`/assets/ongeki/cover/${song.Id}.png`]"
                        :fallback="`/assets/ongeki/cover/0.png`" />
                </div>
            </div>
            <div class="header">
                <div>乐曲名</div>
                <div>演唱/作曲</div>
                <div>BPM</div>
                <div>来源</div>
                <div>类别</div>
                <div>版本</div>
                <div>Boss</div>
            </div>
            <div class="data grow">
                <div class="gap-5">
                    <div class="bg-gray-300 px-2 rounded-2xl">
                        ID:{{ song.Id }}
                    </div>
                    {{ song.Title }}
                </div>
                <div>{{ song.Artist }}</div>
                <div>{{ reduce_bpm().join('-') }}</div>
                <div>{{ song.Source }}</div>
                <div>{{ song.Genre }}</div>
                <div>{{ song.Version }}</div>
                <div>Lv. {{ song.BossLevel }} - {{ song.BossCard }}</div>
            </div>
        </div>
        <div class="chart">
            <template v-for="c, i in song.Charts">
                <div v-if="i == 0" class="chart-header">
                    <div>难度</div>
                    <div>定数</div>
                    <div class="flex flex-col grow py-2">
                        <div class="border-b-[1px] border-gray-400 w-full text-center">NOTE</div>
                        <div class="detail text-xl w-full">
                            <div>TOTAL</div>
                            <div>TAP</div>
                            <div>HOLD</div>
                            <div>SIDE</div>
                            <div>SHOLD</div>
                            <div>FLICK</div>
                        </div>
                    </div>
                    <div>Bell</div>
                    <div>作图者</div>
                </div>
                <div v-if="c == null && i != 4" class="chart-data-disable"></div>
                <div v-if="c != null" class="chart-data">
                    <div :style="`background-color: ${level_idx_color_map[i]};`">{{ level_idx_map[i] }}</div>
                    <div>{{ c.Const }}</div>
                    <div>
                        <div class="detail grow">
                            <div>{{ c.NoteCount }}</div>
                            <div class="text-gray-400">{{ c.TapCount }}</div>
                            <div class="text-gray-400">{{ c.HoldCount }}</div>
                            <div class="text-gray-400">{{ c.SideCount }}</div>
                            <div class="text-gray-400">{{ c.SHoldCount }}</div>
                            <div class="text-gray-400">{{ c.FlickCount }}</div>
                        </div>
                    </div>
                    <div>{{ c.BellCount }}</div>
                    <div class="text-center" >{{ c.Creator }}</div>
                </div>
            </template>
        </div>
        <div v-if="song.CopyRight != '-'" class="text-center -m-2" style="white-space: pre;">
            {{ song.CopyRight }}
        </div>
    </div>
</template>

<style scoped>
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

.header>div {
    @apply line-style bg-gray-200 justify-center;
}

.data {
    @apply flex flex-col;
}

.data>div {
    @apply line-style font-fangSong;
}

.chart {
    @apply flex flex-col;
}

.chart>div {
    @apply flex-grow;
}

.chart>div>div:nth-child(1) {
    width: 200px;
    max-width: 200px;
}

.chart>div>div:nth-child(2) {
    width: 100px;
    max-width: 100px;
}

.chart>div>div:nth-child(3) {
    width: 500px;
    max-width: 500px;
}

.chart>div>div:nth-child(4) {
    width: 100px;
    max-width: 100px;
}

.chart-header {
    @apply header flex-row;
    flex-direction: row;
}

.chart-data {
    @apply data flex;
    flex-direction: row;
    min-height: 68px;
}

.chart-data>div {
    @apply justify-center;
}

.chart-data-disable {
    @apply bg-gray-300 line-style;
}

.chart-data-disable::after {
    @apply text-2xl opacity-0;

    content: '1';
    min-height: 68px;
}

.detail {
    display: grid;
    grid-template-columns: repeat(6, minmax(0, 1fr));
}

.detail>div {
    @apply text-center;
}
</style>