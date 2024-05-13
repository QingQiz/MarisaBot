<script setup lang="ts">

import {onMounted, ref} from "vue";
import {
    cat_rice,
    cat_noodle,
    cat_slide,
    cat_control,
    Chart,
    GetMaxTick,
    Parse,
    ScaleTickFrom, SplitChartAt
} from "@/components/chunithm/utils/parser";
import {useRoute} from "vue-router";
import {context_get} from "@/GlobalVars";
import axios from "axios";
import {Beat, Div, Measure, Noodle, Rice, Slide, SpeedVelocity} from "@/components/chunithm/utils/parser_t";

const route = useRoute()

let id           = ref(route.query.id)
let name         = ref(route.query.name)
let data_fetched = ref(false);

let chart = ref({} as Chart);
let index = ref([] as number[][]);

const overflow    = 50;
const pixel_ratio = ref(window.devicePixelRatio);

axios(name.value ? `/assets/chunithm/chart/${name.value}.c2s` : context_get, {params: {id: id.value, name: 'chart'}})
    .then(data => {
        let c = Parse(data.data);

        [chart.value, index.value] = ProcessChart4Display(c);
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
    let measures = chart.BEAT_1 as Measure[];
    let beats    = new Map<number, Beat[]>();
    let split    = [] as [number, number][];

    for (let x of chart.BEAT_2 as Beat[]) {
        if (!beats.has(x.measure_id)) beats.set(x.measure_id, []);

        beats.get(x.measure_id)?.push(x);
    }

    beats.forEach((value, _) => {
        value.sort((a, b) => a.tick - b.tick);

        for (let i = 8; i < value.length; i += 8) {
            split.push([value[i].tick, 8]);
        }
    });

    for (let i = 0; i < measures.length; i++) {
        split.push([measures[i].tick, measures[i].met.first]);
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

function ProcessChart4Display(chart: Chart): [Chart, number[][]] {
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

    for (let point of split_points) {
        SplitChartAt(chart, point);
        if (overflow > 0) SplitChartAt(chart, point + overflow);
    }

    let range = [[0, split_points[0] + overflow]];
    for (let i = 0; i < split_points.length; i++) {
        range.push([Math.floor(split_points[i]), Math.floor(split_points[i + 1] ?? max_tick) + overflow]);
    }

    return [chart, range];
}

function GetNotes(key: string, tick: number, tick_next: number = 0) {
    if (!chart.value[key]) return [];

    return chart.value[key].filter(note => note.tick >= tick && note.tick < tick_next);
}

function listenOnDevicePixelRatio() {
    function onChange() {
        pixel_ratio.value = window.devicePixelRatio;
        listenOnDevicePixelRatio();
    }

    matchMedia(
        `(resolution: ${window.devicePixelRatio}dppx)`
    ).addEventListener("change", onChange, {once: true});
}

onMounted(listenOnDevicePixelRatio);
</script>


<template>
    <div v-if="data_fetched" class="config stage-container" :style="`--pixel-ratio: ${pixel_ratio}`">
        <div class="stage" v-for="i in index"
             :style="`--tick-min: ${Math.floor(i[0])}; --tick-max: ${Math.floor(i[1])}`">
            <div v-for="type in cat_slide">
                <div v-for="note in GetNotes(type, i[0], i[1]) as Slide[]"
                     :class="`${type} ${typeof note.extra == 'string' ? note.extra : ''}`"
                     :style="`--tick:${Math.floor(note.tick)}; --cell:${note.cell}; --width:${note.width}; --tick-end:${Math.floor(note.tick_end)}; --target-cell:${note.cell_target}; --target-width:${note.width_target}; `">
                </div>
            </div>
            <div v-for="type in cat_noodle">
                <div v-for="note in GetNotes(type, i[0], i[1]) as Noodle[]"
                     :class="type"
                     :style="`--tick:${Math.floor(note.tick)}; --cell:${note.cell}; --width:${note.width}; --tick-end:${Math.floor(note.tick_end)}`">
                </div>
            </div>
            <div v-for="type in cat_rice">
                <div v-for="note in GetNotes(type, i[0], i[1]) as Rice[]"
                     :class="type"
                     :style="`--tick:${Math.floor(note.tick)}; --cell:${note.cell}; --width:${note.width}`">
                </div>
            </div>
            <div v-for="type in cat_control">
                <div v-for="note in GetNotes(type, i[0], i[1])"
                     :class="type"
                     :style="`--tick:${Math.floor(note.tick)}; --content:'${note}'`">
                </div>
            </div>
            <div>
                <div v-for="note in (GetNotes('SFL', i[0], i[1]) as SpeedVelocity[]).filter(x => x.velocity != 1)"
                     class="SFL"
                     :style="`--tick:${note.tick}; --tick-end: ${note.tick_end}; border-color: ${GetColor(note.velocity)}`">
                    {{ note.velocity }}
                </div>
            </div>
            <div>
                <div v-for="note in (GetNotes('DIV', i[0], i[1]) as Div[])"
                     class="DIV"
                     :style="`--tick:${Math.floor(note.tick)}; --content:'${note.first}/${note.second}'`">
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped lang="postcss" src="../../assets/css/chunithm/preview.pcss"></style>


<script lang="ts">
import * as d3 from "d3";

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

const SvColor = d3.scaleLinear<string>()
    .domain([-1, 0, 1, 3, 100, 500])
    .range(['#ff00f2', '#0000ff', '#00ff00', '#ff0000', "#520101", "#000000"])
    .interpolate(d3.interpolateRgb.gamma(2.2))

function GetColor(val: number) {
    return SvColor(val);
}

function GetMidValue(arr: number[]) {
    let copy   = arr.slice();
    let ignore = 5;

    copy.sort((a, b) => a - b);

    return copy.slice(ignore, copy.length - 2 * ignore).reduce((a, b) => a + b, 0) / (copy.length - 2 * ignore);
}
</script>