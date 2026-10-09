<script setup lang="ts">
import {Score, GroupSongInfo} from "../utils/summary_t";
import {computed} from "vue";
import {calcMaxOverPower, calcOverPower} from "../utils/overpower";
import {getRank, isAllJustice, isFullCombo, isMaxScore} from "../utils/score";


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
    },
    // 每首曲子只取 op 最高的那一张谱面（g/v/ALL），或按谱面分组（lv/b），
    // 同一档里都可能混着不同难度，用一条 2px 难度条显示出来。chuop 系列的页面都开着。
    showDifficulty: {
        type   : Boolean,
        default: false
    }
})

// 上排按成绩数值分档、下排按达成状况分档：同一首歌在两排里经常不在同一档，
// 所以难度条要上下各画一条。
function OpKey(score: Score): OpStatisticKey {
    if (!score) return 'np'
    if (isMaxScore(score)) return 'ajc';
    if (isAllJustice(score)) return 'aj';
    if (isFullCombo(score)) return 'fc';
    return 'pl';
}

function RankKey(score: Score): RkStatisticKey {
    if (!score) return 'np';
    if (isMaxScore(score)) return 'ajc';

    const rank = getRank(score.score);
    switch (rank) {
        case 'sssp':
        case 'sss':
        case 'ssp':
        case 'ss':
            return rank;
        default:
            return 'oth';
    }
}

function GetOverPowerStatistic() {
    let opStat = {'fc': 0, 'aj': 0, 'ajc': 0, 'pl': 0, 'np': 0, 'opMax': 0, 'opSum': 0, 'songCnt': 0} as OpStatistic;

    for (let i = 0; i < props.group.length; i++) {
        let song  = props.group[i]
        let score = props.scores[i]

        let constant = Math.max(...song.Item3.Constants)
        opStat['opSum'] += calcOverPower(score);
        opStat[OpKey(score)] += 1
        opStat['songCnt'] += 1
        opStat['opMax'] += calcMaxOverPower(constant);
    }

    return opStat
}

function GetRankStatistic() {
    let rkStat = {'ajc': 0, 'sssp': 0, 'sss': 0, 'ssp': 0, 'ss': 0, 'oth': 0, 'np': 0, 'songCnt': 0} as RkStatistic;

    for (let i = 0; i < props.group.length; i++) {
        rkStat[RankKey(props.scores[i])] += 1
        rkStat['songCnt'] += 1
    }

    return rkStat
}

let op = computed(() => GetOverPowerStatistic());
let rk = computed(() => GetRankStatistic());

// ---------- 难度条 ----------

// 难度色沿用 ChunithmSong.vue 的 level_idx_color_map，只有 ULTIMA 不一样：
// 那边用的纯黑贴在最下面那条时会和外框糊在一起，这里用官方配色的 #97343A。
const DIFFICULTY_COLORS: { [levelIndex: number]: string } = {
    0: '#52E72B',   // BASIC
    1: '#FFA801',   // ADVANCED
    2: '#FF5A66',   // EXPERT
    3: '#C64FE4',   // MASTER
    4: '#97343A',   // ULTIMA（官方配色）
    5: '#DBAAFF',   // WORLD'S END
}

// 固定顺序，保证同一个条里色块排列一致。
// 难度从高到低排：ULTIMA 在最左，BASIC 在最右。
const DIFFICULTY_ORDER = [5, 4, 3, 2, 1, 0]

type Segment = {
    key: string
    cls: string
    width: string
    diffs: { level: number, width: string, color: string }[]
}

// 每个分类下各难度各有几首
function GetDifficultyCounts(keyFn: (s: Score) => string): { [category: string]: { [level: number]: number } } {
    const counts: { [category: string]: { [level: number]: number } } = {}

    for (const score of props.scores) {
        if (!score) continue

        const level = score.level_index ?? 0
        const key   = keyFn(score)

        if (!counts[key]) counts[key] = {}
        counts[key][level] = (counts[key][level] ?? 0) + 1
    }

    return counts
}

function MakeSegment(
    stat: { [key: string]: number },
    counts: { [category: string]: { [level: number]: number } },
    statKey: string,
    cls: string
): Segment {
    const byLevel = counts[statKey] ?? {}

    let inCategory = 0
    for (const level of DIFFICULTY_ORDER) inCategory += byLevel[level] ?? 0

    // 段宽是该分类占总数的比例，条内再按分类内部的难度占比切分，
    // 于是条的总宽与段宽一致，色块宽度正比于该难度的歌曲数。
    const diffs = DIFFICULTY_ORDER
        .filter(level => (byLevel[level] ?? 0) > 0)
        .map(level => ({
            level: level,
            width: `${(byLevel[level] ?? 0) / inCategory * 100}%`,
            color: DIFFICULTY_COLORS[level] ?? '#DBAAFF',
        }))

    const width = stat['songCnt'] === 0 ? '0%' : `${stat[statKey] / stat['songCnt'] * 100}%`

    return {key: statKey, cls: cls, width: width, diffs: diffs}
}

const RankSegments = computed(() => {
    const counts = GetDifficultyCounts(s => RankKey(s))
    return [
        MakeSegment(rk.value, counts, 'ajc',  'ajc'),
        MakeSegment(rk.value, counts, 'sssp', 'sssp'),
        MakeSegment(rk.value, counts, 'sss',  'sss'),
        MakeSegment(rk.value, counts, 'ssp',  'ssp'),
        MakeSegment(rk.value, counts, 'ss',   'ss'),
        MakeSegment(rk.value, counts, 'oth',  'pl'),
    ]
})

const OpSegments = computed(() => {
    const counts = GetDifficultyCounts(s => OpKey(s))
    return [
        MakeSegment(op.value, counts, 'ajc', 'ajc'),
        MakeSegment(op.value, counts, 'aj',  'aj'),
        MakeSegment(op.value, counts, 'fc',  'fc'),
        MakeSegment(op.value, counts, 'pl',  'pl'),
    ]
})

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
                        <div v-for="s in RankSegments" :key="s.key" class="seg" :style="`width: ${s.width}`">
                            <div v-if="showDifficulty" class="seg-diff">
                                <div v-for="d in s.diffs" :key="d.level"
                                     :style="`width: ${d.width}; background-color: ${d.color}`"></div>
                            </div>
                            <div class="seg-body" :class="s.cls"></div>
                        </div>
                    </div>
                    <div class="relative w-full h-full flex">
                        <div v-for="s in OpSegments" :key="s.key" class="seg" :style="`width: ${s.width}`">
                            <div class="seg-body" :class="s.cls"></div>
                            <div v-if="showDifficulty" class="seg-diff">
                                <div v-for="d in s.diffs" :key="d.level"
                                     :style="`width: ${d.width}; background-color: ${d.color}`"></div>
                            </div>
                        </div>
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

/* 每个分类一段：上面是分类色块，下面 2px 是该分类的难度构成 */
.seg {
    height: 100%;
    min-width: 0;
    display: flex;
    flex-direction: column;
}

.seg-body {
    flex: 1 1 auto;
    min-height: 0;
}

.seg-diff {
    /* 与容器的 border-4 同宽 */
    flex: 0 0 4px;
    height: 4px;
    min-width: 0;
    display: flex;
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
    @apply bg-sky-300;
}

.ss {
    @apply bg-blue-400;
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
    @apply text-sky-300;
}

.t-ss {
    @apply text-blue-400;
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