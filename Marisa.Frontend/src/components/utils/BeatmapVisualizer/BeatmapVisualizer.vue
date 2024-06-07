<script setup lang="ts">

import {
    BeatmapBeat,
    BeatmapBpm,
    BeatmapLn,
    BeatmapRice,
    BeatmapSlide, BeatmapSpeedVelocity, BeatmapDiv, BeatmapMeasure
} from "@/components/utils/BeatmapVisualizer/BeatmapTypes";
import {computed, onMounted, ref} from "vue";
import {range, zip} from "@/utils/list";


let props = defineProps({
    length       : {
        type    : Number,
        required: true
    },
    rice         : {
        type    : Array as () => BeatmapRice[],
        required: true
    },
    ln           : {
        type    : Array as () => BeatmapLn[],
        required: true
    },
    slide        : {
        type    : Array as () => BeatmapSlide[],
        required: true
    },
    rice_display : {
        type    : Array as () => string[],
        required: false,
        default : undefined
    },
    ln_display   : {
        type    : Array as () => string[],
        required: false,
        default : undefined
    },
    slide_display: {
        type    : Array as () => string[],
        required: false,
        default : undefined
    },
    bpm          : {
        type    : Array as () => BeatmapBpm[],
        required: false,
        default : []
    },
    sv           : {
        type    : Array as () => BeatmapSpeedVelocity[],
        required: false,
        default : []
    },
    beat         : {
        type    : Array as () => BeatmapBeat[],
        required: false,
        default : []
    },
    measure      : {
        type    : Array as () => BeatmapMeasure[],
        required: false,
        default : []
    },
    div          : {
        type    : Array as () => BeatmapDiv[],
        required: false,
        default : []
    },
    split        : {
        type    : Array as () => number[],
        required: false,
        default : []
    },
    overflow     : {
        type    : Number,
        required: false,
        default : 0
    },
    /**
     * 是否裁剪超出stage的部分，
     * true:  调整长条的Tick/TickEnd，让其不超出stage。NOTE: 会破坏长条的完整性，例如当长条从下到上是渐变的话，会重置渐变
     * false: 不调整，超出部分会超出，需要自行处理。
     */
    cut: {
        type    : Boolean,
        required: false,
        default : true,
    }
})

let rice = computed(() => zip([props.rice, range(props.rice.length)])
    .map(x => [x[0], props.rice_display ? props.rice_display[x[1]] : "rice"] as [BeatmapRice, string])
);

let ln = computed(() => zip([props.ln, range(props.ln.length)])
    .map(x => [x[0], props.ln_display ? props.ln_display[x[1]] : "ln"] as [BeatmapLn, string])
);

let slide = computed(() => zip([props.slide, range(props.slide.length)])
    .map(x => [x[0], props.slide_display ? props.slide_display[x[1]] : "slide"] as [BeatmapSlide, string])
)

let pixel_ratio = ref(window.devicePixelRatio);

function GetRange() {
    let range = [[-props.overflow, Math.floor(props.split[0]) + props.overflow]];
    for (let i = 0; i < props.split.length; i++) {
        range.push([Math.floor(props.split[i]) - props.overflow, Math.floor(props.split[i + 1] ?? props.length) + props.overflow]);
    }
    return range;
}

function GetRice(l: number, r: number) {
    return rice.value.filter(x => x[0].Tick >= l && x[0].Tick < r);
}

function GetLn(l: number, r: number) {
    return GetAndSplit(ln.value.map(x => ({...x[0], ext: x[1]})), l, r, props.cut)
        .map(x => [x as BeatmapLn, x.ext] as [BeatmapLn, string])
}

function GetSlide(l: number, r: number) {
    return GetAndSplit(slide.value.map(x => ({...x[0], ext: x[1]})), l, r, props.cut)
        .map(x => [x as unknown as BeatmapSlide, x.ext] as [BeatmapSlide, string])
}

function GetBpm(l: number, r: number) {
    return props.bpm.filter(x => x.Tick >= l && x.Tick < r);
}

function GetSv(l: number, r: number) {
    return GetAndSplit(props.sv, l, r);
}

function GetBeat(l: number, r: number) {
    return props.beat.filter(x => x.Tick >= l && x.Tick < r);
}

function GetMeasure(l: number, r: number) {
    return props.measure.filter(x => x.Tick >= l && x.Tick < r);
}

function GetDiv(l: number, r: number) {
    return props.div.filter(x => x.Tick >= l && x.Tick < r);
}

function ListenOnDevicePixelRatio() {
    function onChange() {
        pixel_ratio.value = window.devicePixelRatio;
        ListenOnDevicePixelRatio();
    }

    matchMedia(
        `(resolution: ${window.devicePixelRatio}dppx)`
    ).addEventListener("change", onChange, {once: true});
}

function RiceStyle(r: BeatmapRice, i: number[]) {
    return {
        '--width': `${r.Width * 100}%`,
        '--x'    : `${r.X * 100}%`,
        '--y'    : `${CalcY(r.Tick, i)}`
    }
}

function LnStyle(l: BeatmapLn, i: number[]) {
    return {
        '--width': `${l.Width * 100}%`,
        '--x'    : `${l.X * 100}%`,
        '--y'    : `${CalcY(l.Tick, i)}`,
        '--y-end': `${CalcY(l.TickEnd, i)}`
    }
}

function SlideStyle(s: BeatmapSlide, i: number[]) {
    return {
        '--x'        : `${s.Border()[0] * 100}%`,
        '--width'    : `${(s.Border()[1] - s.Border()[0]) * 100}%`,
        '--y'        : `${CalcY(s.Tick, i)}`,
        '--y-end'    : `${CalcY(s.TickEnd, i)}`,
        '--width-end': `${s.WidthEnd * 100}%`,
        '--clip-lb'  : `${(s.X - s.Border()[0]) / (s.Border()[1] - s.Border()[0]) * 100}%`,
        '--clip-rb'  : `${(s.X + s.Width - s.Border()[0]) / (s.Border()[1] - s.Border()[0]) * 100}%`,
        '--clip-lt'  : `${(s.XEnd - s.Border()[0]) / (s.Border()[1] - s.Border()[0]) * 100}%`,
        '--clip-rt'  : `${(s.XEnd + s.WidthEnd - s.Border()[0]) / (s.Border()[1] - s.Border()[0]) * 100}%`,
    }
}

onMounted(ListenOnDevicePixelRatio);
</script>


<template>
    <div class="config stage-container" :style="`--pixel-ratio: ${pixel_ratio}`">
        <div class="stage" v-for="i in GetRange()" :style="`--tick-min: ${i[0]}; --tick-max: ${i[1]}`">
            <!--            RICE -->
            <div v-for="n in GetRice(i[0], i[1])" class="note-common" :style="RiceStyle(n[0], i)">
                <slot :name="n[1]" :note="n[0]" :range="i">
                    <div class="rice"/>
                </slot>
            </div>

            <!--            LN -->
            <div v-for="n in GetLn(i[0], i[1])" class="ln-common" :style="LnStyle(n[0], i)">
                <slot :name="n[1]" :note="n[0]" :range="i">
                    <div class="ln">
                        {{n[1]}}
                    </div>
                </slot>
            </div>

            <!--            SLIDE -->
            <div v-for="n in GetSlide(i[0], i[1])" class="slide-common" :style="SlideStyle(n[0], i)">
                <slot :name="n[1]" :note="n[0]" :range="i">
                    <div class="slide"/>
                </slot>
            </div>

            <!--            BPM -->
            <template v-for="b in GetBpm(i[0], i[1])">
                <div class="bpm" :style="`--y: ${CalcY(b.Tick, i)}; --content: '${b.Bpm}'`"/>
            </template>

            <!--            SV -->
            <template v-for="s in GetSv(i[0], i[1])">
                <div class="sv"
                     :style="`--y: ${CalcY(s.Tick, i)}; --y-end: ${CalcY(s.TickEnd, i)}; border-color: ${GetColor(s.Velocity)}`">
                    {{ s.Velocity }}
                </div>
            </template>

            <!--            BEAT -->
            <template v-for="b in GetBeat(i[0], i[1])">
                <div class="beat" :style="`--y: ${CalcY(b.Tick, i)}`"/>
            </template>

            <!--            MEASURE -->
            <template v-for="m in GetMeasure(i[0], i[1])">
                <div class="met" :style="`--y: ${CalcY(m.Tick, i)}; --content:'#${m.Id}'`"/>
            </template>

            <!--            DIV -->
            <template v-for="d in GetDiv(i[0], i[1])">
                <div class="div" :style="`--y: ${CalcY(d.Tick, i)}; --content:'${d.First}/${d.Second}'`"/>
            </template>
        </div>
    </div>
</template>


<style lang="postcss" scoped src="../../../assets/css/utils/BeatmapVisualizer/visualizer.pcss"></style>


<script lang="ts">
import * as d3 from "d3";

const SvColor = d3.scaleLinear<string>()
    .domain([-1, 0, 1, 3, 100, 500])
    .range(['#ff00f2', '#0000ff', '#6B7280', '#ff0000', "#520101", "#000000"])
    .interpolate(d3.interpolateRgb.gamma(2.2))

function GetColor(val: number) {
    return SvColor(val);
}

function GetAndSplit<T extends { Tick: number, TickEnd: number }>(arr: T[], l: number, r: number, cut: boolean = true) {
    //     |-----|
    // |----------------|
    let a = arr.filter(x => (x.Tick >= l && x.TickEnd < r));
    // |-----|
    //     |----------------|
    let b = arr.filter(x => (x.Tick < l && x.TickEnd >= l && x.TickEnd < r));
    // |----------------|
    //     |-----|
    let c = arr.filter(x => (x.Tick < l && x.TickEnd >= r));
    //              |-----|
    // |----------------|
    let d = arr.filter(x => (x.Tick >= l && x.Tick < r && x.TickEnd >= r));

    return cut
        ? [
            ...a,
            ...b.map(x => ({...x, Tick: l})),
            ...c.map(x => ({...x, Tick: l, TickEnd: r})),
            ...d.map(x => ({...x, TickEnd: r})),
        ]
        : [...a, ...b, ...c, ...d];
}

function CalcY(tick: number, range: number[]) {
    return `calc(${Math.floor(tick) - range[0]} / ${range[1] - range[0]} * 100%)`;
}
</script>