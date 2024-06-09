<script setup lang="ts">
import {
    context_get,
    osu_beatmapCover_builder,
    osu_beatmapInfo,
    GetDiffColor,
    GetDiffTextColor,
    host
} from '@/GlobalVars'
import axios from 'axios'
import {computed, ref} from 'vue'
import {useRoute} from 'vue-router'
import BeatmapInfo from './partial/BeatmapInfo.vue';
import {BeatmapInfo as BeatmapInfoT} from './Osu.Data';
import {parse} from "@/components/osu/utils/beatmap_parser";
import BeatmapVisualizer from "@/components/utils/BeatmapVisualizer/BeatmapVisualizer.vue";
import {
    BeatmapBeat, BeatmapBpm,
    BeatmapDiv, BeatmapLn,
    BeatmapMeasure, BeatmapRice, BeatmapSlide,
    BeatmapSpeedVelocity
} from "@/components/utils/BeatmapVisualizer/BeatmapTypes";
import {range} from "@/utils/list";
import {color} from "d3-color";

const data_fetched = ref(false)

let info     = ref({} as BeatmapInfoT)
let max_time = ref(0)

let d       = ref("");
let beatmap = ref({} as ReturnType<typeof parse>);

let color_map = computed(() => {
    let cm = {
        'w': 'rgb(255,255,255)',
        'b': 'rgb(94, 158, 204)',
        'g': 'rgb(229, 195, 73)',
    };

    let color = [
        'w', 'ww', 'wbw', 'wbbw', 'wbgbw', 'wbwwbw', 'wbwgwbw', 'wbwggwbw', 'wbwbgbwbw', 'wbwbggbwbw', 'wbwbgwgbwbw'
    ];

    let key_count = beatmap.value.key_count ?? 0;

    if (key_count > color.length) {
        return new Array(key_count).fill('rgb(255,255,255)');
    }

    return color[key_count - 1].split('').map(x => cm[x as keyof typeof cm]);
})

const beatmapId=  3469849

axios.get("http://localhost:14311/Api/Osu/GetBeatmapById", {params: {beatmapId: beatmapId}})
    .then(data => {
        d.value       = data.data;
        beatmap.value = parse(data.data);
    })
    .then(_ => axios.get(osu_beatmapInfo, { params: { beatmapId: beatmapId } }))
    .then(data => { info.value = data.data })
    .finally(() => data_fetched.value = true)

function GetSplit() {
    let measure_start  = [...beatmap.value.measure.map(x => x.Tick)]//, ...beatmap.value.beat.map(x => x.Tick)];
    let measure_length = [];

    measure_start.sort((a, b) => a - b);

    for (let i = 0; i < measure_start.length - 1; i++) {
        measure_length.push(measure_start[i] - (i == 0 ? 0 : measure_start[i - 1]))
    }
    measure_length.push(beatmap.value.length - measure_start[measure_start.length - 1])

    let lane_length_sum = 5000;
    let lane_length_cnt = 1;

    let idx = [];
    let sum = 0;

    for (let i = 0; i < measure_length.length; i++) {
        let err  = lane_length_sum / lane_length_cnt;
        let err1 = Math.abs(sum - err);
        let err2 = Math.abs(sum + measure_length[i] - err);

        if (err1 < err2) {
            idx.push(i - 1);
            lane_length_cnt++;
            lane_length_sum += sum;
            sum = measure_length[i];
        } else {
            sum += measure_length[i];
        }
    }

    let measure_prefix_sum = new Array(measure_length.length);
    measure_prefix_sum[0]  = measure_length[0];

    for (let i = 1; i < measure_length.length; i++) {
        measure_prefix_sum[i] = measure_prefix_sum[i - 1] + measure_length[i];
    }

    return idx.map(x => measure_prefix_sum[x]);
}
</script>

<template>
    <template v-if="data_fetched">
        <BeatmapVisualizer
            :split="GetSplit()"
            :measure="beatmap.measure"
            :beat="beatmap.beat"
            :sv="beatmap.sv"
            :div="[]"
            :bpm="beatmap.bpm"
            :rice="beatmap.rice.map(x => ({...x, X:  x.X / beatmap.key_count}))"
            :rice_display="beatmap.rice.map(x => `rice-${x.X}`)"
            :ln="beatmap.ln.map(x => ({...x, X: x.X / beatmap.key_count}))"
            :ln_display="beatmap.ln.map(x => `ln-${x.X}`)"
            :slide="[]"
            :length="beatmap.length"
            :overflow="50"
            :cut="false"
        >
            <template v-for="i in range(beatmap.key_count)" :key="`rice-${i}`" #[`rice-${i}`]>
                <div class="z-20">
                    <div :style="`background-color: ${color_map[i]}`" class="h-3 mx-[1px] rounded-[2px]"></div>
                </div>
            </template>

            <template v-for="i in range(beatmap.key_count)" :key="`ln-${i}`" #[`ln-${i}`]>
                <div class="z-10 overflow-hidden">
                    <div class="mx-1 h-full rounded-t-full" :style="`background-color: ${color_map[i]}`"></div>
                </div>
            </template>
        </BeatmapVisualizer>
        <div class="info-container gap-12">
            <div class="title-container gap-8">
                <!-- title -->
                <div class="title">
                    {{ info.beatmapset.title_unicode }}
                </div>
                <!-- artist -->
                <div class="artist">
                    {{ info.beatmapset.artist_unicode }}
                </div>
                <div class="flex text-4xl gap-2">
                    <div class="osu-star-rating"
                         :style="`background-color: ${GetDiffColor(info.difficulty_rating)}; color: ${GetDiffTextColor(info.difficulty_rating)}`">
                        {{ info.difficulty_rating.toFixed(2) }}
                    </div>
                    <!-- diff name -->
                    <div class="text-[#65ccfe]">
                        {{ info.version }}
                    </div>
                    <!-- mapper -->
                    <div>
                        mapped by <span class="font-bold text-[#65ccfe]">{{ info.beatmapset.creator }}</span>
                    </div>
                    <div class="px-3 py-0.5 bg-black rounded-lg w-fit bg-opacity-30 no-shadow">
                        #{{ info.id }}
                    </div>
                </div>
            </div>
            <beatmap-info :beatmap="info" class="osu-preview-beatmap-info" />
        </div>
    </template>
</template>

<style scoped>

</style>
