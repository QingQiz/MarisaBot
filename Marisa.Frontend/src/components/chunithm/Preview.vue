<script setup lang="ts">

import {computed, ref} from "vue";
import {
    cat_noodle,
    cat_rice,
    cat_slide,
    Chart,
    GetMaxTick,
    Parse,
    ScaleTickFrom,
} from "@/components/chunithm/utils/parser";
import {useRoute} from "vue-router";
import {context_get} from "@/GlobalVars";
import axios from "axios";
import BeatmapVisualizer from "@/components/utils/BeatmapVisualizer/BeatmapVisualizer.vue";
import {
    Beatmap,
    BeatmapBeat, BeatmapBpm,
    BeatmapDiv, BeatmapLn,
    BeatmapMeasure, BeatmapRice, BeatmapSlide,
    BeatmapSpeedVelocity
} from "@/components/utils/BeatmapVisualizer/BeatmapTypes";
import {zip} from "@/utils/list";

const route = useRoute()

let id           = ref(route.query.id)
let name         = ref(route.query.name)
let data_fetched = ref(false);

let chart  = ref({} as Chart);
let index  = ref([] as number[]);
let length = ref(0);

axios(name.value ? `/assets/chunithm/chart/${name.value}.c2s` : context_get, {params: {id: id.value, name: 'chart'}})
    .then(data => {
        let c = Parse(data.data);

        [chart.value, index.value, length.value] = ProcessChart4Display(c);
    })
    .finally(() => data_fetched.value = true);

function GetLaneLength(mid_length: number) {
    return mid_length * 4;
}

/**
 * 将谱面根据小节划分，并将过长的小节以4拍为步长进行进一步切分
 * @param chart
 * @return，每个切分的起始tick，和这个切分最多 ***可能*** 有多少拍
 */
function GetMinSplit(chart: Chart) {
    let measures = chart['BEAT_1'] as BeatmapMeasure[];
    let beats    = new Map<number, BeatmapBeat[]>();
    let split    = [] as [number, number][];

    for (let x of chart['BEAT_2'] as BeatmapBeat[]) {
        if (!beats.has(x.MeasureId)) beats.set(x.MeasureId, []);

        beats.get(x.MeasureId)?.push(x);
    }

    beats.forEach((value, _) => {
        value.sort((a, b) => a.Tick - b.Tick);

        for (let i = 8; i < value.length; i += 8) {
            split.push([value[i].Tick, 8]);
        }
    });

    for (let i = 0; i < measures.length; i++) {
        split.push([measures[i].Tick, measures[i].Met.First]);
    }

    return split;
}

function GetMeasureLength(split_tick: number[], max_tick: number) {
    split_tick.sort((a, b) => a - b);

    let res: number[] = [];

    for (let i = 0; i < split_tick.length - 1; i++) {
        res.push(split_tick[i + 1] - split_tick[i]);
    }
    res.push(max_tick - split_tick[split_tick.length - 1]);

    return res;
}

function GetMeasureMidLength(min_split: [number, number][], max_tick: number) {
    let res  = [] as number[];
    let time = min_split.filter(x => x[1] != 1).map(x => x[0]);

    for (let i = 0; i < time.length - 1; i++) {
        res.push(time[i + 1] - time[i]);
    }
    res.push(max_tick - time[time.length - 1]);

    return GetMidValue(res);
}

function ProcessChart4Display(chart: Chart): [Chart, number[], number] {
    let max_tick = GetMaxTick(chart);
    let split    = GetMinSplit(chart);
    let mid_len  = GetMeasureMidLength(split, max_tick);

    ScaleTickFrom(chart, 700 / mid_len);

    split    = GetMinSplit(chart);
    max_tick = GetMaxTick(chart);
    mid_len  = GetMeasureMidLength(split, max_tick);

    let measure_length            = GetMeasureLength(split.map(x => x[0]), max_tick);
    let split_points              = GetSplitPoint(measure_length, GetLaneLength(mid_len));
    let measure_length_prefix_sum = new Array(measure_length.length);

    measure_length_prefix_sum[0] = measure_length[0];
    for (let i = 1; i < measure_length.length; i++) {
        measure_length_prefix_sum[i] = measure_length_prefix_sum[i - 1] + measure_length[i];
    }

    split_points = split_points.map(x => measure_length_prefix_sum[x]);

    return [chart, split_points, max_tick];
}

function Get(cats: string[]) {
    let res = [] as [Beatmap, string][];

    for (let cat of cats) {
        let c = chart.value[cat];

        for (let x of c) {
            res.push([x, cat]);
        }
    }
    return res.length == 0 ? [[], []] : zip(res);
}

let rices  = computed(() => Get(cat_rice));
let lns    = computed(() => Get(cat_noodle));
let slides = computed(() => Get(cat_slide));
</script>


<template>
    <div v-if="data_fetched">
        <BeatmapVisualizer
            :split="index"
            :measure="chart['BEAT_1'] as BeatmapMeasure[]"
            :beat="chart['BEAT_2'] as BeatmapBeat[]"
            :sv="chart['SFL'] as BeatmapSpeedVelocity[]"
            :div="chart['DIV'] as BeatmapDiv[]"
            :bpm="chart['BPM'] as BeatmapBpm[]"
            :rice="rices[0] as BeatmapRice[]"
            :rice_display="rices[1] as string[]"
            :ln="lns[0] as BeatmapLn[]"
            :ln_display="lns[1] as string[]"
            :slide="slides[0] as BeatmapSlide[]"
            :slide_display="slides[1] as string[]"
            :length="length"
            :overflow="50"
        >
            <template v-for="cat in cat_rice" :key="cat" #[cat]>
                <div class="cat-rice-common" :class="cat"/>
            </template>

            <template v-for="cat in cat_noodle" :key="cat" #[cat]>
                <div :class="cat"/>
            </template>

            <template v-for="cat in cat_slide" :key="cat" #[cat]="{note}">
                <div :class="[cat, note.Color]"/>
            </template>
        </BeatmapVisualizer>
    </div>
</template>

<style scoped lang="postcss" src="../../assets/css/chunithm/preview.pcss"></style>
<style scoped lang="postcss" src="../../assets/css/chunithm/preview.rice.pcss"></style>
<style scoped lang="postcss" src="../../assets/css/chunithm/preview.ln.pcss"></style>
<style scoped lang="postcss" src="../../assets/css/chunithm/preview.slide.pcss"></style>


<script lang="ts">
/**
 * 使用贪心策略将小节按顺序合并为最接近 `lane_length` 的若干个序列
 * @param arr
 * @param lane_length
 * @return 一系列坐标，表示切分的位置。将每个切分合并即为最终的结果
 */
function GetSplitPoint(arr: number[], lane_length: number) {
    let prefix_sum = new Array(arr.length + 1);
    prefix_sum[0]  = 0;

    for (let i = 0; i < arr.length; i++) {
        prefix_sum[i + 1] = prefix_sum[i] + arr[i];
    }

    let res = [-1]

    for (let i = 0; i < arr.length - 1; i++) {
        let pre = prefix_sum[res[res.length - 1] + 1];

        let current = prefix_sum[i + 1] - pre;
        let next    = prefix_sum[i + 2] - pre;

        if (Math.abs(current - lane_length) <= Math.abs(next - lane_length)) {
            res.push(i);
        }
    }

    return res.slice(1);
}

function GetMidValue(arr: number[]) {
    let copy   = arr.slice();
    let ignore = 5;

    copy.sort((a, b) => a - b);

    return copy.slice(ignore, copy.length - 2 * ignore).reduce((a, b) => a + b, 0) / (copy.length - 2 * ignore);
}
</script>