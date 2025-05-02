<script setup lang="ts">

import {
    BeatmapBeat,
    BeatmapBpm,
    BeatmapLn,
    BeatmapRice,
    BeatmapSlideUnit, BeatmapSpeedVelocity, BeatmapDiv, BeatmapMeasure, BeatmapSpeedVelocity2
} from "@/components/utils/BeatmapVisualizer/BeatmapTypes";
import {computed, onMounted, ref, toRaw} from "vue";
import {Distinct, range, zip} from "@/utils/list";


let props = defineProps({
    length: {
        type    : Number,
        required: true
    },

    rice: {
        type    : Array as () => BeatmapRice[],
        required: true
    },

    ln: {
        type    : Array as () => BeatmapLn[],
        required: true
    },

    slide: {
        type    : Array as () => BeatmapSlideUnit[],
        required: true
    },

    /**
     * 长条的名字，默认是 ln，如果给出了定义，则定义的槽名字为这个值
     */
    rice_display: {
        type    : Array as () => string[],
        required: false,
        default : undefined
    },

    ln_display: {
        type    : Array as () => string[],
        required: false,
        default : undefined
    },

    slide_display: {
        type    : Array as () => string[],
        required: false,
        default : undefined
    },

    bpm: {
        type    : Array as () => BeatmapBpm[],
        required: false,
        default : []
    },

    sv: {
        type    : Array as () => BeatmapSpeedVelocity[],
        required: false,
        default : []
    },

    /**
     * 基于区域的 SV
     */
    sv2: {
        type    : Array as () => BeatmapSpeedVelocity2[],
        required: false,
        default : []
    },

    beat: {
        type    : Array as () => BeatmapBeat[],
        required: false,
        default : []
    },

    measure: {
        type    : Array as () => BeatmapMeasure[],
        required: false,
        default : []
    },

    div: {
        type    : Array as () => BeatmapDiv[],
        required: false,
        default : []
    },

    split: {
        type    : Array as () => number[],
        required: false,
        default : []
    },

    overflow: {
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
    .map(x => [x[0], props.slide_display ? props.slide_display[x[1]] : "slide"] as [BeatmapSlideUnit, string])
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
    return GetAndSplitAndScale(
        slide.value.map(x => ({...x[0], ext: x[1]})) as any as BeatmapSlideUnit[], l, r, props.cut
    ).map(x => [x as unknown as BeatmapSlideUnit, (x as any).ext] as [BeatmapSlideUnit, string])
}

function GetBpm(l: number, r: number) {
    return props.bpm.filter(x => x.Tick >= l && x.Tick < r);
}

function GetSv(l: number, r: number) {
    return GetAndSplit(props.sv, l, r);
}

function GetSv2(l: number, r: number) {
    let res = GetAndSplit(toRaw(props.sv2), l, r);
    for (let sv2 of res) {
        sv2.SVs = GetAndSplit(sv2.SVs, l, r, props.cut);
    }
    return res;
}

function GetAllSv2Velocity() {
    let res = Distinct(props.sv2.flatMap(x => x.SVs).map(x => x.Velocity), (x, y) => x == y);
    res.sort((a, b) => b - a);
    return res;
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

function SvStyle(sv: BeatmapSpeedVelocity, range: number[], idx: number = 0) {
    return {
        '--y'         : CalcY(sv.Tick, range),
        '--y-end'     : CalcY(sv.TickEnd, range),
        'border-color': GetColor(sv.Velocity),
        '--idx'       : idx
    }
}


function Sv2Style(sv2: BeatmapSpeedVelocity2, sv: BeatmapSpeedVelocity, range: number[]) {
    return {
        '--width': `${sv2.Width * 100}%`,
        '--x'    : `${sv2.X * 100}%`,
        '--y'    : `${CalcY(sv.Tick, range)}`,
        '--y-end': `${CalcY(sv.TickEnd, range)}`,
        '--color': `${GetColor(sv.Velocity)}`,
    }
}

function SlideStyle(s: BeatmapSlideUnit, i: number[]) {
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
        <template v-for="vs in [GetAllSv2Velocity()]">
            <div v-if="vs.length != 0" class="my-5 mx-3 flex text-xl">
                <div class="" style="writing-mode: sideways-lr">
                    SV2 Color Index
                </div>
                <div style="background-color: var(--stage-color)">
                    <div class="w-[60px] h-[60px] flex items-center justify-center text-gray-200"
                         :style="`background-color: ${GetColor(x)}`" v-for="x in vs">
                        x{{ x }}
                    </div>
                </div>
            </div>
        </template>
        <div class="stage" v-for="sRange in GetRange()" :style="`--tick-min: ${sRange[0]}; --tick-max: ${sRange[1]}`">
            <!--            RICE -->
            <div v-for="n in GetRice(sRange[0], sRange[1])" class="note-common" :style="RiceStyle(n[0], sRange)">
                <slot :name="n[1]" :note="n[0]" :range="sRange">
                    <div class="rice"/>
                </slot>
            </div>

            <!--            LN -->
            <div v-for="n in GetLn(sRange[0], sRange[1])" class="ln-common" :style="LnStyle(n[0], sRange)">
                <slot :name="n[1]" :note="n[0]" :range="sRange">
                    <div class="ln"/>
                </slot>
            </div>

            <!--            SLIDE -->
            <div v-for="n in GetSlide(sRange[0], sRange[1])" class="slide-common" :style="SlideStyle(n[0], sRange)">
                <slot :name="n[1]" :note="n[0]" :range="sRange">
                    <div class="slide"/>
                </slot>
            </div>

            <!--            BPM -->
            <template v-for="b in GetBpm(sRange[0], sRange[1])">
                <div class="bpm" :style="`--y: ${CalcY(b.Tick, sRange)}; --content: '${b.Bpm.toFixed(0)}'`"/>
            </template>

            <!--            SV -->
            <template v-for="sv in GetSv(sRange[0], sRange[1])">
                <div class="sv" :style="SvStyle(sv, sRange)">
                    {{ sv.Velocity.toFixed(2) }}
                </div>
            </template>

            <!--            SV2 -->
            <template v-for="sv2s in [GetSv2(sRange[0], sRange[1])]">
                <template v-for="sv2 in sv2s">
                    <template v-for="sv in sv2.SVs">
                        <div class="sv2" :style="Sv2Style(sv2, sv, sRange)"></div>
                    </template>
                </template>
            </template>

            <!--            BEAT -->
            <template v-for="b in GetBeat(sRange[0], sRange[1])">
                <div class="beat" :style="`--y: ${CalcY(b.Tick, sRange)}`"/>
            </template>

            <!--            MEASURE -->
            <template v-for="m in GetMeasure(sRange[0], sRange[1])">
                <div class="met" :style="`--y: ${CalcY(m.Tick, sRange)}; --content:'#${m.Id}'`"/>
            </template>

            <!--            DIV -->
            <template v-for="d in GetDiv(sRange[0], sRange[1])">
                <div class="div" :style="`--y: ${CalcY(d.Tick, sRange)}; --content:'${d.First}/${d.Second}'`"/>
            </template>
        </div>
    </div>
</template>


<style lang="postcss" scoped src="../../../assets/css/utils/BeatmapVisualizer/visualizer.pcss"></style>


<script lang="ts">
import * as d3 from "d3";

const SvColor = d3.scaleLinear<string>()
    .domain([-10, -1, 0, 1, 3, 100, 500])
    .range([
        '#ff00f2',
        '#ff82f9', '#0000ff',
        '#6B7280', '#ff0000',
        "#520101", "#000000"
    ])
    .interpolate(d3.interpolateRgb.gamma(2.2))

function GetColor(val: number) {
    return SvColor(val);
}

/**
 * @param arr note数组，包括ln、slide、sv等
 * @param l stage 起始tick
 * @param r stage 结束tick
 * @param cut 是否直接裁剪超出stage的部分
 */
function GetAndSplit<T extends { Tick: number, TickEnd: number }>(arr: T[], l: number, r: number, cut: boolean = true) {
    // NOTE :   |-----|
    // STAGE: |----------------|
    let a = arr.filter(x => (x.Tick >= l && x.TickEnd < r));
    // NOTE : |-----|
    // STAGE:    |----------------|
    let b = arr.filter(x => (x.Tick < l && x.TickEnd >= l && x.TickEnd < r));
    // NOTE : |----------------|
    // STAGE:    |-----|
    let c = arr.filter(x => (x.Tick < l && x.TickEnd >= r));
    // NOTE :              |-----|
    // STAGE: |----------------|
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

/**
 * 对于Slide来说，不仅需要裁剪，而且需要缩放他的X和Width
 * @param arr
 * @param l
 * @param r
 * @param cut
 * @constructor
 */
function GetAndSplitAndScale<T extends BeatmapSlideUnit>(arr: T[], l: number, r: number, cut: boolean = true) {
    // 这里不裁剪，我们手动你裁剪并改变他的X和Width
    let filtered = GetAndSplit(arr, l, r, false);
    if (!cut) {
        return filtered;
    }

    let scale = function (x: T) {
        let xNew         = x.X, xEndNew = x.XEnd, widthNew = x.Width, widthEndNew = x.WidthEnd;
        let tickNew      = x.Tick, tickEndNew = x.TickEnd;
        let unitStartNew = x.UnitStart, unitEndNew = x.UnitEnd;

        if (x.Tick < l) {
            let ratio    = (l - x.Tick) / (x.TickEnd - x.Tick);
            xNew         = x.X + ratio * (x.XEnd - x.X);
            widthNew     = x.Width + ratio * (x.WidthEnd - x.Width);
            tickNew      = l;
            unitStartNew = x.UnitStart + ratio * (x.UnitEnd - x.UnitStart);
        }
        if (x.TickEnd > r) {
            let ratio   = (r - x.Tick) / (x.TickEnd - x.Tick);
            xEndNew     = x.X + ratio * (x.XEnd - x.X);
            widthEndNew = x.Width + ratio * (x.WidthEnd - x.Width);
            tickEndNew  = r;
            unitEndNew  = x.UnitStart + ratio * (x.UnitEnd - x.UnitStart);
        }
        return {
            ...x,
            X        : xNew,
            XEnd     : xEndNew,
            Width    : widthNew,
            WidthEnd : widthEndNew,
            Tick     : tickNew,
            TickEnd  : tickEndNew,
            UnitStart: unitStartNew,
            UnitEnd  : unitEndNew
        };
    }

    return filtered.map(x => scale(x));
}

function CalcY(tick: number, range: number[]) {
    return `calc(${Math.floor(tick) - range[0]} / ${range[1] - range[0]} * 100%)`;
}
</script>