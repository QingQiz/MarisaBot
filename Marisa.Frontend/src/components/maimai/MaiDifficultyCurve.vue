<template>
    <MaiCardShell v-if="song && picked" class="mai-curve" :bg-key="bgKey" :accent="accent" pad-bottom="pb-8">
            <!-- ── top meta bar ── -->
            <MaiSongMetaBar :from="song.ver" :type="song.type" :song-id="songId"
                            :bpm="song.bpm" :genre="song.genre"/>

            <!-- ── cover + title ── -->
            <div class="flex items-end gap-5 mt-7">
                <MaiCover :song-id="songId" :size="112" :frame-radius="20" :img-radius="14"/>
                <MaiSongHeading :title="song.title" :artist="song.artist" :max="64" :min="24" :row-top="0"/>
            </div>

            <!-- ── section divider + difficulty chip ── -->
            <div class="flex items-center gap-4 mt-7 mb-4">
                <span class="font-rodin text-[20px] tracking-[0.28em] text-white/70 whitespace-nowrap">DIFFICULTY CURVE</span>
                <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
                <span class="diff-chip font-rodin" :style="{ color: diffColor, borderColor: diffColor }">
                    {{ diffName }} {{ chart.ds.toFixed(1) }}
                </span>
            </div>

            <!-- ── stats column + curve ── -->
            <div class="flex gap-5 items-stretch">
                <div class="w-[218px] shrink-0 flex flex-col gap-3">
                    <div v-if="isDsKind" class="stat">
                        <div class="stat-k">综合拟合定数</div>
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
                        <g v-for="mm in minMaxMarks" :key="mm.lbl">
                            <line :x1="ML" :y1="Y(mm.v)" :x2="SVG_W - MR" :y2="Y(mm.v)"
                                  :stroke="accent" stroke-opacity="0.35" stroke-dasharray="2 5" stroke-width="1.2"/>
                            <text :x="SVG_W - MR - 16" :y="Y(mm.v) + mm.dy" text-anchor="end" class="mm-label">
                                {{ mm.lbl }} {{ mm.v.toFixed(isDsKind ? 2 : 1) }}
                            </text>
                        </g>
                        <line :x1="ML" :y1="refY" :x2="SVG_W - MR" :y2="refY"
                              stroke="#fff" :stroke-opacity="isDsKind ? 0.45 : 0.35"
                              stroke-dasharray="7 6" stroke-width="1.5"/>
                        <text :x="ML + 8" :y="refLabelY">
                            <tspan class="ref">{{ isDsKind ? '官方定数 ' : refLabel }}</tspan>
                            <tspan v-if="isDsKind" class="ref-num">{{ chart.ds.toFixed(1) }}</tspan>
                        </text>
                        <path :d="curvePath" fill="none" :stroke="accent" stroke-width="3"
                              stroke-linejoin="round" stroke-linecap="round"/>
                        <circle :cx="endPt.x" :cy="endPt.y" r="4.5" :fill="accent"/>
                        <circle :cx="endPt.x" :cy="endPt.y" r="8" :fill="accent" opacity="0.25"/>
                        <text v-if="!isDsKind" class="axis" text-anchor="middle">
                            <tspan v-for="(ch, i) in yAxisLabel" :key="i" x="13" :y="yLabelStart + i * 17">{{ ch }}</tspan>
                        </text>
                        <text :x="(ML + SVG_W - MR) / 2" :y="svgH - 2" text-anchor="middle" class="axis-en">DX Rating</text>
                    </svg>
                </div>
            </div>

            <footer class="mt-5 flex items-baseline justify-between gap-6">
                <div class="foot-note min-w-0 whitespace-nowrap">数据来源：水鱼查分器</div>
                <span class="footer-text shrink-0">MARISA BOT · DIFFICULTY CURVE</span>
            </footer>
    </MaiCardShell>

    <div v-else-if="noData || song" class="mai-curve mai-card w-[840px] px-12 py-10 antialiased">
        <div class="text-[26px] font-bold">暂无难度曲线数据</div>
        <div class="mt-3 text-[16px] text-white/60">数据覆盖 Lv11 及以上的常规谱面，宴会场谱面与收录后新增的曲目暂不支持。</div>
    </div>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {DIFF_NAMES, DIFF_COLORS, bgKeyOf} from '@/components/maimai/utils/song_card'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiSongMetaBar from '@/components/maimai/MaiSongMetaBar.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'

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

// idx 查询参数显式指定难度（该难度无数据则走兜底卡）；缺省取 MASTER，无 MASTER 数据时取定数最高
const picked = computed<CurveChart | undefined>(() => {
    const cs = song.value!.charts
    if (route.query.idx !== undefined) return cs.find(c => c.li === Number(route.query.idx))
    return cs.find(c => c.li === 3) ?? cs.reduce((a, b) => b.ds > a.ds ? b : a)
})
const chart = computed<CurveChart>(() => picked.value!)

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
    return `同定数 AP 达成率 ${b.ap_rate_pct.toFixed(1)}%`
})

// ── 曲线绘制 ──
const SVG_W = 545, ML = 78, MR = 16, MT = 18, MB = 40
const svgH = 302

// 横轴按曲线支持范围截取：数据轴修复后支持窗为各谱自身人群的 rating 区间，
// 高难谱不再有低段假支持，固定全域会留大片空白
const xDomain = computed<[number, number]>(() => {
    const c = chart.value.curve
    return [c[0][0] - 300, c[c.length - 1][0] + 300]
})

const yDomain = computed<[number, number]>(() => {
    if (!isDsKind.value) return [-6, 106]
    const ys = chart.value.curve.map(p => p[1])
    return [Math.min(...ys, chart.value.ds) - 0.18, Math.max(...ys, chart.value.ds) + 0.18]
})
function X(v: number) {
    const [x0, x1] = xDomain.value
    return ML + (v - x0) / (x1 - x0) * (SVG_W - ML - MR)
}
function Y(v: number) {
    const [lo, hi] = yDomain.value
    return MT + (1 - (v - lo) / (hi - lo)) * (svgH - MT - MB)
}

// Catmull-Rom 平滑（曲线穿过全部数据点，仅显示层）。控制点 y 钳制在数据极值包络内：
// 样条在极值附近会过冲、戳出 MAX/MIN 参考线（贝塞尔曲线不出控制点凸包，钳完即不越界）
const curvePath = computed(() => {
    const pts = chart.value.curve.map(p => ({x: X(p[0]), y: Y(p[1])}))
    if (pts.length < 3) return 'M' + pts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' L')
    const yTop = Math.min(...pts.map(p => p.y)), yBot = Math.max(...pts.map(p => p.y))
    const cy = (v: number) => Math.min(Math.max(v, yTop), yBot)
    let d = `M${pts[0].x.toFixed(1)},${pts[0].y.toFixed(1)}`
    for (let i = 0; i < pts.length - 1; i++) {
        const p0 = pts[Math.max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.min(pts.length - 1, i + 2)]
        d += ` C${(p1.x + (p2.x - p0.x) / 6).toFixed(1)},${cy(p1.y + (p2.y - p0.y) / 6).toFixed(1)}`
           + ` ${(p2.x - (p3.x - p1.x) / 6).toFixed(1)},${cy(p2.y - (p3.y - p1.y) / 6).toFixed(1)}`
           + ` ${p2.x.toFixed(1)},${p2.y.toFixed(1)}`
    }
    return d
})
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

// 最大/最小参考线（QingQiz 建议）：标签分别外扩到线上方/下方，曲线扁平时也不互撞
const minMaxMarks = computed(() => {
    const ys = chart.value.curve.map(p => p[1])
    return [
        {lbl: 'MAX', v: Math.max(...ys), dy: -6},
        {lbl: 'MIN', v: Math.min(...ys), dy: 14},
    ]
})

const yTicks = computed(() => {
    if (!isDsKind.value) return [0, 25, 50, 75, 100].map(v => ({v, y: Y(v), label: String(v)}))
    const [lo, hi] = yDomain.value
    const out = []
    for (let v = Math.ceil(lo * 2) / 2; v < hi; v += 0.5) out.push({v, y: Y(v), label: v.toFixed(1)})
    return out
})
const xTicks = computed(() => {
    const [x0, x1] = xDomain.value
    const step = x1 - x0 <= 3200 ? 500 : 1000
    const out = []
    for (let v = Math.ceil(x0 / step) * step; v <= x1; v += step) out.push({v, x: X(v)})
    return out
})
const refY = computed(() => isDsKind.value ? Y(chart.value.ds) : Y(50))
// 参考线标签放线上还是线下：取标签 x 跨度内曲线离两个候选位置更远的一侧（End Time 这类
// 曲线贴着参考线走的谱，固定放线上会被整段盖住）
const refLabelY = computed(() => {
    const above = refY.value - 7, below = refY.value + 16
    const pts = chart.value.curve
        .map(p => ({x: X(p[0]), y: Y(p[1])}))
        .filter(p => p.x <= ML + 190)
    if (!pts.length) return above
    const clearance = (baseline: number) => Math.min(...pts.map(p => Math.abs(p.y - (baseline - 5))))
    return clearance(above) >= clearance(below) ? above : below
})
const refLabel = computed(() => isDsKind.value ? `官方定数 ${chart.value.ds.toFixed(1)}` : '同等级中位')
// 百分位模式坐标轴含义不自明，保留竖排说明；拟合定数模式删（QingQiz 反馈）
const yAxisLabel = computed(() => '同等级难度百分位')
// y 轴标签竖排、绘图区垂直居中
const yLabelStart = computed(() => {
    const mid = (MT + svgH - MB) / 2
    return mid - (yAxisLabel.value.length - 1) * 17 / 2 + 5
})

const bgKey = computed(() => bgKeyOf(chart.value?.li ?? 3, false))
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">

.diff-chip { font-size: 17px; font-weight: 900; padding: 3px 14px; border: 2px solid; border-radius: 9999px; background: rgba(8,8,16,0.4); white-space: nowrap; }

.stat { background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.10); border-radius: 12px; padding: 10px 14px; text-align: center; }
.stat-k { font-family: 'Microsoft YaHei',sans-serif; font-size: 13px; color: rgba(255,255,255,0.6); }
.stat-v { font-family: 'SEGA NewRodin','Microsoft YaHei',sans-serif; font-weight: 800; font-size: 27px; margin-top: 3px; white-space: nowrap; }
.stat-sub { font-family: 'Microsoft YaHei',sans-serif; font-size: 11.5px; color: rgba(255,255,255,0.5); margin-top: 2px; }
.stat-v :deep(.badge-of) { font-size: 16px; opacity: 0.55; }
.rank-of { font-size: 16px; opacity: 0.55; }

.tick { fill: #fff; fill-opacity: 0.55; font-size: 13px; font-family: 'SEGA NewRodin',sans-serif; }
.axis { fill: #fff; fill-opacity: 0.4; font-size: 12px; font-family: 'Microsoft YaHei',sans-serif; }
.axis-en { fill: #fff; fill-opacity: 0.45; font-size: 13px; font-weight: bold; font-family: 'Torus',sans-serif; letter-spacing: 0.08em; }
.ref { fill: #fff; fill-opacity: 0.6; font-size: 12.5px; font-family: 'Microsoft YaHei',sans-serif; }
.mm-label { fill: #fff; fill-opacity: 0.55; font-size: 11px; font-weight: bold; font-family: 'Torus',sans-serif; letter-spacing: 0.05em; }
.ref-num { fill: #fff; fill-opacity: 0.6; font-size: 13px; font-weight: bold; font-family: 'Torus',sans-serif; }
.tabular-nums { font-variant-numeric: tabular-nums; }
</style>
