<template>
    <MaiCardShell v-if="data" v-bind="cardTheme(themeIndex)" class="multi-batch chu-multi-batch" :width="width"
                  :style="{'--players': data.Players.length, '--player-column': playerColumnWidth + 'px'}">
        <header class="batch-header">
            <div class="batch-heading">
                <img :src="versionLogo(data.Version)" class="version-logo" alt="CHUNITHM" @error="fallbackLogo">
                <div><div class="eyebrow">MARISA BOT · VERSUS</div><h1>多人批量对战</h1></div>
            </div>
            <div class="badges">
                <div v-if="data.Round" class="badge"><span>ROUND</span><strong>{{ pad(data.Round) }}</strong></div>
                <div v-if="pageCount > 1" class="badge"><strong>{{ pad(data.Page) }}</strong><span>/ {{ pad(pageCount) }}</span></div>
            </div>
        </header>

        <div class="scope-line">
            <span class="scope-chip">{{ data.Title }}</span>
            <span>{{ data.TotalCharts }} 谱面</span>
            <span class="separator">·</span>
            <span>{{ data.Players.length }} 人</span>
        </div>

        <div class="grid-row table-head">
            <div class="song-label"><span>曲目 / 谱面</span><small>本页 {{ data.Rows.length }} 谱面</small></div>
            <div v-for="(player, index) in data.Players" :key="index" class="player-head" :class="{champion: player.Place === 1 && player.Matchups > 0}"
                 :style="{'--place-color': placeColor(player.Matchups > 0 ? player.Place : null)}">
                <span class="player-name" :title="player.Name">{{ player.Name }}</span>
                <span class="player-place"><template v-if="player.Matchups > 0"><b>{{ player.Place }}</b>{{ placeSuffix(player.Place) }}</template><b v-else>—</b></span>
                <span class="player-points"><b>{{ formatPoints(player.Points) }}</b><i>/</i>{{ player.Matchups }}</span>
            </div>
        </div>

        <section class="battle-list">
            <article v-for="(row, index) in data.Rows" :key="row.Song.Id + ':' + row.ChartIndex" class="grid-row battle-row">
                <div class="song-cell">
                    <span class="row-number">{{ pad((data.Page - 1) * data.PageSize + index + 1) }}</span>
                    <MaiCover game="chunithm" :song-id="row.Song.Id" :size="52" :frame-radius="9" :img-radius="6"/>
                    <div class="song-copy">
                        <div class="song-title" :class="{'has-han': /[㐀-鿿]/.test(row.Song.Title)}" :title="row.Song.Title">{{ row.Song.Title }}</div>
                        <div class="chart-meta"><b :style="difficultyStyle(row.LevelIndex)">{{ row.LevelName }} {{ row.Level }}</b><span class="constant">{{ row.Constant.toFixed(1) }}</span></div>
                    </div>
                </div>
                <div v-for="(cell, column) in row.Cells" :key="column" class="score-cell" :class="{best: cell.Score.State === 'played' && cell.Place === 1 && playedCount(row) > 1, unplayed: cell.Score.State !== 'played'}">
                    <template v-if="cell.Score.State === 'played'">
                        <div class="achievement">{{ formatScore(cell.Score.Score) }}</div>
                        <div class="score-badges">
                            <img v-if="cell.Score.Rank" :src="rankIcon(cell.Score.Rank)" class="rank" :alt="cell.Score.Rank.toUpperCase()">
                        </div>
                        <div class="completion-marks">
                            <ScoreMarks family="common" :combo="cell.Score.FullCombo" :chain="cell.Score.FullChain" :clear="cell.Score.Clear"/>
                        </div>
                    </template>
                    <span v-else class="missing">—</span>
                </div>
            </article>
        </section>

        <VersusStandings v-if="data.Standings.length" :standings="data.Standings" :rounds="data.Round" :accent="accent" unranked-empty/>

        <footer class="batch-footer">
            <div><span class="footer-text">MARISA BOT · VERSUS</span><span class="range-label">第 {{ (data.Page - 1) * data.PageSize + 1 }}–{{ (data.Page - 1) * data.PageSize + data.Rows.length }} / {{ data.TotalCharts }} 谱面 · 分数为本轮对位胜场</span></div>
            <span v-if="data.Page < pageCount" class="hint">发送 p{{ data.Page + 1 }} 查看下一页</span>
            <span v-else-if="data.Round" class="hint">发送「开始」进行下一轮</span>
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
import ScoreMarks from '@/components/chunithm/partial/ScoreMarks.vue'
import VersusStandings from '@/components/versus/VersusStandings.vue'
import {cardTheme, difficultyColor, difficultyStyle, versionLogo, fallbackLogo} from '@/components/chunithm/utils/song-card'
import {formatScore, rankIcon} from '@/components/chunithm/utils/score'
import {formatPoints, placeColor, placeSuffix, type ChuChart, type VersusStanding} from '@/components/chunithm/utils/versus'
interface PlayerView { Name: string; Place: number; Points: number; Matchups: number }
interface PageData { Title: string; Version: string; Round: number; Players: PlayerView[]; Page: number; PageSize: number; TotalCharts: number; Rows: ChuChart[]; Standings: VersusStanding[] }
const route = useRoute()
const data = ref<PageData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'chuVersusMultiBatch'}}).then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })
const themeIndex = computed(() => data.value?.Rows.length && data.value.Rows.every(row => row.LevelIndex === 4) ? 4 : 3)
const accent = computed(() => difficultyColor(themeIndex.value))
const pageCount = computed(() => data.value ? Math.max(1, Math.ceil(data.value.TotalCharts / data.value.PageSize)) : 1)
const playerColumnWidth = 228
const width = computed(() => Math.max(960, 96 + 2 + 340 + (data.value?.Players.length ?? 2) * playerColumnWidth))
function pad(n: number) { return n.toString().padStart(2, '0') }
function playedCount(row: ChuChart) { return row.Cells.filter(c => c.Score.State === 'played').length }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/versus/multi-batch.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/versus.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/theme.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/versus-multi-batch.pcss"/>
