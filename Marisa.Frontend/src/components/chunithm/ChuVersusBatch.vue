<template>
    <MaiCardShell v-if="data" class="batch-card chu-batch chu-theme" :class="{'chu-ultima': themeIndex === 4}" :width="cardWidth" bg-key="DMY" :background="backgroundOf(themeIndex)" :accent="difficultyColor(themeIndex)" :style="{'--accent': difficultyColor(themeIndex)}">
        <header class="batch-header">
            <div class="batch-heading">
                <img :src="versionLogo(data.Version)" class="version-logo" alt="CHUNITHM" @error="fallbackLogo">
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
            <article v-for="(row, index) in data.Rows" :key="row.Id + ':' + row.ChartIndex" class="battle-row">
                <div class="song-cell">
                    <span class="row-number">{{ pageNumber((data.Page - 1) * data.PageSize + index + 1) }}</span>
                    <MaiCover game="chunithm" :song-id="row.Id" :size="52" :frame-radius="9" :img-radius="6"/>
                    <div class="song-copy">
                        <div class="song-title" :class="{'has-han': /[\u3400-\u9fff]/.test(row.Title)}">{{ row.Title }}</div>
                        <div class="chart-meta"><b :style="difficultyStyle(row.LevelIndex)">{{ row.LevelName }} {{ row.Level }}</b><span class="constant">{{ row.Constant.toFixed(1) }}</span></div>
                        <div class="song-id">ID {{ row.Id }}</div>
                    </div>
                </div>
                <div v-for="side in sides" :key="side" class="score-cell" :class="[side, {'is-win': row.Outcome === side}]" :style="{gridColumn: side === 'left' ? 2 : 4}">
                    <template v-if="scoreOf(row, side).State === 'played'">
                        <div class="achievement">{{ formatScore(scoreOf(row, side).Score) }}</div>
                        <div class="score-badges">
                            <img :src="rankIcon(scoreOf(row, side).Rank)" class="rank" alt="">
                        </div>
                    </template>
                    <span v-else class="missing">未游玩</span>
                    <VersusMarks :combo="scoreOf(row, side).FullCombo" :chain="scoreOf(row, side).FullChain" :clear="scoreOf(row, side).Clear"/>
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
import VersusMarks from '@/components/chunithm/partial/VersusMarks.vue'
import {backgroundOf, difficultyColor, difficultyStyle, versionLogo, fallbackLogo} from '@/components/chunithm/utils/song-card'
import {formatScore, hasClear, rankIcon, type ChuScore} from '@/components/chunithm/utils/versus'
type Side = 'left' | 'right'
type Outcome = Side | 'draw' | 'unplayed'
interface Row { Id: number; Title: string; LevelIndex: number; LevelName: string; ChartIndex: number; Level: string; Constant: number; Left: ChuScore; Right: ChuScore; Outcome: Outcome }
interface BatchData { Scope: string; Version: string; SortLabel: string; Players: {Name: string}[]; Page: number; PageSize: number; TotalCharts: number; Summary: {LeftWins: number; RightWins: number; Draws: number; Unplayed: number}; Rows: Row[] }
const route = useRoute()
const data = ref<BatchData | null>(null)
const cardWidth = computed(() => data.value?.Rows.some(row => hasClear(row.Left.Clear) || hasClear(row.Right.Clear)) ? 1224 : 960)
const sides: Side[] = ['left', 'right']
const themeIndex = computed(() => data.value?.Rows.length && data.value.Rows.every(row => row.LevelIndex === 4) ? 4 : 3)
const pageCount = computed(() => data.value ? Math.max(1, Math.ceil(data.value.TotalCharts / data.value.PageSize)) : 1)
const summarySegments = computed(() => {
    const summary = data.value?.Summary
    return [{count: summary?.LeftWins ?? 0, color: '#68dcf2'}, {count: summary?.Draws ?? 0, color: '#d8c7f4'}, {count: summary?.Unplayed ?? 0, color: '#342d41'}, {count: summary?.RightWins ?? 0, color: '#eea0db'}]
})
axios.get(context_get, {params: {id: route.query.id, name: 'chuVersusBatch'}}).then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })
function pageNumber(n: number) { return n.toString().padStart(2, '0') }
function scoreOf(row: Row, side: Side) { return side === 'left' ? row.Left : row.Right }
function outcomeMark(outcome: Outcome) { return ({left: '←', right: '→', draw: '=', unplayed: '—'})[outcome] }
function outcomeLabel(outcome: Outcome) { return ({left: 'WIN', right: 'WIN', draw: '平局', unplayed: '未游玩'})[outcome] }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/versus/batch.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/versus.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/theme.pcss"/>
