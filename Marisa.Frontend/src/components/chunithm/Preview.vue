<script setup lang="ts">

import {ref} from "vue";
import {
    cat_rice,
    cat_noodle,
    cat_slide,
    cat_control,
    Chart,
    GetMaxTick,
    Parse,
    ScaleTickFrom, SplitChartAt,
} from "@/components/chunithm/utils/parser";
import {useRoute} from "vue-router";
import {context_get} from "@/GlobalVars";
import axios from "axios";
import {Noodle, Rice, Slide, SpeedVelocity} from "@/components/chunithm/utils/parser_t";

const route = useRoute()

let id           = ref(route.query.id)
let name         = ref(route.query.name)
let data_fetched = ref(false);

let chart = ref({} as Chart);
let index = ref([] as number[][]);


axios(name.value ? `/assets/chunithm/chart/${name.value}.c2s` : context_get, {params: {id: id.value, name: 'chart'}})
    .then(data => {
        let c = Parse(data.data);

        [chart.value, index.value] = ProcessChart4Display(c);
    })
    .finally(() => data_fetched.value = true);


function GetMeasureLength(chart: Chart, max_tick: number) {
    let res: number[] = []

    let time = chart.BEAT_1 // .concat(chart.BEAT_2);
    time.sort((a, b) => a.tick - b.tick);

    for (let i = 0; i < time.length - 1; i++) {
        res.push(time[i + 1].tick - time[i].tick);
    }
    res.push(max_tick - time[time.length - 1].tick);

    return res;
}

function ProcessChart4Display(chart: Chart): [Chart, number[][]] {
    let max_tick       = GetMaxTick(chart);
    let measure_length = GetMeasureLength(chart, max_tick);
    let average        = measure_length.reduce((a, b) => a + b, 0) / measure_length.length;

    ScaleTickFrom(chart, 700 / average);

    max_tick       = GetMaxTick(chart);
    measure_length = GetMeasureLength(chart, max_tick);

    let split_points              = GetSplitPoint(measure_length);
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
        range.push([Math.floor(split_points[i]), Math.floor(split_points[i + 1] ?? max_tick + 20)]);
    }

    return [chart, range];
}

function GetNotes(key: string, tick: number, tick_next: number = 0) {
    if (!chart.value[key]) return [];

    return chart.value[key].filter(note => note.tick >= tick && note.tick < tick_next);
}
</script>


<template>
    <div v-if="data_fetched" class="config stage-container">
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
                     :style="`--tick:${Math.floor(note.tick)};`">
                    {{ note }}
                </div>
            </div>
            <div v-for="note in (GetNotes('SFL', i[0], i[1]) as SpeedVelocity[]).filter(x => x.velocity != 1)"
                 class="SFL"
                 :style="`--tick:${note.tick}; --tick-end: ${note.tick_end}; border-color: ${GetColor(note.velocity)}`">
                {{ note.velocity }}
            </div>
        </div>
    </div>
</template>

<style scoped lang="postcss" src="../../assets/css/chunithm/preview.pcss"></style>


<script lang="ts">
import * as d3 from "d3";

function GetSplitPoint(arr: number[]) {
    let prefix_sum = new Array(arr.length + 1);
    prefix_sum[0]  = 0;

    for (let i = 0; i < arr.length; i++) {
        prefix_sum[i + 1] = prefix_sum[i] + arr[i];
    }

    let avg = prefix_sum[arr.length] / arr.length * 4;
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

const SvColor = d3.scaleLinear<string>()
    .domain([-1, 0, 1, 3, 100, 500])
    .range(['#ff00f2', '#0000ff', '#00ff00', '#ff0000', "#520101", "#000000"])
    .interpolate(d3.interpolateRgb.gamma(2.2))

function GetColor(val: number) {
    return SvColor(val);
}
</script>