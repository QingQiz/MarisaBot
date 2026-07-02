<template>
    <div v-if="song" class="mai-song mai-card relative w-[1240px] overflow-hidden antialiased"
         :class="{ 'is-utage': isUtage }" :style="rootStyle">
        <!-- 机台斜纹 + 难度主题色辉光 -->
        <div class="absolute inset-0 pointer-events-none stripe-layer"></div>
        <div class="absolute inset-0 pointer-events-none" :style="glowStyle"></div>

        <div class="relative px-12 pt-10 pb-8">
            <!-- ── 顶栏：版本 logo → 类型章 → NEW → BPM → genre → ID ── -->
            <header ref="headerEl" class="flex items-center gap-3 mr-[44px]">
                <img :src="versionLogo" :alt="song.From" :style="logoStyle"
                     class="h-[68px] shrink-0 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
                <div class="flex-1"></div>
                <div class="flex items-center gap-3 shrink-0">
                    <img :src="typeBadge" :alt="song.Type"
                         class="h-11 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
                    <span v-if="song.IsNew" class="new-chip">NEW</span>
                </div>
                <div class="bpm-pill shrink-0">
                    <svg class="w-[20px] h-[20px] translate-y-[1.5px]" viewBox="0 0 24 24" fill="none" aria-label="BPM">
                        <path d="M9.4 3h5.2c.5 0 .9.33 1 .8l3.1 14.6c.13.62-.34 1.2-1 1.2H6.3c-.66 0-1.13-.58-1-1.2L8.4 3.8c.1-.47.5-.8 1-.8z"
                              stroke="currentColor" stroke-width="1.9" stroke-linejoin="round"/>
                        <path d="M12 15.2 17.6 5.6" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/>
                        <circle cx="12" cy="15.6" r="1.5" fill="currentColor"/>
                    </svg>
                    <span class="bpm-num tabular-nums">{{ song.Bpm }}</span>
                </div>
                <span class="meta-chip shrink-0" :style="{ fontSize: genreFs + 'px' }">{{ genreDisplay }}</span>
                <div class="id-pill tabular-nums shrink-0">ID {{ song.Id }}</div>
            </header>

            <!-- ── 标题 + 曲师 ── -->
            <h1 ref="titleEl" class="mai-song-title w-[1000px] ml-[44px] !mt-[13px]"
                :style="{ fontSize: titleSize + 'px' }">{{ song.Title }}</h1>
            <div class="artist-line w-[1000px] ml-[44px] mt-[28px]">{{ song.Artist }}</div>

            <!-- ── 双列：左 曲绘+DX 分栏 / 右 谱面信息；左右对称 92px ── -->
            <div class="flex gap-7 mt-[26px] ml-[44px] mr-[44px]">
                <div class="left-col w-[452px] shrink-0 flex flex-col gap-7" :style="{ zoom: leftZoom }">
                    <div class="cover-frame self-start">
                        <img :src="coverSrc" @error="OnCoverError" alt=""
                             class="block w-[440px] h-[440px] object-cover rounded-[24px]">
                    </div>
                    <div v-if="starInfos.length" class="flex flex-col gap-3 w-full">
                        <div v-for="info in starInfos" :key="info.name" class="star-block" :style="starBlockStyle(info.idx)">
                            <!-- 头部定宽：MASTER / Re:MASTER 两栏的星星列对齐 -->
                            <div class="shrink-0 w-[118px]">
                                <div class="star-head-title">DX SCORE</div>
                                <div class="star-head-sub">{{ info.name }} 谱面</div>
                                <div class="star-head-max tabular-nums">MAX {{ info.maxDx }}</div>
                            </div>
                            <div v-for="(cell, i) in info.cells" :key="i" class="star-cell">
                                <img :src="`/assets/maimai/pic/music_icon_dxstar_${i + 1}.png`"
                                     alt="" class="star-icon drop-shadow-[0_2px_5px_rgba(0,0,0,0.35)]">
                                <div class="star-thresh tabular-nums">{{ cell.threshold }}</div>
                                <div class="star-delta tabular-nums">{{ cell.delta }}</div>
                            </div>
                        </div>
                    </div>
                </div>

            <!-- ── 谱面信息（右列；zoom 整体缩放，内部保持 624 设计宽度） ── -->
            <section class="right-col flex-1 min-w-0" :style="{ zoom: rightZoom }">
                <div class="flex items-center gap-4 h-[44px] mb-[14px]">
                    <span class="section-tag shrink-0 whitespace-nowrap"
                          :style="{ background: theme.main, color: theme.onMain }">谱面信息</span>
                    <span class="font-rodin text-[20px] tracking-[0.25em] shrink-0 whitespace-nowrap text-white/75">CHART DATA</span>
                    <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
                    <span class="font-torus font-bold text-[18px] tracking-[0.2em] shrink-0 whitespace-nowrap text-white/55">
                        {{ isUtage ? 'U·TA·GE' : song.Charts.length + ' CHARTS' }}
                    </span>
                </div>

                <div class="flex flex-col gap-[10px]">
                    <div v-for="(chart, i) in song.Charts" :key="i"
                         class="chart-row relative h-[144px] rounded-[10px] overflow-hidden"
                         :style="rowStyle(i)">
                        <!-- 行头双层 chip：难度名（难度色）+ 等级（向白混色提亮层；定数已移到徽章） -->
                        <div class="flex items-stretch">
                            <div class="diff-chip-long shrink-0" :style="{ color: !isUtage && i === 4 ? '#4a1d78' : '#fff' }">
                                <span class="chip-name-seg"
                                      :style="{ background: rowColor(i), fontFamily: isUtage ? `'Microsoft YaHei', sans-serif` : undefined }">
                                    {{ isUtage ? '宴' : DIFF_NAMES[i] }}</span>
                                <span class="chip-lv-seg tabular-nums"
                                      :style="{ background: lighten(rowColor(i), 0.16), color: !isUtage && i === 4 ? '#4a1d78' : '#fff', width: chipLvWidth }">
                                    {{ chart.Level }}</span>
                            </div>
                            <!-- 谱师名义：行右上角；无谱师（- / N/A / 空，常见于 BASIC/ADVANCED）统一显示淡化的 N/A -->
                            <div class="flex items-center ml-auto pr-[14px] mt-[-2px] min-w-0">
                                <div class="font-rodin leading-tight whitespace-nowrap"
                                     :class="hasCharter(chart.Charter) ? 'text-white' : 'text-white/40'"
                                     :style="charterStyle(charterText(chart.Charter), 300, 22, 13)">
                                    {{ charterText(chart.Charter) }}
                                </div>
                            </div>
                        </div>
                        <!-- 定数徽章：行右下，难度色描边 + 霓虹光晕（宴谱无定数则显示等级） -->
                        <div class="lv-badge" :style="lvBadgeStyle(i)">{{ isUtage ? chart.Level : chart.Constant.toFixed(1) }}</div>
                        <!-- 物量区：在行头横条下界到行下界之间垂直居中 -->
                        <div class="absolute left-[12px] right-[10px] top-[38px] bottom-0 flex items-center pr-[124px] pl-[6px]">
                        <div class="flex items-end w-full">
                            <div v-for="(label, k) in noteLabels(chart)" :key="label" class="note-cell">
                                <div class="note-label" :style="{ color: noteColors[label] }">{{ label }}</div>
                                <div class="note-value">{{ chart.Notes[k] }}</div>
                                <div v-if="label === 'BREAK'" class="note-loss tabular-nums">
                                    50<span class="label-cjk">落</span> -{{ break50Loss(chart) }}
                                </div>
                                <div v-else class="note-loss tabular-nums">-{{ noteLoss(chart, label) }}</div>
                            </div>
                            <div class="sep-line"></div>
                            <div class="note-cell">
                                <div class="note-label text-white/80">COMBO</div>
                                <div class="note-value">{{ totalCombo(chart) }}</div>
                                <div class="note-loss opacity-0">-</div>
                            </div>
                            <div class="sep-line"></div>
                            <div class="note-cell">
                                <div class="note-label text-white/80">MAX DX</div>
                                <div class="note-value">{{ chart.MaxDx }}</div>
                                <div class="note-loss opacity-0">-</div>
                            </div>
                        </div>
                        </div>
                    </div>
                </div>

            </section>
            </div>

            <footer class="mt-6 ml-[44px]">
                <span class="footer-text">MARISA BOT · SONG</span>
            </footer>
        </div>
    </div>
</template>

<script setup lang="ts">
import {computed, nextTick, ref, watch, watchEffect} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {
    DIFF_NAMES, DIFF_COLORS, UTAGE, isUtageId, themeMainOf, genreDisplayOf,
    VERSION_CODE, LOGO_BBOX_LEFT, versionLogoSrc, typeBadgeSrc,
    coverSrcOf, COVER_FALLBACK, bgKeyOf, cardBackground,
} from '@/components/maimai/utils/song_card'

interface Chart {
    Level: string
    Constant: number
    Charter: string
    Notes: number[]
    MaxDx: number
}

interface Song {
    Id: number
    Title: string
    Type: 'DX' | 'SD'
    Artist: string
    Genre: string
    Bpm: number
    From: string
    IsNew: boolean
    Charts: Chart[]
}

// 难度深色（描边/阴影用），main 色沿用公用 DIFF_COLORS
const DIFF_DEEP = ['#2f9e12', '#c77f00', '#e02b3a', '#9a2dbb', '#8e4ed1']
const DIFF = DIFF_NAMES.map((name, i) => ({name, main: DIFF_COLORS[i], deep: DIFF_DEEP[i]}))

const route = useRoute()
const song  = ref<Song | null>(null)

axios.get(context_get, {params: {id: route.query.id, name: 'SongData'}}).then(res => {
    song.value = res.data
})

const isUtage = computed(() => isUtageId(song.value?.Id ?? 0))
const topIdx  = computed(() => (song.value?.Charts.length ?? 1) - 1)

const theme = computed(() => {
    const main = themeMainOf(topIdx.value, isUtage.value)
    const onMain = !isUtage.value && topIdx.value === 4 ? '#4a1d78' : '#ffffff'
    return {main, onMain}
})

function DiffOf(i: number) {
    return isUtage.value ? UTAGE : DIFF[Math.min(i, DIFF.length - 1)]
}

const genreDisplay = computed(() => genreDisplayOf(song.value?.Genre))

const versionLogo = computed(() => versionLogoSrc(song.value?.From))

// 版本 logo 视觉左对齐：素材左侧透明留白按显示倍率（68px 高 / 素材 160px）补偿，
// 使可见字形左缘 = 标题左缘 92px
const logoStyle = computed(() => {
    const code = VERSION_CODE[song.value?.From ?? '']
    const trim = code ? (LOGO_BBOX_LEFT[code] ?? 0) * (68 / 160) : 5 * (68 / 292)
    return {marginLeft: `${(44 - trim).toFixed(1)}px`}
})

// 顶栏自适应：溢出时缩 genre 字号（真实测宽）
const headerEl = ref<HTMLElement | null>(null)
const genreFs  = ref(17)

async function FitHeader() {
    const h = headerEl.value
    if (!h) return
    genreFs.value = 17
    await nextTick()
    try { await document.fonts.ready } catch { /* 测量兜底 */ }
    for (let i = 0; i < 6 && h.scrollWidth > h.clientWidth && genreFs.value > 12; i++) {
        genreFs.value--
        await nextTick()
    }
}

const typeBadge = computed(() => typeBadgeSrc(song.value?.Type))

const coverSrc = ref('')
watchEffect(() => {
    if (song.value) coverSrc.value = coverSrcOf(song.value.Id)
})

function OnCoverError() {
    coverSrc.value = COVER_FALLBACK
}

// 标题真实测宽 auto-shrink
const TITLE_MAX = 110
const TITLE_MIN = 24
const titleEl   = ref<HTMLElement | null>(null)
const titleSize = ref(TITLE_MAX)

async function FitTitle() {
    const el = titleEl.value
    if (!el || !song.value) return
    titleSize.value = TITLE_MAX
    await nextTick()
    try {
        await Promise.all([
            document.fonts.load(`700 ${TITLE_MAX}px 'SEGA NewRodin'`, song.value.Title),
            document.fonts.load(`700 ${TITLE_MAX}px 'LXGW WenKai'`, song.value.Title),
        ])
    } catch { /* 测量兜底 */ }
    await nextTick()
    for (let pass = 0; pass < 4 && el.scrollWidth > el.clientWidth; pass++) {
        const next = Math.floor(titleSize.value * el.clientWidth / el.scrollWidth)
        titleSize.value = Math.max(TITLE_MIN, Math.min(next, titleSize.value - 1))
        await nextTick()
        if (titleSize.value <= TITLE_MIN) break
    }
}

// 物量数值居中自校正：渲染后量 Range 实框 vs 格子实框，把中心差值平移回去。
// transform 的 px 在 zoom 前坐标系、量测在 zoom 后视觉坐标系 → 用闭环迭代：
// 应用→复测残差→按实测响应比例补偿，对任意 zoom 自适应收敛
async function OpticalCenterValues() {
    await nextTick()
    try { await document.fonts.ready } catch { /* 量测兜底 */ }
    await nextTick()
    const delta = (cell: HTMLElement, val: HTMLElement) => {
        const range = document.createRange()
        range.selectNodeContents(val)
        const tb = range.getBoundingClientRect()
        const cb = cell.getBoundingClientRect()
        return (cb.left + cb.width / 2) - (tb.left + tb.width / 2)
    }
    document.querySelectorAll<HTMLElement>('.note-cell').forEach(cell => {
        const val = cell.querySelector<HTMLElement>('.note-value')
        if (!val || !(val.textContent ?? '').trim()) return
        val.style.transform = ''
        let shift = 0
        let scale = 1   // transform 本地 px → 视觉 px 的响应比例（zoom 影响），实测估计
        for (let pass = 0; pass < 3; pass++) {
            const d = delta(cell, val)
            if (Math.abs(d) <= 0.5) break
            const prev = shift
            shift += d / scale
            val.style.transform = `translateX(${shift.toFixed(2)}px)`
            const d2 = delta(cell, val)
            const applied = shift - prev
            if (Math.abs(applied) > 0.01) {
                const responded = d - d2
                if (responded > 0.01) scale = responded / applied
            }
        }
    })
}

// 双列等比缩放：左列 zoom=k、右列 zoom=m，解方程组
//   高度对齐: Hl·k = Hr·m
//   宽度填满: Wl·k + gap + Wr·m = avail
// 右列内部始终按 624 设计宽度排版（不会被挤），只整体温和缩放
const leftZoom  = ref(1)
const rightZoom = ref(1)

async function FitColumns() {
    leftZoom.value = 1
    rightZoom.value = 1
    await nextTick()
    try { await document.fonts.ready } catch { /* 量测兜底 */ }
    await nextTick()
    const left   = document.querySelector<HTMLElement>('.left-col')
    const charts = document.querySelector<HTMLElement>('.right-col')
    const last   = left?.lastElementChild
    const rLast  = charts?.lastElementChild
    if (!left || !charts || !last || !rLast) return
    // 两列盒都会被 flex stretch 拉高，自然高度一律按内容末端量
    const Hl = last.getBoundingClientRect().bottom - left.getBoundingClientRect().top
    const Hr = rLast.getBoundingClientRect().bottom - charts.getBoundingClientRect().top
    const Wl = 452, Wr = 624, GAP = 28
    const avail = (left.parentElement?.clientWidth ?? 1104) - GAP
    let m = avail / (Wl * Hr / Hl + Wr)
    m = Math.min(Math.max(m, 0.8), 1.15)
    let k = (Hr / Hl) * m
    const over = Wl * k + Wr * m - avail
    if (over > 0) {
        const s = avail / (Wl * k + Wr * m)
        k *= s
        m *= s
    }
    leftZoom.value  = Math.round(k * 1000) / 1000
    rightZoom.value = Math.round(m * 1000) / 1000
}

// 串行执行：缩放定稿之后再做数值光学居中（避免量测时序竞争）
watch(() => song.value?.Title, async () => {
    await FitTitle()
    await FitHeader()
    await FitColumns()
    await OpticalCenterValues()
}, {flush: 'post'})

// DX 分星线（ordinal.ts dxScoreStar 同口径）。
// 有 Re:MASTER（5 谱）时出 MASTER + Re:MASTER 上下两栏，否则只出最高难度一栏
const STAR_PCT = [85, 90, 93, 95, 97]

function starOf(idx: number) {
    const c = song.value!.Charts[idx]
    return {
        idx,
        name:  DiffOf(idx).name,
        maxDx: c.MaxDx,
        cells: STAR_PCT.map(pct => {
            const threshold = Math.ceil(c.MaxDx * pct / 100)
            return {threshold, delta: threshold - c.MaxDx}
        }),
    }
}

const starInfos = computed(() => {
    const charts = song.value?.Charts
    if (!charts || charts.length === 0 || isUtage.value) return []
    return charts.length === 5 ? [starOf(3), starOf(4)] : [starOf(charts.length - 1)]
})

// ── 难度主题暗色背景（公用 bgKeyOf / cardBackground）──
const topKey = computed(() => bgKeyOf(topIdx.value, isUtage.value))

const rootStyle = computed(() => cardBackground(topKey.value))

const glowStyle = computed(() => ({
    background: `radial-gradient(620px 620px at 24% 30%, ${theme.value.main}30 0%, transparent 70%),
                 radial-gradient(520px 420px at 88% 6%, ${theme.value.main}1c 0%, transparent 70%)`,
}))

// 星线栏底：与难度行同语言，色染→暗的渐变界限更靠左（30% vs 行的 34%）
function starBlockStyle(i: number) {
    const c = DIFF_COLORS[i] ?? '#999'
    return {
        background: `linear-gradient(90deg, ${c}26 0%, rgba(0,0,0,0.42) 30%, rgba(0,0,0,0.42) 100%)`,
        border: '1px solid rgba(255,255,255,0.09)',
        boxShadow: `inset 4px 0 0 ${c}`,
    }
}

// 行主题色：宴谱粉、普通难度按 index
function rowColor(i: number) {
    return themeMainOf(i, isUtage.value)
}

function rowStyle(i: number) {
    const c = rowColor(i)
    return {
        border: '1px solid rgba(255,255,255,0.09)',
        boxShadow: `inset 4px 0 0 ${c}`,
        background: `linear-gradient(90deg, ${c}26 0%, rgba(0,0,0,0.42) 34%, rgba(0,0,0,0.42) 100%)`,
    }
}

// 行头右段统一宽度：取本曲所有难度中最长的等级 label 计宽（定数已移到徽章），
// 左段定宽 132 + 右段统一 → 各行横条总宽一致
const chipLvWidth = computed(() => {
    const maxLen = Math.max(0, ...(song.value?.Charts.map(c => c.Level.length) ?? []))
    return `${Math.round(maxLen * 10.2) + 46}px`   // Torus 18px 数字 ≈10.2px/字 + 左右 padding（含「+」余量）
})

// 定数徽章：深色底 + 难度色描边 + 同色大数字 + 霓虹光晕。非宴谱显示定数（带一位小数比纯
// 等级宽）→ 统一缩到 34px 使各行字号一致；宴谱无定数仍显示等级，超长略缩
function lvBadgeStyle(i: number) {
    const c = isUtage.value ? '#f04fc6' : DIFF_COLORS[i] ?? '#999'
    const glow = {
        background: 'rgba(8, 8, 16, 0.35)',
        border: `2.5px solid ${c}`,
        color: c,
        boxShadow: `0 0 18px ${c}66, inset 0 0 12px ${c}2e`,
        textShadow: `0 0 10px ${c}88`,
    }
    if (!isUtage.value) return {...glow, fontSize: '34px'}
    const lv = song.value?.Charts[i]?.Level ?? ''
    return {...glow, ...(lv.length >= 4 ? {fontSize: '30px'} : {})}
}

function textUnits(s: string, latin: number) {
    let w = 0
    for (const c of s) w += (c.codePointAt(0) ?? 0) > 0xFF ? 1.0 : latin
    return w
}

// 谱师缺省（- / N/A / 空，常见于 BASIC/ADVANCED）统一显示占位 N/A
function hasCharter(charter: string) {
    return !!charter && charter !== '-' && charter !== 'N/A'
}

function charterText(charter: string) {
    return hasCharter(charter) ? charter : 'N/A'
}

// 谱师名义（纯日西文 → NewRodin EB）auto-shrink 完整显示
function charterStyle(charter: string, budget: number, max: number, min: number) {
    const u = Math.max(textUnits(charter, 0.8), 1)
    return {fontSize: `${Math.max(min, Math.min(max, Math.floor(budget / u)))}px`}
}

const noteColors: Record<string, string> = {
    TAP: '#ff7bac', HOLD: '#ffd23e', SLIDE: '#53c8ff', TOUCH: '#3fe0c5', BREAK: '#ff9f1c',
}

function noteLabels(chart: Chart) {
    return chart.Notes.length === 5
        ? ['TAP', 'HOLD', 'SLIDE', 'TOUCH', 'BREAK']
        : ['TAP', 'HOLD', 'SLIDE', 'BREAK']
}

function totalCombo(chart: Chart) {
    return chart.Notes.reduce((a, b) => a + b, 0)
}

// 颜色向白混色提亮（t∈0..1，1=纯白）——暗底上比降透明度更浅
function lighten(hex: string, t: number) {
    const n = parseInt(hex.slice(1), 16)
    const f = (v: number) => Math.round(v + (255 - v) * t)
    return `rgb(${f((n >> 16) & 255)}, ${f((n >> 8) & 255)}, ${f(n & 255)})`
}

// 达成率损失（同 mai 容错率 / MaiMaiSong.NoteScore 口径）：
// x = 100/(tap+2*hold+3*slide+5*break+touch)，y = 1/break（绝赞 bonus 共 1%）
function noteXY(chart: Chart) {
    const n = chart.Notes
    const tap = n[0], hold = n[1], slide = n[2]
    const touch = n.length === 5 ? n[3] : 0
    const brk = n[n.length - 1]
    const x = 100 / (tap + 2 * hold + 3 * slide + 5 * brk + touch)
    const y = brk > 0 ? 1.0 / brk : 0
    return {x, y}
}

// TAP/HOLD/SLIDE/TOUCH 的 MISS 损失：x / 2x / 3x / x
function noteLoss(chart: Chart, label: string): string {
    const {x} = noteXY(chart)
    const v = label === 'HOLD' ? 2 * x : label === 'SLIDE' ? 3 * x : x
    return v.toFixed(4)
}

// BREAK 显示 50 落损失（绝赞 2600→2550，每个减 0.25y，同容错率文案口径）
function break50Loss(chart: Chart): string {
    return (0.25 * noteXY(chart).y).toFixed(4)
}

</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
/* 机台斜纹 */
.stripe-layer {
    background: repeating-linear-gradient(
        -38deg,
        rgba(255, 255, 255, 0.028) 0 3px,
        transparent 3px 26px
    );
}

/* ── 顶栏 ── */
.id-pill {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 22px;
    letter-spacing: 0.08em;
    color: #fff;
    background: var(--pill-bg);
    padding: 5px 18px;
    border-radius: 9999px;
    box-shadow: var(--pill-shadow);
}

.bpm-pill {
    display: inline-flex;
    align-items: center;
    gap: 9px;
    color: #fff;
    background: var(--pill-bg);
    padding: 5px 18px 5px 14px;
    border-radius: 9999px;
    box-shadow: var(--pill-shadow);
}

.bpm-num {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 22px;
    line-height: 1.2;
}

.is-utage .id-pill {
    background: rgba(122, 22, 110, 0.85);
}

.cover-frame {
    padding: 6px;
    border-radius: 30px;
    background: rgba(255, 255, 255, 0.75);
    /* 下坠柔影收紧：避免视觉下界超出右列（盒子本身已对齐） */
    box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.8),
                0 8px 20px -12px rgba(0, 0, 0, 0.5);
}

.new-chip {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 15px;
    letter-spacing: 0.22em;
    color: #fff;
    padding: 5px 14px 4px 16px;
    border-radius: 9999px;
    background: var(--new-chip-bg);
    box-shadow: var(--new-chip-shadow);
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}

.meta-chip {
    font-family: 'SEGA NewRodin', 'LXGW WenKai', sans-serif;
    font-weight: bold;
    font-size: 17px;
    letter-spacing: 0.05em;
    color: var(--chip-ink);
    padding: 5px 16px;
    border-radius: 9999px;
    background: var(--chip-bg);
    box-shadow: var(--chip-ring),
                0 3px 10px rgba(90, 40, 120, 0.15);
    backdrop-filter: blur(8px);
}

/* ── 标题 / 曲师 ── */
.mai-song-title {
    font-family: 'SEGA NewRodin', 'LXGW WenKai', sans-serif;
    font-weight: 700;
    /* 整数行高：小数行高会把下方布局推到小数像素，条底边抗锯齿在分割线缺口处发亮 */
    line-height: 1;
    color: #fff;
    text-shadow: 0 2px 4px rgba(0, 0, 0, 0.5), 0 4px 22px rgba(0, 0, 0, 0.4);
    white-space: nowrap;
    overflow: hidden;
    /* overflow 在 padding 边裁切，故底部 padding 是给降部（g/y/p 等）留的安全区，margin 抵消其布局影响。
       字号 auto-shrink 到 110px，固定 10px 不够会切掉降部，底部改用 em 随字号缩放。 */
    padding-block: 8px 0.22em;
    margin-block: -8px -0.22em;
}

.artist-line {
    font-family: 'Torus', 'SEGA Maru Gothic', 'LXGW WenKai', sans-serif;
    font-size: 23px;
    font-weight: bold;
    line-height: 33px;
    color: rgba(255, 255, 255, 0.82);
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
}

/* ── 谱面信息 ── */
.section-tag {
    font-family: 'Microsoft YaHei', sans-serif;
    font-weight: bold;
    font-size: 23px;
    letter-spacing: 0.12em;
    border-radius: 9999px;
    padding: 4px 22px;
    box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.8), 0 4px 12px rgba(90, 40, 120, 0.25);
}

/* 行头双层 chip：左段难度色放难度名，右段淡化同色放等级（定数已移到徽章）。
   宽度统一固定、内部分栏定宽 → 各行横条整齐对位 */
.diff-chip-long {
    display: inline-flex;
    align-items: stretch;
    overflow: hidden;
    border-radius: 10px 0 14px 0;   /* 右端也只下角圆（上角方），与分割线一致 */
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.35);
}

/* 左段右缘做半药丸凸弧伸进右段（弧度=条右端圆角） */
.chip-name-seg {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 132px;
    padding: 9px 10px 10px 8px;
    font-family: 'SEGA NewRodin', 'LXGW WenKai', sans-serif;
    font-weight: 900;
    font-size: 15px;
    letter-spacing: 0.04em;
    line-height: 1;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.18);
    border-radius: 0 0 14px 0;   /* 分割线只下角圆（上角方），弧度与条右端(14px)一致 */
    position: relative;
    z-index: 1;
}

/* 高度凑偶数 38px：奇数高在 1.5x 设备倍率下底边落半像素，浅段半像素混色像一条深线 */
.chip-lv-seg {
    display: flex;
    align-items: center;
    justify-content: center;
    flex: none;
    margin-left: -14px;
    padding: 10px 16px 10px 26px;
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 18px;
    line-height: 1;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

/* 等级徽章：行右侧下方居中放大号标级 */
.lv-badge {
    position: absolute;
    right: 14px;
    bottom: 10px;
    width: 86px;
    height: 86px;
    border-radius: 22px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 41px;
    line-height: 1;
}

.label-cjk {
    font-family: 'Microsoft YaHei', sans-serif;
    font-weight: bold;
}

.note-cell {
    flex: 1 1 0;
    text-align: center;
    min-width: 0;
}

/* 独立分隔线：COMBO 居中于两线之间，MAX DX 与左线间距对称 */
.sep-line {
    width: 2px;
    align-self: stretch;
    background: rgba(255, 255, 255, 0.16);
    margin: 0 10px;
}

/* 单 note MISS 达成率损失（字号同 DX SCORE 栏的差值小字） */
.note-loss {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 11px;
    line-height: 1.1;
    margin-top: 3px;
    color: rgba(255, 255, 255, 0.5);
    white-space: nowrap;
}

.note-label {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 12px;
    letter-spacing: 0.1em;
    text-shadow: 0 1px 3px rgba(0, 0, 0, 0.6);
}

/* 居中数字不用 tabular-nums："1" 的等宽位空白会造成墨迹偏右的视觉错位 */
.note-value {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 32px;
    line-height: 1.1;
    color: #fff;
    text-shadow: 0 2px 4px rgba(0, 0, 0, 0.55);
}

/* ── 页脚 ── */
.footer-text {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 13px;
    letter-spacing: 0.4em;
    color: rgba(255, 255, 255, 0.5);
}

/* ── 左列 DX 分星线（两栏 w-full 自动等宽，总宽贴住封面 452px） ── */
.star-block {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    width: 100%;
    padding: 10px 14px 9px;
    border-radius: 10px;
}

.star-head-title {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 11px;
    letter-spacing: 0.12em;
    color: rgba(255, 255, 255, 0.6);
}

.star-head-sub {
    font-family: 'Torus', 'Microsoft YaHei', sans-serif;
    font-weight: bold;
    font-size: 13px;
    color: rgba(255, 255, 255, 0.92);
    margin-top: 2px;
    white-space: nowrap;
}

.star-head-max {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 11px;
    color: rgba(255, 255, 255, 0.55);
    margin-top: 1px;
}

.star-cell {
    width: 56px;
    text-align: center;
}

.star-icon {
    display: block;
    width: 36px;
    height: 36px;
    margin: 0 auto;
    object-fit: contain;
}

.star-thresh {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 15px;
    line-height: 1.2;
    color: #fff;
    text-shadow: 0 2px 4px rgba(0, 0, 0, 0.45);
    margin-top: 2px;
}

.star-delta {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    font-size: 11px;
    line-height: 1.1;
    color: rgba(255, 255, 255, 0.55);
}
</style>
