<script setup lang="ts">
import {computed} from 'vue'
import type {Score, GroupedSong, PlateInfo} from '@/components/maimai/utils/summary_t'
import {achievementOrdinal, fcOrdinal, fsOrdinal, dxScoreStar} from '@/components/maimai/utils/ordinal'

const props = defineProps({
    charts: { type: Array as () => GroupedSong['x'], required: true },
    scores: { type: Object as () => Record<string, Score>, required: true },
    detail: { type: Boolean, default: false },
    // plate 模式时传入；中央 overlay 完成率按 plate.Dim + plate.Level 动态算。
    // sum 模式 (null) 时按 SSS 阈值兜底 (与 PR #38 行为一致)。
    plate:  { type: Object as () => PlateInfo | null, default: null },
})

// rank 顶档加 'app'（fc=='app' 等价 ach 满分 101 — 与下层 fc 顶档对齐，amber-200 同色，
// 但 detail label 上层不写 AP+ 字样，只在下层 fc detail 写 — 朋友 review #2 的诉求）
type RankKey = 'app' | 'sssp' | 'sss' | 'ssp' | 'ss' | 'oth' | 'np'
type FcKey   = 'app' | 'ap' | 'fcp' | 'fc' | 'oth' | 'np'

function getScore(songId: number, levelIdx: number): Score | undefined {
    return props.scores[`(${songId}, ${levelIdx})`]
}

function rankKey(score: Score | undefined): RankKey {
    if (!score) return 'np'
    if (score.fc === 'app') return 'app'   // ap+ 等价满分 → 上条最顶档 segment
    const a = score.achievements
    if (a >= 100.5) return 'sssp'
    if (a >= 100)   return 'sss'
    if (a >= 99.5)  return 'ssp'
    if (a >= 99)    return 'ss'
    return 'oth'
}

function fcKey(score: Score | undefined): FcKey {
    if (!score) return 'np'
    switch (score.fc) {
        case 'app': return 'app'
        case 'ap':  return 'ap'
        case 'fcp': return 'fcp'
        case 'fc':  return 'fc'
        default:    return 'oth'
    }
}

const rankStat = computed(() => {
    const s = {app: 0, sssp: 0, sss: 0, ssp: 0, ss: 0, oth: 0, np: 0, total: 0}
    for (const c of props.charts) {
        s[rankKey(getScore(c.Item3.Id, c.Item2))]++
        s.total++
    }
    return s
})

const fcStat = computed(() => {
    const s = {app: 0, ap: 0, fcp: 0, fc: 0, oth: 0, np: 0, total: 0}
    for (const c of props.charts) {
        s[fcKey(getScore(c.Item3.Id, c.Item2))]++
        s.total++
    }
    return s
})

function pct(n: number, total: number): string {
    return `${total ? n / total * 100 : 0}%`
}

const overallText = computed(() => {
    // 中央完成度按命令的阈值维度动态算：
    // - plate.Dim = Achievement → 按达成率 (achievementOrdinal)
    // - plate.Dim = Fc          → 按 fc 字段 (fcOrdinal)
    // - plate.Dim = Fs          → 按 fs 字段 (fsOrdinal)
    // - plate.Dim = DxScore     → 按星档 (dxScoreStar，需 chart.MaxDx)
    // - sum 模式 (plate = null) → 按 SSS (与 PR #38 兜底一致)
    const dim       = props.plate?.Dim   ?? 'Achievement'
    const threshold = props.plate?.Level ?? 12   // 12 = SSS
    const total = props.charts.length

    let done = 0
    for (const c of props.charts) {
        const sc = props.scores[`(${c.Item3.Id}, ${c.Item2})`]
        if (!sc) continue
        const lv = dim === 'Achievement' ? achievementOrdinal(sc.achievements)
                : dim === 'Fc'           ? fcOrdinal(sc.fc)
                : dim === 'Fs'           ? fsOrdinal(sc.fs)
                :                          dxScoreStar(sc.dxScore, c.Item3.MaxDx)
        if (lv >= threshold) done++
    }
    return `${done}/${total}`
})
</script>

<template>
    <div class="stats-bar" :class="{detail}">
        <div v-if="detail" class="detail-row">
            <!-- 上层 detail 不写 AP+ (字样只在下层 fc detail) — SSS+ 段含 app+sssp 合计 -->
            <pre class="t-all">ALL:{{ rankStat.total }}</pre>
            <pre class="t-sssp">SSS+:{{ rankStat.app + rankStat.sssp }}</pre>
            <pre class="t-sss">SSS:{{ rankStat.sss }}</pre>
            <pre class="t-ssp">SS+:{{ rankStat.ssp }}</pre>
            <pre class="t-ss">SS:{{ rankStat.ss }}</pre>
            <pre class="t-pl">OTH:{{ rankStat.oth }}</pre>
            <pre class="t-np">NP:{{ rankStat.np }}</pre>
        </div>

        <div class="bars">
            <div class="bar">
                <div class="seg app"  :style="{width: pct(rankStat.app,  rankStat.total)}"></div>
                <div class="seg sssp" :style="{width: pct(rankStat.sssp, rankStat.total)}"></div>
                <div class="seg sss"  :style="{width: pct(rankStat.sss,  rankStat.total)}"></div>
                <div class="seg ssp"  :style="{width: pct(rankStat.ssp,  rankStat.total)}"></div>
                <div class="seg ss"   :style="{width: pct(rankStat.ss,   rankStat.total)}"></div>
                <div class="seg pl"   :style="{width: pct(rankStat.oth,  rankStat.total)}"></div>
                <div class="seg np"   :style="{width: pct(rankStat.np,   rankStat.total)}"></div>
            </div>
            <div class="bar">
                <div class="seg app"  :style="{width: pct(fcStat.app,    fcStat.total)}"></div>
                <div class="seg ap"   :style="{width: pct(fcStat.ap,     fcStat.total)}"></div>
                <div class="seg fcp"  :style="{width: pct(fcStat.fcp,    fcStat.total)}"></div>
                <div class="seg fc"   :style="{width: pct(fcStat.fc,     fcStat.total)}"></div>
                <div class="seg pl"   :style="{width: pct(fcStat.oth,    fcStat.total)}"></div>
                <div class="seg np"   :style="{width: pct(fcStat.np,     fcStat.total)}"></div>
            </div>
            <div class="overlay">{{ overallText }}</div>
        </div>

        <div v-if="detail" class="detail-row">
            <pre class="t-all">ALL:{{ fcStat.total }}</pre>
            <pre class="t-app">AP+:{{ fcStat.app }}</pre>
            <pre class="t-ap">AP:{{ fcStat.ap }}</pre>
            <pre class="t-fcp">FC+:{{ fcStat.fcp }}</pre>
            <pre class="t-fc">FC:{{ fcStat.fc }}</pre>
            <pre class="t-pl">OTH:{{ fcStat.oth }}</pre>
            <pre class="t-np">NP:{{ fcStat.np }}</pre>
        </div>
    </div>
</template>

<style scoped lang="postcss">
.stats-bar {
    @apply relative flex flex-col;
}

.bars {
    @apply relative w-full h-12 flex flex-col bg-gray-500 border-4 border-black;
}

.stats-bar.detail .bars {
    @apply h-24;
}

.bar {
    @apply relative w-full h-full flex;
}

.seg {
    @apply h-full;
}

.overlay {
    @apply absolute inset-0 flex items-center place-content-center text-black font-bold text-2xl;
}

.stats-bar.detail .overlay {
    @apply text-4xl;
}

.detail-row {
    @apply flex justify-between text-xl font-bold;
}

/* 全部颜色照抄 chuni OverPower */
.sssp { @apply bg-amber-300; }
.sss  { @apply bg-amber-400; }
.ssp  { @apply bg-sky-300; }
.ss   { @apply bg-blue-400; }
.app  { @apply bg-amber-200; }
.ap   { @apply bg-amber-300; }
.fcp  { @apply bg-green-300; }
.fc   { @apply bg-green-500; }
.pl   { @apply bg-gray-100; }
.np   { @apply bg-gray-500; }

/* text colors */
.t-all  { @apply text-black; }
.t-sssp { @apply text-amber-300; }
.t-sss  { @apply text-amber-400; }
.t-ssp  { @apply text-sky-300; }
.t-ss   { @apply text-blue-400; }
.t-app  { @apply text-amber-200; }
.t-ap   { @apply text-amber-300; }
.t-fcp  { @apply text-green-300; }
.t-fc   { @apply text-green-500; }
.t-pl   { @apply text-gray-300; }
.t-np   { @apply text-gray-500; }
</style>
