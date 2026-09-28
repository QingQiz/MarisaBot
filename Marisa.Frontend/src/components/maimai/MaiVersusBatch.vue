<template>
    <MaiCardShell v-if="data" class="batch-card" :width="960" :bg-key="bgKeyOf(3, false)" :accent="DIFF_COLORS[3]">
        <header class="batch-header">
            <div class="batch-heading">
                <img :src="versionLogoSrc(data.Version)" class="version-logo" alt="">
                <div><div class="eyebrow">MARISA BOT · VERSUS</div><h1>批量对战</h1></div>
            </div>
            <div class="page-badge"><strong>{{ pageNumber(data.Page) }}</strong><span>/ {{ pageNumber(pageCount) }}</span></div>
        </header>

        <div class="scope-line">
            <span class="scope-chip">{{ data.Scope }}</span>
            <span>{{ data.TotalCharts }} 谱面</span>
            <span class="separator">·</span>
            <span>{{ data.SortLabel }}</span>
        </div>

        <section class="match-summary">
            <div class="player-summary player-a"><span>{{ data.Players[0].Name }}</span><div><b>{{ data.Summary.LeftWins }}</b><small>胜</small></div></div>
            <div class="summary-center"><b>VS</b><span>全范围战绩</span><div>{{ data.Summary.Draws }} 平</div><div class="summary-unplayed">{{ data.Summary.Unplayed }} 双方未游玩</div></div>
            <div class="player-summary player-b"><span>{{ data.Players[1].Name }}</span><div><b>{{ data.Summary.RightWins }}</b><small>胜</small></div></div>
        </section>
        <div class="match-bar">
            <span v-for="(segment, index) in summarySegments" :key="index" :style="{flexGrow: segment.count, background: segment.color}"></span>
        </div>

        <div class="table-head">
            <span>曲目 / 谱面</span><span class="name-a">{{ data.Players[0].Name }}</span><span>胜负</span><span class="name-b">{{ data.Players[1].Name }}</span>
        </div>
        <section class="battle-list">
            <article v-for="(row, index) in data.Rows" :key="row.Id + ':' + row.LevelIndex" class="battle-row">
                <div class="song-cell">
                    <span class="row-number">{{ pageNumber((data.Page - 1) * data.PageSize + index + 1) }}</span>
                    <MaiCover :song-id="row.Id" :size="52" :frame-radius="9" :img-radius="6"/>
                    <div class="song-copy">
                        <div class="song-title" :class="{'has-han': /[\u3400-\u9fff]/.test(row.Title)}">{{ row.Title }}</div>
                        <div class="chart-meta"><b :style="{color: DIFF_COLORS[row.LevelIndex]}">{{ DIFF_NAMES[row.LevelIndex] }} {{ row.Level }}</b><span class="constant">{{ row.Constant.toFixed(1) }}</span></div>
                        <div class="song-id">{{ row.Type }} <span>·</span> ID {{ row.Id }}</div>
                    </div>
                </div>
                <div v-for="side in sides" :key="side" class="score-cell" :class="[side, {'is-win': row.Outcome === side}]" :style="{gridColumn: side === 'left' ? 2 : 4}">
                    <template v-if="scoreOf(row, side).State === 'played'">
                        <div class="achievement">{{ scoreOf(row, side).Achievement?.toFixed(4) }}<small>%</small></div>
                        <div class="score-badges">
                            <img :src="rankIcon(scoreOf(row, side).Rank)" class="rank" alt="">
                            <span class="completion">
                                <img v-if="scoreOf(row, side).Fc" :src="icon(scoreOf(row, side).Fc)" alt="">
                                <img v-if="scoreOf(row, side).Fs" :src="icon(scoreOf(row, side).Fs)" alt="">
                                <img v-if="starN(row, side)" :src="starIcon(row, side)" alt="">
                            </span>
                        </div>
                    </template>
                    <span v-else class="missing">未游玩</span>
                </div>
                <div class="outcome-cell" :class="row.Outcome">
                    <b>{{ outcomeMark(row.Outcome) }}</b>
                    <small>{{ outcomeLabel(row.Outcome) }}</small>
                </div>
            </article>
        </section>

        <footer class="batch-footer">
            <div><span class="footer-text">MARISA BOT · VERSUS</span><span class="range-label">第 {{ (data.Page - 1) * data.PageSize + 1 }}–{{ (data.Page - 1) * data.PageSize + data.Rows.length }} / {{ data.TotalCharts }} 谱面</span></div>
            <span v-if="data.Page < pageCount" class="page-help">发送 p{{ data.Page + 1 }} 查看下一页</span>
        </footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'
import {DIFF_COLORS, DIFF_NAMES, bgKeyOf, versionLogoSrc} from '@/components/maimai/utils/song_card'
import {dxScoreStar} from '@/components/maimai/utils/ordinal'

type Side = 'left' | 'right'
type Outcome = Side | 'draw' | 'unplayed'
interface Score { State: 'played' | 'unplayed'; Achievement: number | null; Rank: string | null; Fc: string; Fs: string; DxScore: number | null }
interface Row { Id: number; Title: string; Type: string; LevelIndex: number; Level: string; Constant: number; MaxDx: number; Left: Score; Right: Score; Outcome: Outcome }
interface BatchData { Scope: string; Version: string; SortLabel: string; Players: {Name: string}[]; Page: number; PageSize: number; TotalCharts: number; Summary: {LeftWins: number; RightWins: number; Draws: number; Unplayed: number}; Rows: Row[] }
const route = useRoute()
const data = ref<BatchData | null>(null)
const sides: Side[] = ['left', 'right']
const pageCount = computed(() => data.value ? Math.ceil(data.value.TotalCharts / data.value.PageSize) : 1)
const summarySegments = computed(() => {
    const summary = data.value?.Summary
    return [
        {count: summary?.LeftWins ?? 0, color: '#68dcf2'},
        {count: summary?.Draws ?? 0, color: '#d8c7f4'},
        {count: summary?.Unplayed ?? 0, color: '#342d41'},
        {count: summary?.RightWins ?? 0, color: '#eea0db'},
    ]
})
axios.get(context_get, {params: {id: route.query.id, name: 'versusBatch'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})
function pageNumber(n: number) { return n.toString().padStart(2, '0') }
function scoreOf(row: Row, side: Side) { return side === 'left' ? row.Left : row.Right }
function rankIcon(rank: string | null) { return '/assets/maimai/pic/rank_' + (rank ?? 'd').toLowerCase().replaceAll('+', 'p') + '.png' }
function icon(name: string) { return '/assets/maimai/pic/icon_' + name + '.png' }
function starN(row: Row, side: Side) { return dxScoreStar(scoreOf(row, side).DxScore ?? 0, row.MaxDx) }
function starIcon(row: Row, side: Side) { return '/assets/maimai/pic/music_icon_dxstar_' + starN(row, side) + '.png' }
function outcomeMark(outcome: Outcome) { return ({left: '←', right: '→', draw: '=', unplayed: '—'})[outcome] }
function outcomeLabel(outcome: Outcome) { return ({left: 'WIN', right: 'WIN', draw: '平局', unplayed: '未游玩'})[outcome] }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss">
.batch-header { display:flex; align-items:center; justify-content:space-between; }
.batch-heading { display:flex; align-items:center; gap:20px; }
.version-logo { width:112px; height:74px; object-fit:contain; }
.eyebrow { font:700 11px 'Torus',sans-serif; letter-spacing:.3em; color:#c4b9d3; }
h1 { font:900 36px 'Microsoft YaHei',sans-serif; margin:5px 0 0; letter-spacing:.08em; }
.page-badge { display:flex; align-items:baseline; gap:5px; padding:10px 17px; border:1px solid #ffffff2a; border-radius:14px; background:#ffffff08; font-family:'Torus',sans-serif; }
.page-badge strong { font-size:32px; }
.page-badge span { font-size:16px; color:#b5a6c9; }
.scope-line { display:flex; flex-wrap:wrap; align-items:center; gap:12px; margin:20px 0 18px; font:700 14px 'Microsoft YaHei',sans-serif; color:#d4cadd; }
.scope-chip { max-width:100%; overflow-wrap:anywhere; border:1px solid #d690ed77; border-radius:99px; padding:6px 16px; background:#ba42d932; color:#f1d2ff; }
.separator { color:#9986ad; }
.match-summary { display:grid; grid-template-columns:minmax(0,1fr) 160px minmax(0,1fr); align-items:center; padding:18px 30px 10px; border-radius:16px 16px 0 0; background:linear-gradient(90deg,#68dcf20b,transparent 45%,transparent 55%,#eea0db0b),#0a081a55; border:1px solid #ffffff20; border-bottom:0; }
.player-summary { min-width:0; text-align:center; font:700 19px 'Microsoft YaHei',sans-serif; }
.player-summary>span { display:block; overflow-wrap:anywhere; }
.player-summary b { font:900 44px 'Torus',sans-serif; }
.player-summary small { font:700 13px 'Microsoft YaHei',sans-serif; margin-left:8px; opacity:.7; }
.player-summary div { margin-top:3px; }
.player-a,.name-a { color:#95eafa; }
.player-b,.name-b { color:#f3b1e2; }
.summary-center { text-align:center; font:700 12px 'Microsoft YaHei',sans-serif; color:#bbaeCA; }
.summary-center>b { display:block; color:#f4eaff; font:900 27px 'Torus',sans-serif; letter-spacing:.13em; }
.summary-center>span { display:block; margin:3px 0 6px; font-size:11px; letter-spacing:.12em; }
.summary-center i { margin:0 5px; font-style:normal; color:#70617f; }
.summary-unplayed { margin-top:4px; }
.match-bar { display:flex; height:5px; overflow:hidden; border-radius:0 0 12px 12px; margin-bottom:20px; }
.match-bar span { flex-basis:0; }
.table-head,.battle-row { display:grid; grid-template-columns:minmax(0,1.65fr) minmax(0,1fr) 60px minmax(0,1fr); }
.table-head { padding:0 0 10px; font:700 12px 'Microsoft YaHei',sans-serif; color:#baa8cd; text-align:center; letter-spacing:.04em; }
.table-head>span { min-width:0; overflow-wrap:anywhere; }
.table-head>span:first-child { text-align:left; padding-left:14px; }
.battle-list { display:flex; flex-direction:column; gap:6px; }
.battle-row { align-items:stretch; min-height:88px; border:1px solid #ffffff12; border-radius:12px; background:#09051458; overflow:hidden; }
.battle-row:nth-child(even) { background:#ffffff05; }
.song-cell { display:flex; align-items:center; gap:11px; padding:10px 8px 10px 12px; min-width:0; }
.row-number { font:700 12px 'Torus',sans-serif; color:#b09abc; width:19px; flex-shrink:0; text-align:center; }
.song-copy { min-width:0; }
.song-title { font:900 15px/1.32 'SEGA NewRodin',sans-serif; overflow-wrap:anywhere; color:#f8f3fb; }
.song-title.has-han { font-family:'Microsoft YaHei',sans-serif; }
.chart-meta { display:flex; align-items:center; gap:8px; margin-top:4px; white-space:nowrap; }
.chart-meta>b { font:900 10px 'SEGA NewRodin',sans-serif; }
.constant { color:#ddcce9; font:700 12px 'Torus',sans-serif; }
.song-id { margin-top:3px; color:#aa94b9; font:700 10px 'Torus',sans-serif; letter-spacing:.05em; }
.song-id>span { margin:0 5px; color:#796786; }
.score-cell { display:flex; flex-direction:column; justify-content:center; align-items:center; gap:6px; grid-row:1; min-width:0; position:relative; }
.score-cell.left.is-win { background:linear-gradient(90deg,#68dcf200,#68dcf212); }
.score-cell.right.is-win { background:linear-gradient(90deg,#eea0db12,#eea0db00); }
.achievement { font:900 25px/1 'Torus',sans-serif; font-variant-numeric:tabular-nums; color:#e7dfed; }
.achievement small { font-size:12px; margin-left:2px; color:#ab99b9; }
.left.is-win .achievement { color:#95eafa; }
.right.is-win .achievement { color:#f3b1e2; }
.score-badges { display:flex; align-items:center; justify-content:center; gap:9px; height:20px; }
.rank { height:19px; width:auto; display:block; }
.completion { display:flex; align-items:center; gap:4px; }
.completion img { height:20px; width:auto; display:block; }
.missing { color:#a291b0; font:700 15px 'Microsoft YaHei',sans-serif; }
.outcome-cell { grid-column:3; grid-row:1; display:flex; flex-direction:column; justify-content:center; align-items:center; color:#ad99bb; gap:1px; }
.outcome-cell b { font:700 23px/1 'Torus','Microsoft YaHei',sans-serif; }
.outcome-cell small { font:700 9px 'Microsoft YaHei',sans-serif; white-space:nowrap; }
.outcome-cell.left { color:#95eafa; }
.outcome-cell.right { color:#f3b1e2; }
.batch-footer { display:flex; justify-content:space-between; align-items:end; margin-top:20px; }
.batch-footer>div { display:flex; flex-direction:column; gap:8px; }
.range-label { color:#b29bc2; font:700 11px 'Microsoft YaHei',sans-serif; }
.page-help { color:#bba7c9; font:700 12px 'Microsoft YaHei',sans-serif; }
</style>
