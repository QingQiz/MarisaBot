<template>
    <div v-if="song" class="mai-score mai-card relative w-[840px] overflow-hidden antialiased" :style="rootStyle">
        <div class="absolute inset-0 pointer-events-none stripe-layer"></div>
        <div class="absolute inset-0 pointer-events-none" :style="glowStyle"></div>

        <div class="relative px-12 pt-9 pb-10">
            <!-- ── top meta bar ── -->
            <header class="flex items-center gap-2 flex-nowrap whitespace-nowrap">
                <img :src="versionLogo" :style="logoStyle" class="h-[50px] shrink-0 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
                <div class="flex-1"></div>
                <img :src="typeBadge" class="h-9 drop-shadow-[0_3px_8px_rgba(0,0,0,0.4)]">
                <span v-if="song.IsNew" class="new-chip">NEW</span>
                <div class="bpm-pill">
                    <svg class="w-[18px] h-[18px] translate-y-[1px]" viewBox="0 0 24 24" fill="none">
                        <path d="M9.4 3h5.2c.5 0 .9.33 1 .8l3.1 14.6c.13.62-.34 1.2-1 1.2H6.3c-.66 0-1.13-.58-1-1.2L8.4 3.8c.1-.47.5-.8 1-.8z" stroke="currentColor" stroke-width="1.9" stroke-linejoin="round"/>
                        <path d="M12 15.2 17.6 5.6" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/>
                        <circle cx="12" cy="15.6" r="1.5" fill="currentColor"/>
                    </svg>
                    <span class="bpm-num tabular-nums">{{ song.Bpm }}</span>
                </div>
                <span class="meta-chip">{{ genreDisplay }}</span>
                <div class="id-pill tabular-nums">ID {{ song.Id }}</div>
            </header>

            <!-- ── cover + 标题（标题占满封面右侧整宽，曲师 + 玩家名同行） ── -->
            <div class="flex items-end gap-5 mt-7">
                <div class="cover-frame shrink-0">
                    <img :src="coverSrc" @error="onCoverErr" alt="" class="block w-[132px] h-[132px] object-cover rounded-[16px]">
                </div>
                <div class="flex-1 min-w-0 pb-1">
                    <h1 ref="titleEl" class="mai-title" :style="{ fontSize: titleSize + 'px' }">{{ song.Title }}</h1>
                    <div class="flex items-baseline justify-between gap-4 mt-[10px]">
                        <div class="artist-line min-w-0">{{ song.Artist }}</div>
                        <div v-if="player.Nickname" class="player-inline shrink-0"><span class="player-label">PLAYER</span> {{ player.Nickname }}</div>
                    </div>
                </div>
            </div>

            <!-- ── section tag ── -->
            <div class="flex items-center gap-4 mt-8 mb-4">
                <span class="section-tag">乐曲成绩</span>
                <span class="font-rodin text-[18px] tracking-[0.28em] text-white/70 whitespace-nowrap">SONG INFO</span>
                <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
            </div>

            <!-- ── 乐曲成绩表：左难度块 + 右侧单行平铺(列对齐) ── -->
            <div class="vc-tbl">
                <div class="vc-head">
                    <div class="vc-h-diff"></div>
                    <div class="vc-cells" :class="{ 'vc-cells--utg': isUtage }">
                        <div class="vc-h vc-h-c">达成率</div>
                        <div class="vc-h vc-h-c">评级</div>
                        <div v-if="!isUtage" class="vc-h vc-h-c">Ra</div>
                        <div class="vc-h vc-h-c">DX SCORE</div>
                        <div class="vc-h vc-h-c">DX%</div>
                        <div class="vc-h vc-h-c">DX星级</div>
                        <div class="vc-h vc-h-c">FC / FS</div>
                    </div>
                </div>
                <div v-for="c in charts" :key="c.LevelIndex" class="vc-row" :style="rowStyle(c.LevelIndex)">
                    <div class="vc-chip" :style="{ color: diffColor(c.LevelIndex), borderColor: diffColor(c.LevelIndex) }">
                        <span class="vc-chip-name" :style="isUtage ? { fontFamily: `'Microsoft YaHei', sans-serif` } : undefined">{{ diffName(c.LevelIndex) }}</span>
                        <span class="vc-chip-ds tabular-nums">{{ isUtage ? c.Level : c.Constant.toFixed(1) }}</span>
                    </div>
                    <div v-if="c.Played" class="vc-cells" :class="{ 'vc-cells--utg': isUtage }">
                        <div class="vc-ach tabular-nums"><span class="vc-ach-int">{{ achInt(c) }}</span>.{{ achDec(c) }}<span class="vc-ach-pct">%</span></div>
                        <div class="vc-cell vc-cell-c"><img :src="rankIcon(c)" alt="" :style="rankStyle(c)" class="vc-rank"></div>
                        <div v-if="!isUtage" class="vc-cell vc-cell-c vc-cell-ra tabular-nums">{{ c.Ra }}</div>
                        <div class="vc-cell vc-cell-c vc-cell-dx tabular-nums">{{ c.DxScore }}/{{ c.MaxDx }}</div>
                        <div class="vc-cell vc-cell-c vc-cell-rate tabular-nums">{{ dxRate(c) }}%</div>
                        <div class="vc-cell vc-cell-c"><img v-if="starN(c)" :src="starIcon(c)" alt="" class="vc-star"></div>
                        <div class="vc-cell vc-cell-mark">
                            <span class="vc-mslot"><img v-if="c.Fc" :src="fcIcon(c)" alt="" :style="markStyle(c.Fc)" class="vc-mark"></span>
                            <span class="vc-mslot"><img v-if="c.Fs" :src="fsIcon(c)" alt="" :style="markStyle(c.Fs)" class="vc-mark"></span>
                        </div>
                    </div>
                    <div v-else class="vc-unplayed">No Play Record</div>
                </div>
            </div>

            <footer class="mt-7">
                <span class="footer-text">MARISA BOT · SONG INFO</span>
            </footer>
        </div>
    </div>
</template>

<script setup lang="ts">
import {computed, nextTick, ref, watch} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {dxScoreStar} from '@/components/maimai/utils/ordinal'
import {
    DIFF_NAMES, DIFF_COLORS, UTAGE, isUtageId, themeMainOf, genreDisplayOf,
    VERSION_CODE, LOGO_BBOX_LEFT, versionLogoSrc, typeBadgeSrc,
    coverSrcOf, COVER_FALLBACK, bgKeyOf, cardBackground,
} from '@/components/maimai/utils/song_card'

interface ChartScore {
    LevelIndex: number; Level: string; Constant: number; Charter: string; MaxDx: number
    Played: boolean; Achievement: number | null; Rank: string | null; Ra: number | null
    Fc: string | null; Fs: string | null; DxScore: number | null
}
interface ScoreData {
    Song: { Id: number; Title: string; Type: string; Artist: string; Genre: string; Bpm: number; From: string; IsNew: boolean }
    Player: { Nickname: string }
    Charts: ChartScore[]
}

const route = useRoute()
const data  = ref<ScoreData | null>(null)

const song   = computed(() => data.value?.Song ?? null)
const player = computed(() => data.value?.Player ?? {Nickname: ''})
const charts = computed(() => data.value?.Charts ?? [])
const isUtage = computed(() => isUtageId(song.value?.Id ?? 0))

axios.get(context_get, {params: {id: route.query.id, name: 'SongScore'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})

function diffName(i: number) { return isUtage.value ? '宴' : DIFF_NAMES[Math.min(i, 4)] }
function diffColor(i: number) { return isUtage.value ? UTAGE.main : DIFF_COLORS[Math.min(i, 4)] }

const PIC = '/assets/maimai/pic'
function rankIcon(c: ChartScore) { return `${PIC}/rank_${c.Rank}.png` }
function fcIcon(c: ChartScore) { return `${PIC}/icon_${c.Fc}.png` }
function fsIcon(c: ChartScore) { return `${PIC}/icon_${c.Fs}.png` }
function starN(c: ChartScore) { return dxScoreStar(c.DxScore ?? 0, c.MaxDx) }
function starIcon(c: ChartScore) { return `${PIC}/music_icon_dxstar_${starN(c)}.png` }
function dxRate(c: ChartScore) { return c.MaxDx ? ((c.DxScore ?? 0) / c.MaxDx * 100).toFixed(1) : '0.0' }
function achInt(c: ChartScore) { return Math.floor(c.Achievement ?? 0) }
function achDec(c: ChartScore) { return (c.Achievement ?? 0).toFixed(4).split('.')[1] }

// FC/FS 标记与 rank 评级牌均已替换为游戏图集高分辨率版本（UI_MSS_MBase_Icon_* / UI_GAM_Rank_）并裁切到内容边界：
// 内容铺满画布、垂直居中、无截断，故各图标按同一 CSS 高度显示即视觉等高，无需再做 per-icon 归一化或垂直校正。
const MARK_BASE = 29, RANK_BASE = 32
function markStyle(_name: string | null) { return {height: MARK_BASE + 'px'} }
function rankStyle(_c: ChartScore) { return {height: RANK_BASE + 'px'} }

const typeBadge = computed(() => typeBadgeSrc(song.value?.Type))

const coverSrc = ref('')
watch(song, s => { if (s) coverSrc.value = coverSrcOf(s.Id) }, {immediate: true})
function onCoverErr() { coverSrc.value = COVER_FALLBACK }

const genreDisplay = computed(() => genreDisplayOf(song.value?.Genre))

const versionLogo = computed(() => versionLogoSrc(song.value?.From))
// 版本 logo 视觉左对齐（60px 高 / 素材 160px）
const logoStyle = computed(() => {
    const code = VERSION_CODE[song.value?.From ?? '']
    const trim = code ? (LOGO_BBOX_LEFT[code] ?? 0) * (60 / 160) : 0
    return {marginLeft: `${(-trim).toFixed(1)}px`}
})

// 背景按最高难度上色（有 Re:MASTER 取白谱档、宴会场取宴），跟 mai song 一致
const topIdx = computed(() => Math.max(0, charts.value.length - 1))
const topKey = computed(() => bgKeyOf(topIdx.value, isUtage.value))
const themeMain = computed(() => themeMainOf(topIdx.value, isUtage.value))
const rootStyle = computed(() => cardBackground(topKey.value))
const glowStyle = computed(() => ({
    background: `radial-gradient(720px 520px at 18% 8%, ${themeMain.value}2e 0%, transparent 70%),
                 radial-gradient(560px 480px at 92% 4%, ${themeMain.value}1c 0%, transparent 70%)`,
}))

function rowStyle(i: number) {
    const c = diffColor(i)
    return {
        border: '1px solid rgba(255,255,255,0.08)',
        boxShadow: `inset 5px 0 0 ${c}`,
        background: `linear-gradient(90deg, ${c}24 0%, rgba(0,0,0,0.34) 26%, rgba(0,0,0,0.34) 100%)`,
    }
}

// 标题真实测宽 auto-shrink（同 MaiSong 思路，简化）
const TITLE_MAX = 84, TITLE_MIN = 28
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

/* top meta pills */
.id-pill, .bpm-pill { font-family: 'Torus', sans-serif; color: #fff; background: var(--pill-bg); border-radius: 9999px; box-shadow: var(--pill-shadow); }
.id-pill { font-weight: bold; font-size: 16px; letter-spacing: 0.06em; padding: 3px 12px; }
.bpm-pill { display: inline-flex; align-items: center; gap: 6px; padding: 3px 12px 3px 10px; }
.bpm-num { font-weight: bold; font-size: 16px; }
.meta-chip { font-family: 'SEGA NewRodin','LXGW WenKai',sans-serif; font-weight: bold; font-size: 14px; color: var(--chip-ink); padding: 4px 12px; border-radius: 9999px; background: var(--chip-bg); box-shadow: var(--chip-ring); white-space: nowrap; }
.new-chip { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.18em; padding: 4px 11px 3px 13px; border-radius: 9999px; background: var(--new-chip-bg); box-shadow: var(--new-chip-shadow); }

.cover-frame { padding: 5px; border-radius: 22px; background: rgba(255,255,255,0.78); box-shadow: 0 0 0 1px rgba(255,255,255,0.8), 0 8px 20px -12px rgba(0,0,0,0.5); }
.artist-line { flex: 1; min-width: 0; font-family: 'Torus','SEGA Maru Gothic','LXGW WenKai',sans-serif; font-size: 20px; font-weight: bold; color: rgba(255,255,255,0.8); margin-top: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.player-inline { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; color: #fff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); white-space: nowrap; }
.player-label { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.18em; color: rgba(255,255,255,0.45); }

.section-tag { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; letter-spacing: 0.1em; border-radius: 9999px; padding: 4px 20px; background: #c64fe4; color: #fff; box-shadow: 0 0 0 2px rgba(255,255,255,0.8); white-space: nowrap; }
.footer-text { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.4em; color: rgba(255,255,255,0.45); }

/* shared row text */
.tabular-nums { font-variant-numeric: tabular-nums; }
.num { font-family: 'Torus',sans-serif; font-weight: bold; }

/* ───── 乐曲成绩表：左侧难度块 + 右侧单行平铺（列对齐 + 图标按内容高归一） ───── */
.vc-tbl { display: flex; flex-direction: column; gap: 9px; }
.vc-head { display: flex; align-items: flex-end; padding-bottom: 2px; }
.vc-head .vc-h-diff { width: 116px; flex-shrink: 0; }
.vc-h { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 13px; letter-spacing: 0.04em; color: rgba(255,255,255,0.5); }
.vc-h-c { text-align: center; }
.vc-row { display: flex; align-items: stretch; height: 54px; border-radius: 10px; overflow: hidden; }
.vc-chip { width: 116px; flex-shrink: 0; position: relative; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 1px; background: rgba(8,8,16,0.34); }
.vc-chip::after { content: ''; position: absolute; right: -1.5px; top: 11px; bottom: 11px; width: 4px; border-radius: 2px; background: currentColor; }
.vc-chip-name { font-family: 'SEGA NewRodin',sans-serif; font-weight: 900; font-size: 13px; }
.vc-chip-ds { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 25px; line-height: 1; }
.vc-cells { flex: 1; min-width: 0; display: grid; grid-template-columns: 110px 94px 32px 88px 54px 44px 76px; justify-content: space-between; align-items: center; padding-left: 16px; padding-right: 18px; }
/* 宴会场谱面无定数、Ra 无意义，去掉 Ra 列（表头与单元格均不渲染），列模板同步收成 6 列 */
.vc-cells--utg { grid-template-columns: 110px 94px 88px 54px 44px 76px; }
.vc-row .vc-cells { align-items: flex-start; padding-top: 11px; }
.vc-ach { display: flex; align-items: baseline; justify-content: flex-start; font-family: 'Torus',sans-serif; font-weight: bold; font-size: 19px; line-height: 1; }
.vc-ach-int { font-weight: 900; font-size: 31px; }
.vc-ach-pct { font-size: 14px; opacity: 0.62; margin-left: 1px; }
.vc-cell { display: flex; align-items: center; }
.vc-cell-c { justify-content: center; }
.vc-cell-ra { font-family: 'Torus',sans-serif; font-weight: 800; font-size: 19px; }
.vc-cell-dx { font-family: 'Torus',sans-serif; font-weight: 800; font-size: 19px; color: rgba(255,255,255,0.9); }
.vc-cell-rate { font-family: 'Torus',sans-serif; font-weight: 800; font-size: 19px; color: rgba(255,255,255,0.65); }
/* FC / FS 两个标记各占一个等宽槽位、在槽内左对齐：带「+」的图标更宽时只向右延伸，圆章左缘逐行对齐 */
.vc-cell-mark { gap: 0; transform: translateX(5px); }
.vc-mslot { flex: 1 1 0; min-width: 0; display: flex; align-items: center; justify-content: flex-start; }
.vc-rank { display: block; }
.vc-star { height: 28px; display: block; }
.vc-mark { display: block; }
.vc-unplayed { flex: 1; display: flex; align-items: center; padding-left: 20px; font-family: 'SEGA NewRodin', sans-serif; font-weight: bold; font-size: 20px; color: rgba(255,255,255,0.3); letter-spacing: 0.22em; }
</style>
