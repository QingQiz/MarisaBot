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
    },
    // opMax 那一列按什么定数算：
    // 'song'  —— 取这一行所属乐曲的最高定数。op 系列页面里一行代表这首歌的 op 对象，
    //            OP 上限本来就是由该曲最难的谱面定的，所以保持这个口径。
    // 'chart' —— 取这一行谱面自己的定数。汇总页（chusum）一行就是一张谱面，
    //            和下面对 opSum 用的 score.ds 口径一致。
    constantSource: {
        type   : String as () => 'song' | 'chart',
        default: 'song'
    },
    // 渲染形态：
    // 'panel' —— 完整统计条（标题两行数字 + 明细 + 双排分段 + 中间百分比），老版页面在用
    // 'bars'  —— 两排带档位标签的分段条，汇总页顶部与 chuop 各页用
    // 'mini'  —— 两排 10px 细条，汇总页分组标题右侧用
    layout: {
        type   : String as () => 'panel' | 'bars' | 'mini',
        default: 'panel'
    },
    // 'bars' 形态下是否在左侧显示 opSum/opMax（写成上下两行、中间一道横线的分式）
    numbers: {
        type   : Boolean,
        default: false
    },
    // 'bars' 形态下是否在分式右边、条形左边显示完成百分比
    percent: {
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

        // 统计对象随页面而定：op 页一行代表整首歌（上限按全曲最高定数），
        // 汇总页一行就是一张谱面（上限按这张谱面的定数）。
        let constant = props.constantSource === 'chart' ? song.Item1 : Math.max(...song.Item3.Constants)
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

// ---------- 汇总页用的简化分段条（与舞萌汇总同款：两排、右上角计数） ----------

const RANK_LABELS: Record<string, string> = {ajc: 'AJC', sssp: 'SSS+', sss: 'SSS', ssp: 'SS+', ss: 'SS', oth: 'OTH', np: 'NP'}
const OP_LABELS: Record<string, string>   = {ajc: 'AJC', aj: 'AJ', fc: 'FC', pl: 'OTH', np: 'NP'}

// 与 panel 里的 tailwind 配色一致：ajc=amber-200、aj/sssp=amber-300、sss=amber-400、
// ssp=sky-300、ss=blue-400、fc=green-500、pl=gray-100、np=gray-500。
const RANK_BAR_COLORS: Record<string, string> = {
    ajc: '#fde68a', sssp: '#fcd34d', sss: '#fbbf24', ssp: '#7dd3fc', ss: '#60a5fa', oth: '#f3f4f6', np: '#6b7280',
}
const OP_BAR_COLORS: Record<string, string> = {
    ajc: '#fde68a', aj: '#fcd34d', fc: '#22c55e', pl: '#f3f4f6', np: '#6b7280',
}

function MakeDiffs(byLevel: Record<number, number>) {
    let sum = 0
    for (const level of DIFFICULTY_ORDER) sum += byLevel[level] ?? 0
    if (sum === 0) return []

    // 段内再按难度占比切分，色块宽度正比于该难度的谱面数（与 panel 的难度条同一套算法）
    return DIFFICULTY_ORDER
        .filter(level => (byLevel[level] ?? 0) > 0)
        .map(level => ({
            level: level,
            width: `${(byLevel[level] ?? 0) / sum * 100}%`,
            color: DIFFICULTY_COLORS[level] ?? '#DBAAFF',
        }))
}

function MakeBarRow(
    stat: Record<string, number>,
    keys: string[],
    labels: Record<string, string>,
    colors: Record<string, string>,
    counts: { [category: string]: { [level: number]: number } },
) {
    return {
        total: stat['songCnt'] ?? 0,
        segs : keys.map(key => ({
            key  : key,
            label: labels[key],
            color: colors[key],
            n    : stat[key] ?? 0,
            diffs: MakeDiffs(counts[key] ?? {}),
        })),
    }
}

const BarRows = computed(() => {
    const rankCounts = GetDifficultyCounts(s => RankKey(s))
    const opCounts   = GetDifficultyCounts(s => OpKey(s))
    return [
        MakeBarRow(rk.value, ['ajc', 'sssp', 'sss', 'ssp', 'ss', 'oth', 'np'], RANK_LABELS, RANK_BAR_COLORS, rankCounts),
        MakeBarRow(op.value, ['ajc', 'aj', 'fc', 'pl', 'np'], OP_LABELS, OP_BAR_COLORS, opCounts),
    ]
})

// bars 形态的明细行：沿用老 panel 的两行计数（ALL 起头），只是配色按深色卡片调亮
const RankDetail = computed(() => [
    {key: 'all', label: 'ALL', n: rk.value['songCnt'], color: '#ffffff'},
    ...(['sssp', 'sss', 'ssp', 'ss', 'oth', 'np'] as RkStatisticKey[]).map(key => ({
        key, label: RANK_LABELS[key], n: rk.value[key] ?? 0, color: RANK_BAR_COLORS[key],
    })),
])

const OpDetail = computed(() => [
    {key: 'all', label: 'ALL', n: op.value['songCnt'], color: '#ffffff'},
    ...(['ajc', 'aj', 'fc', 'pl', 'np'] as OpStatisticKey[]).map(key => ({
        key, label: OP_LABELS[key], n: op.value[key] ?? 0, color: OP_BAR_COLORS[key],
    })),
])

const OpPercent = computed(() => op.value['opMax'] === 0 ? 0 : op.value['opSum'] / op.value['opMax'] * 100)

</script>

<template>
    <!-- mini 用于分组标题右侧：根节点不能撑满整行，否则 280px 的细条会被顶到左边、右边缘对不齐 -->
    <div :class="layout === 'mini' ? 'shrink-0' : 'w-full'">
        <div v-if="layout === 'panel'" class="flex gap-2 w-full text-black">
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

        <!-- 汇总页 / chuop 页：两排分段条（'bars' 带档位标签与难度条，'mini' 只有细色条）。
             根节点带 layout 类名，mini 形态的数字与百分比要按更小的尺寸排版 -->
        <div v-else class="bar-wrap" :class="layout">
            <!-- 左侧：opSum / opMax 写成分式的样子（分子在上、分母在下，中间一道横线）。
                 列宽固定（分式贴右缘），各行的百分比与条形才能对齐 -->
            <div v-if="numbers" class="bar-nums">
                <span class="bar-nums-frac">
                    <b>{{ op['opSum'].toFixed(2) }}</b>
                    <i></i>
                    <span>{{ op['opMax'].toFixed(2) }}</span>
                </span>
            </div>

            <!-- 完成百分比：跟在分式右边、条形左边，列宽固定 -->
            <div v-if="percent" class="bar-percent">
                {{ OpPercent.toFixed(2) }}<small>%</small>
            </div>

            <div class="bar-main">
                <!-- 老 chuop 面板的内容照旧：上排成绩档计数 → 条形 → 下排达成档计数 -->
                <div v-if="detail && layout === 'bars'" class="bar-detail">
                    <span v-for="c in RankDetail" :key="'r' + c.key" :style="{color: c.color}">
                        {{ c.label }}:<b>{{ c.n }}</b>
                    </span>
                </div>

                <div class="bar-stack" :class="layout">
                    <div v-for="(row, ri) in BarRows" :key="ri" class="bar-row">
                        <div v-for="s in row.segs.filter(x => x.n > 0)" :key="s.key" class="bar-seg"
                             :class="{np: s.key === 'np'}"
                             :style="{flexGrow: s.n, background: s.color}">
                            <span v-if="layout === 'bars' && row.total > 0 && s.n / row.total > 0.06">
                                {{ s.label }} <b>{{ s.n }}</b>
                            </span>
                            <span v-if="showDifficulty && layout === 'bars' && s.diffs.length" class="bar-seg-diff">
                                <i v-for="d in s.diffs" :key="d.level"
                                   :style="{width: d.width, background: d.color}"></i>
                            </span>
                        </div>
                    </div>
                </div>

                <div v-if="detail && layout === 'bars'" class="bar-detail">
                    <span v-for="c in OpDetail" :key="'o' + c.key" :style="{color: c.color}">
                        {{ c.label }}:<b>{{ c.n }}</b>
                    </span>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped lang="postcss">

/* chuop 页：分式 + 百分比 + 分段条 + 明细 chips，横向排布 */
.bar-wrap {
    display: flex;
    align-items: center;
    gap: 10px;
    width: 100%;
}

.bar-nums {
    /* 宽度固定（不是 auto）：各行的位数不同，auto 会让下面条形的左边缘参差不齐。
       分式在列内左右居中，于是各行分式的中心都落在同一竖线上 */
    flex: 0 0 134px;
    display: flex;
    justify-content: center;
    font-family: 'Torus', sans-serif;
    font-variant-numeric: tabular-nums;
    line-height: 1;
    /* 文字的行框里字形偏下（line-height: 1 时下半截是空的），只靠 align-items: center
       会把数字整体压低 3px 左右；补一个 6px 的下外边距，居中时正好往上顶 3px，
       墨迹中心就和两侧条形对上了（分式与百分比用同一个值，两列也就彼此对齐） */
    margin-bottom: 6px;
}

/* 分式本体：分子 / 横线 / 分母，三者等宽并居中，横线宽度取分子宽度 */
.bar-nums-frac {
    display: flex;
    flex-direction: column;
    align-items: stretch;
    text-align: center;
}

.bar-nums-frac b {
    font-size: 30px;
    font-weight: 800;
    color: #fff;
}

.bar-nums-frac i {
    height: 2px;
    margin: 5px 0 4px;
    border-radius: 1px;
    background: rgba(255, 255, 255, .8);
}

.bar-nums-frac span {
    font-size: 19px;
    font-weight: 700;
    color: rgba(255, 255, 255, .6);
}

.bar-main {
    flex: 1 1 auto;
    min-width: 0;
}

/* 段内难度条：贴在色块底部，4px 与 panel 一致 */
.bar-seg {
    position: relative;
}

.bar-seg-diff {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    height: 4px;
    display: flex;
}

/* 明细行：沿用老 panel 的计数顺序（ALL/SSS+/SSS/SS+/SS/OTH/NP 与 ALL/AJC/AJ/FC/OTH/NP），
   只是配色按深色卡片调亮 */
.bar-detail {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: 2px 18px;
    font: 700 20px 'Torus', 'Consolas', monospace;
    letter-spacing: .02em;
    font-variant-numeric: tabular-nums;
}

.bar-detail b {
    font-weight: 800;
    color: #fff;
}

/* 完成百分比：夹在分式与条形之间，列宽固定、数字在列内左右居中，各行中心对齐。
   列宽按常见的「xx.xx%」定，遇到 100.00% 这种多一位时向两侧溢出，不会挤到条形 */
.bar-percent {
    flex: 0 0 126px;
    text-align: center;
    white-space: nowrap;
    font: 800 40px 'Torus', sans-serif;
    font-variant-numeric: tabular-nums;
    line-height: 1;
    color: #fff;
    /* 与 .bar-nums 同一套对齐补偿，见那里的注释 */
    margin-bottom: 6px;
}

.bar-percent small {
    margin-left: 2px;
    font-size: 24px;
    font-weight: 700;
    color: rgba(255, 255, 255, .6);
}

/* 分组标题右侧的小号形态：分式与百分比按比例缩小、列宽收窄，条形仍固定 280px 贴右。
   字号变小后行框的墨迹偏移也变小，对齐补偿量跟着减半 */
.bar-wrap.mini .bar-nums {
    flex: 0 0 92px;
    margin-bottom: 3px;
}

.bar-wrap.mini .bar-nums-frac b {
    font-size: 19px;
}

.bar-wrap.mini .bar-nums-frac i {
    height: 1.5px;
    margin: 3px 0 2px;
}

.bar-wrap.mini .bar-nums-frac span {
    font-size: 14px;
}

.bar-wrap.mini .bar-percent {
    flex: 0 0 76px;
    font-size: 22px;
    margin-bottom: 3px;
}

.bar-wrap.mini .bar-percent small {
    font-size: 14px;
}

.bar-seg-diff i {
    display: block;
    height: 100%;
}

/* 汇总页的简化分段条：'bars' 两排 30px，'mini' 两排 10px */
.bar-stack {
    display: flex;
    flex-direction: column;
    width: 100%;
}

.bar-stack.bars {
    gap: 6px;
}

.bar-stack.mini {
    gap: 2px;
    width: 280px;
    flex-shrink: 0;
}

.bar-row {
    display: flex;
    gap: 2px;
    overflow: hidden;
    border-radius: 10px;
    background: rgba(0, 0, 0, .25);
}

.bars .bar-row {
    height: 30px;
}

.mini .bar-row {
    height: 10px;
    border-radius: 3px;
}

.bar-seg {
    display: flex;
    align-items: center;
    justify-content: center;
    flex-basis: 0;
    min-width: 0;
    overflow: hidden;
    white-space: nowrap;
    font: 700 13px 'Torus', sans-serif;
    letter-spacing: .04em;
    color: rgba(20, 12, 30, .75);
}

.bar-seg b {
    margin-left: 2px;
    font-weight: 800;
    color: #14081f;
}

.bar-seg.np {
    color: rgba(255, 255, 255, .7);
}

.bar-seg.np b {
    color: #fff;
}

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