<script setup lang="ts">
import {Score, GroupSongInfo} from "../utils/summary_t";
import {computed} from "vue";


type OpStatisticKey = 'pl' | 'fc' | 'aj' | 'ajc' | 'np' | 'opMax' | 'opSum' | 'songCnt'
type OpStatistic = {
    [key in OpStatisticKey]: number
}
type RkStatisticKey = 'ajc' | 'sssp' | 'sss' | 'ssp' | 'ss' | 'oth' | 'np' | 'songCnt'
type RkStatistic = {
    [key in RkStatisticKey]: number
}

let props = defineProps({
    group : {
        type    : Array as () => GroupSongInfo[],
        required: true
    },
    scores: {
        type    : Array as () => Score[],
        required: true
    },
    detail: {
        type   : Boolean,
        default: false
    }
})

/**
 * @param score
 * @return op * 10000
 */
function OverPower(score: Score) {
    if (!score || score.score == 0) return 0;

    let s = score.score <= 100_7500 ? score.ra * 10000 : parseInt((score.ds * 10000).toString()) + 20000;
    let r = score.fc == 'fullcombo' || score.fc == 'fullchain' || score.fc == 'fullchain2' ? 5000 : 0;

    if (score.fc == 'alljustice') r = 10000;
    if (score.score == 101_0000) r = 12500;

    let e = score.score <= 100_7500 ? 0 : (score.score - 100_7500) * 15;

    return s * 5 + r + e;
}

function ShouldSkip(song: GroupSongInfo) {
    if (song.Item2 != 3 && song.Item2 != 4) return true;
    let constant = song.Item3.Constants[song.Item2]
    // 好像有一些垃圾数据
    return constant < 10;
}

function GetOverPowerStatistic() {
    function GetKey(score: Score): OpStatisticKey {
        if (!score) return 'np'
        if (score.score == 101_0000) return 'ajc';
        if (score.fc === 'alljustice') return 'aj';
        if (score.fc.startsWith('full')) return 'fc';
        return 'pl';
    }

    let opStat = {'fc': 0, 'aj': 0, 'ajc': 0, 'pl': 0, 'np': 0, 'opMax': 0, 'opSum': 0, 'songCnt': 0} as OpStatistic;

    for (let i = 0; i < props.group.length; i++) {
        let song  = props.group[i]
        let score = props.scores[i]
        if (ShouldSkip(song)) continue;

        let constant = song.Item3.Constants[song.Item2]
        opStat['opSum'] += OverPower(score);
        opStat[GetKey(score)] += 1
        opStat['songCnt'] += 1
        opStat['opMax'] += ((constant) * 5 + 15) * 10000;
    }

    opStat['opMax'] /= 10000.;
    opStat['opSum'] /= 10000.;

    return opStat
}

function GetRankStatistic() {
    function GetKey(score: Score): RkStatisticKey {
        if (!score) return 'np';
        if (score.score == 101_0000) return 'ajc';
        if (score.score >= 100_9000) return 'sssp';
        if (score.score >= 100_7500) return 'sss';
        if (score.score >= 100_5000) return 'ssp';
        if (score.score >= 100_0000) return 'ss';
        return 'oth';
    }

    let rkStat = {'ajc': 0, 'sssp': 0, 'sss': 0, 'ssp': 0, 'ss': 0, 'oth': 0, 'np': 0, 'songCnt': 0} as RkStatistic;

    for (let i = 0; i < props.group.length; i++) {
        if (ShouldSkip(props.group[i])) continue;

        rkStat[GetKey(props.scores[i])] += 1
        rkStat['songCnt'] += 1
    }

    return rkStat
}

let op = computed(() => GetOverPowerStatistic());
let rk = computed(() => GetRankStatistic());

function GetWidth<T extends OpStatistic | RkStatistic>(
    statistic: T,
    key: T extends OpStatistic ? OpStatisticKey : RkStatisticKey
) {
    let val = statistic[key as keyof T] as unknown as number;
    return `${val / statistic['songCnt'] * 100}%`
}

</script>

<template>
    <div class="w-full">
        <div class="flex gap-2 w-full text-black">
            <div class="bar-title">
                <pre>{{ op['opSum'].toFixed(2).padStart(8, ' ') }}</pre>
                <pre>{{ op['opMax'].toFixed(2).padStart(8, ' ') }}</pre>
            </div>

            <div class="w-full">
                <div v-if="detail" class="detail">
                    <pre class="t-all">ALL:{{ rk['songCnt'].toString() }}</pre>
                    <pre class="t-sssp">SSS+:{{ rk['sssp'].toString() }}</pre>
                    <pre class="t-sss">SSS:{{ rk['sss'].toString() }}</pre>
                    <pre class="t-ssp">SS+:{{ rk['ssp'].toString() }}</pre>
                    <pre class="t-ss">SS:{{ rk['ss'].toString() }}</pre>
                    <pre class="t-pl">OTH:{{ rk['oth'].toString() }}</pre>
                    <pre class="t-np">NP:{{ rk['np'].toString() }}</pre>
                </div>
                <div class="relative w-full h-[100px] flex flex-col bg-gray-500 border-4 border-black">
                    <div class="relative w-full h-full flex">
                        <div class="h-full ajc" :style="`width: ${GetWidth(rk, 'ajc')}`"></div>
                        <div class="h-full sssp" :style="`width: ${GetWidth(rk, 'sssp')}`"></div>
                        <div class="h-full sss" :style="`width: ${GetWidth(rk, 'sss')}`"></div>
                        <div class="h-full ssp" :style="`width: ${GetWidth(rk, 'ssp')}`"></div>
                        <div class="h-full ss" :style="`width: ${GetWidth(rk, 'ss')}`"></div>
                        <div class="h-full pl" :style="`width: ${GetWidth(rk, 'oth')}`"></div>
                    </div>
                    <div class="relative w-full h-full flex">
                        <div class="h-full ajc" :style="`width: ${GetWidth(op, 'ajc')}`"></div>
                        <div class="h-full aj" :style="`width: ${GetWidth(op, 'aj')}`"></div>
                        <div class="h-full fc" :style="`width: ${GetWidth(op, 'fc')}`"></div>
                        <div class="h-full pl" :style="`width: ${GetWidth(op, 'pl')}`"></div>
                    </div>
                    <div class="absolute text-5xl inset-0 flex items-center place-content-center">
                        {{ (op['opSum'] / op['opMax'] * 100).toFixed(2) }}%
                    </div>
                </div>
                <div v-if="detail" class="detail">
                    <pre class="t-all">ALL:{{ op['songCnt'].toString() }}</pre>
                    <pre class="t-ajc">AJC:{{ op['ajc'].toString() }}</pre>
                    <pre class="t-aj">AJ:{{ op['aj'].toString() }}</pre>
                    <pre class="t-fc">FC:{{ op['fc'].toString() }}</pre>
                    <pre class="t-pl">OTH:{{ op['pl'].toString() }}</pre>
                    <pre class="t-np">NP:{{ op['np'].toString() }}</pre>
                </div>
            </div>

        </div>
    </div>
</template>

<style scoped lang="postcss">

.bar-title {
    font-size: 35px;

    @apply font-console flex flex-col text-right justify-center;
}

.detail {
    @apply flex justify-between;

    font-size: 30px;
}

.ajc {
    @apply bg-amber-200;
}

.aj, .sssp {
    @apply bg-amber-300
}

.sss {
    @apply bg-amber-400;
}

.ssp {
    @apply bg-amber-500;
}

.ss {
    @apply bg-amber-600;
}

.fc {
    @apply bg-green-500;
}

.pl {
    @apply bg-gray-100;
}

.t-all {
    @apply text-black;
}

.t-ajc {
    @apply text-amber-200;
}

.t-aj, .t-sssp {
    @apply text-amber-300;
}

.t-sss {
    @apply text-amber-400;
}

.t-ssp {
    @apply text-amber-500;
}

.t-ss {
    @apply text-amber-600;
}

.t-fc {
    @apply text-green-500;
}

.t-pl {
    @apply text-gray-300;
}

.t-np {
    @apply text-gray-500;
}
</style>