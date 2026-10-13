<template>
    <MaiCardShell v-if="data" class="multi-batch" :width="width" :bg-key="bgKeyOf(3, false)" :accent="accent"
                  :style="{'--accent': accent, '--players': data.Players.length}">
        <header class="batch-header">
            <div class="batch-heading">
                <img :src="versionLogoSrc(data.Version)" class="version-logo" alt="">
                <div><div class="eyebrow">MARISA BOT · VERSUS</div><h1>多人对战</h1></div>
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
            <span class="song-label">曲目 / 谱面</span>
            <div v-for="(player, index) in data.Players" :key="index" class="player-head" :class="{champion: player.Place === 1}"
                 :style="{'--place-color': placeColor(player.Place)}">
                <span class="player-place"><b>{{ player.Place }}</b>{{ placeSuffix(player.Place) }}</span>
                <span class="player-name">{{ player.Name }}</span>
                <span class="player-points"><b>{{ formatPoints(player.Points) }}</b><i>/</i>{{ player.Matchups }}</span>
            </div>
        </div>

        <section class="battle-list">
            <article v-for="(row, index) in data.Rows" :key="row.Song.Id + ':' + row.LevelIndex" class="grid-row battle-row">
                <div class="song-cell">
                    <span class="row-number">{{ pad((data.Page - 1) * data.PageSize + index + 1) }}</span>
                    <MaiCover :song-id="row.Song.Id" :size="52" :frame-radius="9" :img-radius="6"/>
                    <div class="song-copy">
                        <div class="song-title" :class="{'has-han': /[㐀-鿿]/.test(row.Song.Title)}">{{ row.Song.Title }}</div>
                        <div class="chart-meta"><b :style="{color: DIFF_COLORS[row.LevelIndex]}">{{ DIFF_NAMES[row.LevelIndex] }} {{ row.Level }}</b><span class="constant">{{ row.Constant.toFixed(1) }}</span></div>
                    </div>
                </div>
                <div v-for="(cell, column) in row.Cells" :key="column" class="score-cell" :class="{best: cell.Place === 1 && playedCount(row) > 1}">
                    <template v-if="cell.Score.State === 'played'">
                        <div class="achievement">{{ cell.Score.Achievement!.toFixed(4) }}<small>%</small></div>
                        <div class="score-badges">
                            <img :src="rankIcon(cell.Score.Rank)" class="rank" alt="">
                            <img v-if="cell.Score.Fc" :src="markIcon(cell.Score.Fc)" class="mark" alt="">
                            <img v-if="cell.Score.Fs" :src="markIcon(cell.Score.Fs)" class="mark" alt="">
                        </div>
                    </template>
                    <span v-else class="missing">—</span>
                </div>
            </article>
        </section>

        <MaiVersusStandings v-if="data.Standings.length" :standings="data.Standings" :rounds="data.Round" :accent="accent"/>

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
import MaiVersusStandings from '@/components/versus/VersusStandings.vue'
import {DIFF_COLORS, DIFF_NAMES, bgKeyOf, versionLogoSrc} from '@/components/maimai/utils/song_card'
import {formatPoints, markIcon, placeColor, placeSuffix, rankIcon, type VersusChart, type VersusStanding} from '@/components/maimai/utils/versus'

interface PlayerView { Name: string; Place: number; Points: number; Matchups: number }
interface PageData { Title: string; Version: string; Round: number; Players: PlayerView[]; Page: number; PageSize: number; TotalCharts: number; Rows: VersusChart[]; Standings: VersusStanding[] }

const route = useRoute()
const data = ref<PageData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'versusMultiBatch'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})
const accent = DIFF_COLORS[3]
const pageCount = computed(() => data.value ? Math.max(1, Math.ceil(data.value.TotalCharts / data.value.PageSize)) : 1)
const width = computed(() => Math.max(960, 96 + 340 + (data.value?.Players.length ?? 2) * 136))
function pad(n: number) { return n.toString().padStart(2, '0') }
function playedCount(row: VersusChart) { return row.Cells.filter(c => c.Score.State === 'played').length }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/versus/multi-batch.pcss"/>
