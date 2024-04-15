<script setup lang="ts">

import {ref} from "vue";
import {
    cat_1,
    cat_2,
    cat_3,
    cat_4,
    color_map,
    Chart,
    GetMaxTick,
    Parse,
    ScaleTick, SplitChartAt,
} from "@/components/chunithm/utils/parser";
import {useRoute} from "vue-router";
import {context_get} from "@/GlobalVars";
import axios from "axios";

const route = useRoute()

let id           = ref(route.query.id)
let name         = ref(route.query.name)
let data_fetched = ref(false);

let chart    = ref({} as Chart);
let tick_max = ref(0);
let index    = ref([] as number[][]);
let split_to = 20;


axios(name.value ? `/assets/chunithm/chart/${name.value}.c2s` : context_get, {params: {id: id.value, name: 'chart'}})
    .then(data => {
        let c = Parse(data.data);

        [chart.value, tick_max.value, index.value] = ProcessChart4Display(c);
    })
    .finally(() => data_fetched.value = true);


function GetMeasureLength(chart: Chart, max_tick: number) {
    let res: number[] = []

    let time = chart.BEAT_1 // .concat(chart.BEAT_2);
    time.sort((a, b) => a[0] - b[0]);

    for (let i = 0; i < time.length - 1; i++) {
        res.push(time[i + 1][0] - time[i][0]);
    }
    res.push(max_tick - time[time.length - 1][0]);

    return res;
}

function ProcessChart4Display(chart: Chart): [Chart, number, number[][]] {
    let max_tick       = GetMaxTick(chart);
    let measure_length = GetMeasureLength(chart, max_tick);
    let average        = measure_length.reduce((a, b) => a + b, 0) / measure_length.length;

    ScaleTick(chart, 384 / average);

    max_tick       = GetMaxTick(chart);
    measure_length = GetMeasureLength(chart, max_tick);

    let split_points              = GetSplitPoint(measure_length, split_to);
    let measure_length_prefix_sum = new Array(measure_length.length);

    measure_length_prefix_sum[0] = measure_length[0];
    for (let i = 1; i < measure_length.length; i++) {
        measure_length_prefix_sum[i] = measure_length_prefix_sum[i - 1] + measure_length[i];
    }

    split_points = split_points.map(x => measure_length_prefix_sum[x]);

    for (let point of split_points) {
        SplitChartAt(chart, point);
    }

    let range = [[0, split_points[0]]];
    for (let i = 0; i < split_points.length; i++) {
        range.push([Math.floor(split_points[i]), Math.floor(split_points[i + 1] ?? max_tick)]);
    }

    return [chart, max_tick, range];
}

function GetNotes(key: string, tick: number, tick_next: number = 0) {
    if (!chart.value[key]) return [];

    return chart.value[key].filter(note => note[0] >= tick && note[0] < tick_next);
}
</script>


<template>
    <div v-if="data_fetched" class="config stage-container">
        <div class="stage" v-for="i in index"
             :style="`--tick-min: ${i[0]}; --tick-max: ${i[1]}`">
            <div v-for="type in cat_3">
                <div v-for="note in GetNotes(type, i[0], i[1])"
                     :class="`${type} ${note[6] >= 0 ? color_map[note[6]] : ''}`"
                     :style="`--tick:${Math.floor(note[0])}; --cell:${note[1]}; --width:${note[2]}; --tick-end:${Math.floor(note[3])}; --target-cell:${note[4]}; --target-width:${note[5]}; `">
                </div>
            </div>
            <div v-for="type in cat_2">
                <div v-for="note in GetNotes(type, i[0], i[1])"
                     :class="type"
                     :style="`--tick:${Math.floor(note[0])}; --cell:${note[1]}; --width:${note[2]}; --tick-end:${Math.floor(note[3])}`">
                </div>
            </div>
            <div v-for="type in cat_1">
                <div v-for="note in GetNotes(type, i[0], i[1])"
                     :class="type"
                     :style="`--tick:${Math.floor(note[0])}; --cell:${note[1]}; --width:${note[2]}`">
                </div>
            </div>
            <div v-for="type in cat_4">
                <div v-for="note in GetNotes(type, i[0], i[1])"
                     :class="type"
                     :style="`--tick:${Math.floor(note[0])};`">
                    {{ type == "BEAT_1" ? "#" : "" }}{{ note[1] }}
                </div>
            </div>
            <div v-for="note in GetNotes('SFL', i[0], i[1]).filter(x => x[2] != 1)"
                 :class="`SFL ${note[2] >= 0 ? 'UP' : 'DOWN'}`"
                 :style="`--tick:${Math.floor(note[0])}; --duration: ${note[1]}; border-color: ${GetColor(note[2], 0, 2)}`">
                {{ note[2] }}
            </div>
        </div>
    </div>
</template>

<style scoped lang="postcss" src="../../assets/css/chunithm/preview.pcss"></style>


<script lang="ts">
function GetSplitPoint(arr: number[], n: number) {
    let prefix_sum = new Array(arr.length + 1);
    prefix_sum[0]  = 0;

    for (let i = 0; i < arr.length; i++) {
        prefix_sum[i + 1] = prefix_sum[i] + arr[i];
    }

    let avg = prefix_sum[arr.length - 1] / n;
    let sum = 0;

    let res = [-1]

    for (let i = 0; i < arr.length - 1; i++) {
        let pre = prefix_sum[res[res.length - 1] + 1];

        let current = prefix_sum[i + 1] - pre;
        let next    = prefix_sum[i + 2] - pre;

        if (Math.abs(current - avg) <= Math.abs(next - avg)) {
            res.push(i);
            sum += current;
            avg = sum / (res.length - 1);
        }
    }

    return res.slice(1);
}

function GetColor(val: number, min: number, max: number) {
    if (val < 0) val = -val;

    if (val > max) val = max;

    let normalizedValue = (val - min) / (max - min);
    let r, g, b;

    const blue  = [0, 0, 255]; // 蓝色
    const green = [0, 255, 0]; // 绿色
    const red   = [255, 0, 0]; // 红色

    if (normalizedValue < 0.5) {
        // 在蓝色到绿色的过渡阶段，使用 normalizedValue 的两倍来作为插值因子
        let t = normalizedValue * 2;
        r     = Math.round((1 - t) * blue[0] + t * green[0]);
        g     = Math.round((1 - t) * blue[1] + t * green[1]);
        b     = Math.round((1 - t) * blue[2] + t * green[2]);
    } else {
        // 在绿色到红色的过渡阶段，使用 normalizedValue 减去 0.5 的两倍来作为插值因子
        let t = (normalizedValue - 0.5) * 2;
        r     = Math.round((1 - t) * green[0] + t * red[0]);
        g     = Math.round((1 - t) * green[1] + t * red[1]);
        b     = Math.round((1 - t) * green[2] + t * red[2]);
    }

    // 返回rgb颜色字符串
    return `rgb(${r},${g},${b})`;
}
</script>