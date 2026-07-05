<template>
    <div v-if="song" class="mai-curve mai-card relative w-[840px] overflow-hidden antialiased" :style="rootStyle">
        <div class="absolute inset-0 pointer-events-none stripe-layer"></div>
        <div class="absolute inset-0 pointer-events-none" :style="glowStyle"></div>

        <div class="relative px-12 pt-9 pb-8">
            <!-- ── top meta bar ── -->
            <header class="flex items-center gap-2 flex-nowrap whitespace-nowrap">
                <img :src="versionLogo" :style="logoStyle" class="h-[50px] shrink-0 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
                <div class="flex-1"></div>
                <img :src="typeBadge" class="h-9 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
                <div class="bpm-pill">
                    <span class="bpm-label">BPM</span>
                    <span class="bpm-num tabular-nums">{{ song.bpm }}</span>
                </div>
                <span class="meta-chip">{{ genreDisplay }}</span>
                <div class="id-pill tabular-nums">ID {{ songId }}</div>
            </header>

            <!-- ── cover + title ── -->
            <div class="flex items-end gap-5 mt-7">
                <div class="cover-frame shrink-0">
                    <img :src="coverSrc" @error="onCoverErr" alt="" class="block w-[112px] h-[112px] object-cover rounded-[14px]">
                </div>
                <div class="flex-1 min-w-0 pb-1">
                    <h1 ref="titleEl" class="mai-title" :style="{ fontSize: titleSize + 'px' }">{{ song.title }}</h1>
                    <div class="artist-line">{{ song.artist }}</div>
                </div>
            </div>

            <!-- ── section tag + difficulty chip ── -->
            <div class="flex items-center gap-4 mt-7 mb-4">
                <span class="section-tag" :style="{ background: diffColor }">体感难度</span>
                <span class="font-rodin text-[18px] tracking-[0.28em] text-white/70 whitespace-nowrap">DIFFICULTY CURVE</span>
                <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
                <span class="diff-chip font-rodin" :style="{ color: diffColor, borderColor: diffColor }">
                    {{ diffName }} {{ chart.ds.toFixed(1) }}
                </span>
            </div>

            <!-- ── stats column + curve ── -->
            <div class="flex gap-5 items-stretch">
                <div class="w-[218px] shrink-0 flex flex-col gap-3">
                    <div v-if="isDsKind" class="stat">
                        <div class="stat-k">综合体感定数</div>
                        <div class="stat-v" :style="{ color: accent }">{{ fittedPooled }}</div>
                    </div>
                    <div v-if="chart.score_rank" class="stat">
                        <div class="stat-k">同定数难度</div>
                        <div class="stat-v tabular-nums">#{{ chart.score_rank.rank }} <span class="rank-of">/ {{ chart.score_rank.of }}</span></div>
                    </div>
                    <div v-if="chart.badge" class="stat">
                        <div class="stat-k">{{ chart.badge.label }}</div>
                        <div class="stat-v badge-v tabular-nums" v-html="badgeValue"></div>
                        <div class="stat-sub">{{ badgeSub }}</div>
                    </div>
                </div>
                <div class="flex-1 min-w-0">
                    <svg :width="SVG_W" :height="svgH" :viewBox="`0 0 ${SVG_W} ${svgH}`">
                        <polygon v-if="isDsKind" :points="bandPoints" :fill="accent" opacity="0.13"/>
                        <g v-for="t in yTicks" :key="'y' + t.v">
                            <line :x1="ML" :y1="t.y" :x2="SVG_W - MR" :y2="t.y" stroke="#fff" stroke-opacity="0.08"/>
                            <text :x="ML - 10" :y="t.y + 4" text-anchor="end" class="tick">{{ t.label }}</text>
                        </g>
                        <g v-for="t in xTicks" :key="'x' + t.v">
                            <line :x1="t.x" :y1="MT" :x2="t.x" :y2="svgH - MB" stroke="#fff" stroke-opacity="0.05"/>
                            <text :x="t.x" :y="svgH - MB + 22" text-anchor="middle" class="tick">{{ t.v }}</text>
                        </g>
                        <line :x1="ML" :y1="refY" :x2="SVG_W - MR" :y2="refY"
                              stroke="#fff" :stroke-opacity="isDsKind ? 0.45 : 0.35"
                              stroke-dasharray="7 6" stroke-width="1.5"/>
                        <text :x="ML + 8" :y="refY - 7" class="ref">{{ refLabel }}</text>
                        <path :d="curvePath" fill="none" :stroke="accent" stroke-width="3"
                              stroke-linejoin="round" stroke-linecap="round"/>
                        <circle :cx="endPt.x" :cy="endPt.y" r="4.5" :fill="accent"/>
                        <circle :cx="endPt.x" :cy="endPt.y" r="8" :fill="accent" opacity="0.25"/>
                        <text :x="ML" :y="MT - 4" class="axis">{{ yAxisLabel }}</text>
                        <text :x="SVG_W - MR" :y="svgH - 6" text-anchor="end" class="axis">玩家 DX RATING</text>
                    </svg>
                </div>
            </div>

            <footer class="mt-5 flex items-baseline justify-between gap-4">
                <span class="foot-note">数据来源：水鱼查分器（diving-fish.com）成绩聚合统计（n={{ chart.n.toLocaleString() }}）· 曲线为各段位玩家的体感难度估计</span>
                <span class="footer-text shrink-0">MARISA BOT · DIFFICULTY CURVE</span>
            </footer>
        </div>
    </div>

    <div v-else-if="noData" class="mai-curve mai-card w-[840px] px-12 py-10 antialiased">
        <div class="text-[26px] font-bold">暂无体感难度曲线数据</div>
        <div class="mt-3 text-[16px] text-white/60">数据覆盖 Lv11 及以上的常规谱面，宴会场谱面与收录后新增的曲目暂不支持。</div>
    </div>
</template>

<script setup lang="ts">
import {computed, nextTick, ref, watch} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {
    DIFF_NAMES, DIFF_COLORS, genreDisplayOf,
    VERSION_CODE, LOGO_BBOX_LEFT, versionLogoSrc, typeBadgeSrc,
    coverSrcOf, COVER_FALLBACK, bgKeyOf, cardBackground,
} from '@/components/maimai/utils/song_card'

interface Badge {
    label: string; rank: number; of: number; scope: string; basis: string
    ap_rate_pct: number; n: number; population: string
}
interface ScoreRank { rank: number; of: number; scope: string }
interface CurveChart {
    li: number; ds: number; kind: string; curve: number[][]
    badge: Badge | null; score_rank: ScoreRank | null; pooled: number | null; n: number
}
interface CurveSong {
    title: string; type: string; artist: string; bpm: number; genre: string; ver: string
    charts: CurveChart[]
}

const route  = useRoute()
const songId = Number(route.query.id)
const song   = ref<CurveSong | null>(null)
const noData = ref(false)

axios.get('/assets/maimai/difficulty_curves.json').then(res => {
    const s = res.data[String(songId)] as CurveSong | undefined
    if (s && s.charts.length) song.value = s
    else noData.value = true
})

// 展示定数最高的谱面；idx 查询参数可指定难度
const chart = computed<CurveChart>(() => {
    const cs = song.value!.charts
    const byIdx = cs.find(c => c.li === Number(route.query.idx))
    return byIdx ?? cs.reduce((a, b) => b.ds > a.ds ? b : a)
})

const isDsKind = computed(() => chart.value.kind === 'fitted_ds')
const diffName  = computed(() => DIFF_NAMES[Math.min(chart.value.li, 4)])
const diffColor = computed(() => DIFF_COLORS[Math.min(chart.value.li, 4)])
// Re:MASTER 淡紫在深底上可读性优于 MASTER 紫，两档紫谱曲线统一用淡紫
const accent = computed(() => chart.value.li >= 3 ? DIFF_COLORS[4] : diffColor.value)

const fittedPooled = computed(() =>
    (chart.value.ds + (chart.value.pooled ?? 0)).toFixed(2))

const badgeValue = computed(() => {
    const b = chart.value.badge!
    return `#${b.rank} <span class="badge-of">/ ${b.of}</span>`
})
const badgeSub = computed(() => {
    const b = chart.value.badge!
    return `同定数 · AP 达成率 ${b.ap_rate_pct.toFixed(1)}%`
})

// ── 曲线绘制 ──
const SVG_W = 545, ML = 62, MR = 16, MT = 18, MB = 40
const svgH = 302
const X0 = 10200, X1 = 16800

const yDomain = computed<[number, number]>(() => {
    if (!isDsKind.value) return [-6, 106]
    const ys = chart.value.curve.map(p => p[1])
    return [Math.min(...ys, chart.value.ds) - 0.18, Math.max(...ys, chart.value.ds) + 0.18]
})
function X(v: number) { return ML + (v - X0) / (X1 - X0) * (SVG_W - ML - MR) }
function Y(v: number) {
    const [lo, hi] = yDomain.value
    return MT + (1 - (v - lo) / (hi - lo)) * (svgH - MT - MB)
}

const curvePath = computed(() =>
    'M' + chart.value.curve.map(p => `${X(p[0]).toFixed(1)},${Y(p[1]).toFixed(1)}`).join(' L'))
const bandPoints = computed(() => {
    const c = chart.value.curve
    const up = c.map(p => `${X(p[0]).toFixed(1)},${Y(p[1] + 2 * (p[2] ?? 0)).toFixed(1)}`)
    const dn = [...c].reverse().map(p => `${X(p[0]).toFixed(1)},${Y(p[1] - 2 * (p[2] ?? 0)).toFixed(1)}`)
    return [...up, ...dn].join(' ')
})
const endPt = computed(() => {
    const p = chart.value.curve[chart.value.curve.length - 1]
    return {x: X(p[0]), y: Y(p[1])}
})

const yTicks = computed(() => {
    if (!isDsKind.value) return [0, 25, 50, 75, 100].map(v => ({v, y: Y(v), label: String(v)}))
    const [lo, hi] = yDomain.value
    const out = []
    for (let v = Math.ceil(lo * 2) / 2; v < hi; v += 0.5) out.push({v, y: Y(v), label: v.toFixed(1)})
    return out
})
const xTicks = computed(() => {
    const out = []
    for (let v = 11000; v <= 16000; v += 1000) out.push({v, x: X(v)})
    return out
})
const refY = computed(() => isDsKind.value ? Y(chart.value.ds) : Y(50))
const refLabel = computed(() => isDsKind.value ? `官方定数 ${chart.value.ds.toFixed(1)}` : '带内中位')
const yAxisLabel = computed(() => isDsKind.value ? '体感定数' : '带内百分位')

// ── 头部素材（同 MaiSongScore） ──
const typeBadge = computed(() => typeBadgeSrc(song.value?.type))
const coverSrc = ref('')
watch(song, s => { if (s) coverSrc.value = coverSrcOf(songId) }, {immediate: true})
function onCoverErr() { coverSrc.value = COVER_FALLBACK }
const genreDisplay = computed(() => genreDisplayOf(song.value?.genre))
const versionLogo = computed(() => versionLogoSrc(song.value?.ver))
const logoStyle = computed(() => {
    const code = VERSION_CODE[song.value?.ver ?? '']
    const trim = code ? (LOGO_BBOX_LEFT[code] ?? 0) * (60 / 160) : 0
    return {marginLeft: `${(-trim).toFixed(1)}px`}
})

const rootStyle = computed(() => cardBackground(bgKeyOf(chart.value?.li ?? 3, false)))
const glowStyle = computed(() => ({
    background: `radial-gradient(720px 520px at 18% 8%, ${accent.value}2e 0%, transparent 70%),
                 radial-gradient(560px 480px at 92% 4%, ${accent.value}1c 0%, transparent 70%)`,
}))

const TITLE_MAX = 64, TITLE_MIN = 24
const titleEl = ref<HTMLElement | null>(null)
const titleSize = ref(TITLE_MAX)
watch(song, async () => {
    if (!song.value) return
    titleSize.value = TITLE_MAX
    await nextTick()
    try { await (document as any).fonts.ready } catch { /* ignore */ }
    const el = titleEl.value
    for (let p = 0; el && p < 5 && el.scrollWidth > el.clientWidth; p++) {
        titleSize.value = Math.max(TITLE_MIN, Math.floor(titleSize.value * el.clientWidth / el.scrollWidth) - 1)
        await nextTick()
    }
}, {flush: 'post'})
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.stripe-layer { background: repeating-linear-gradient(-38deg, rgba(255,255,255,0.025) 0 3px, transparent 3px 26px); }

.id-pill, .bpm-pill { font-family: 'Torus', sans-serif; color: #fff; background: var(--pill-bg); border-radius: 9999px; box-shadow: var(--pill-shadow); }
.id-pill { font-weight: bold; font-size: 16px; letter-spacing: 0.06em; padding: 3px 12px; }
.bpm-pill { display: inline-flex; align-items: center; gap: 6px; padding: 3px 12px; }
.bpm-label { font-weight: bold; font-size: 12px; letter-spacing: 0.1em; color: rgba(255,255,255,0.55); }
.bpm-num { font-weight: bold; font-size: 16px; }
.meta-chip { font-family: 'SEGA NewRodin','LXGW WenKai',sans-serif; font-weight: bold; font-size: 14px; color: var(--chip-ink); padding: 4px 12px; border-radius: 9999px; background: var(--chip-bg); box-shadow: var(--chip-ring); white-space: nowrap; }

.cover-frame { padding: 5px; border-radius: 20px; background: rgba(255,255,255,0.78); box-shadow: 0 0 0 1px rgba(255,255,255,0.8), 0 8px 20px -12px rgba(0,0,0,0.5); }
.mai-title { font-family: 'SEGA NewRodin','LXGW WenKai',sans-serif; font-weight: 700; line-height: 1; color: #fff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); white-space: nowrap; overflow: hidden; padding-block: 6px 8px; margin-block: -6px -8px; }
.artist-line { font-family: 'Torus','SEGA Maru Gothic','LXGW WenKai',sans-serif; font-size: 19px; font-weight: bold; color: rgba(255,255,255,0.8); margin-top: 10px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

.section-tag { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; letter-spacing: 0.1em; border-radius: 9999px; padding: 4px 20px; color: #fff; box-shadow: 0 0 0 2px rgba(255,255,255,0.8); white-space: nowrap; }
.diff-chip { font-size: 17px; font-weight: 900; padding: 3px 14px; border: 2px solid; border-radius: 9999px; background: rgba(8,8,16,0.4); white-space: nowrap; }
.footer-text { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.4em; color: rgba(255,255,255,0.45); }
.foot-note { font-family: 'Microsoft YaHei',sans-serif; font-size: 12px; color: rgba(255,255,255,0.4); }

.stat { background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.10); border-radius: 12px; padding: 10px 14px; text-align: center; }
.stat-k { font-family: 'Microsoft YaHei',sans-serif; font-size: 13px; color: rgba(255,255,255,0.6); }
.stat-v { font-family: 'SEGA NewRodin','Microsoft YaHei',sans-serif; font-weight: 800; font-size: 27px; margin-top: 3px; white-space: nowrap; }
.stat-sub { font-family: 'Microsoft YaHei',sans-serif; font-size: 11.5px; color: rgba(255,255,255,0.5); margin-top: 2px; }
.stat-v :deep(.badge-of) { font-size: 16px; opacity: 0.55; }
.rank-of { font-size: 16px; opacity: 0.55; }

.tick { fill: #fff; fill-opacity: 0.55; font-size: 13px; font-family: 'SEGA NewRodin',sans-serif; }
.axis { fill: #fff; fill-opacity: 0.4; font-size: 12px; font-family: 'Microsoft YaHei',sans-serif; }
.ref { fill: #fff; fill-opacity: 0.6; font-size: 12.5px; font-family: 'Microsoft YaHei',sans-serif; }
.tabular-nums { font-variant-numeric: tabular-nums; }
</style>
