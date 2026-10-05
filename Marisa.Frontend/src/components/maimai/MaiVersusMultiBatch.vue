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
import MaiVersusStandings from '@/components/maimai/MaiVersusStandings.vue'
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
<style scoped lang="postcss">
.batch-header { display:flex; align-items:center; justify-content:space-between; }
.batch-heading { display:flex; align-items:center; gap:20px; }
.version-logo { width:112px; height:74px; object-fit:contain; }
.eyebrow { font:700 11px 'Torus',sans-serif; letter-spacing:.3em; color:#c4b9d3; }
h1 { font:900 36px 'Microsoft YaHei',sans-serif; margin:5px 0 0; letter-spacing:.08em; }
.badges { display:flex; gap:10px; }
.badge { display:flex; align-items:baseline; gap:6px; padding:10px 17px; border:1px solid #ffffff2a; border-radius:14px; background:#ffffff08; font-family:'Torus',sans-serif; }
.badge strong { font-size:32px; }
.badge span { font-size:13px; letter-spacing:.18em; color:#b5a6c9; }
.scope-line { display:flex; flex-wrap:wrap; align-items:center; gap:12px; margin:20px 0 18px; font:700 14px 'Microsoft YaHei',sans-serif; color:#d4cadd; }
.scope-chip { max-width:100%; overflow-wrap:anywhere; border:1px solid #d690ed77; border-radius:99px; padding:6px 16px; background:#ba42d932; color:#f1d2ff; }
.separator { color:#9986ad; }

.grid-row { display:grid; grid-template-columns:minmax(0,1fr) repeat(var(--players), 128px); column-gap:8px; }
.table-head { align-items:end; margin-bottom:10px; }
.song-label { padding:0 0 8px 14px; font:700 12px 'Microsoft YaHei',sans-serif; color:#baa8cd; letter-spacing:.04em; }
.player-head {
    min-width:0;
    display:flex;
    flex-direction:column;
    align-items:center;
    gap:3px;
    padding:10px 8px 9px;
    border:1px solid rgba(255,255,255,.14);
    border-top:3px solid var(--place-color);
    border-radius:12px;
    background:rgba(10,8,26,.5);
}
.player-head.champion { border-color:rgba(255,227,140,.65); border-top-color:var(--place-color); background:linear-gradient(180deg,rgba(255,215,94,.2),rgba(10,8,26,.5)); box-shadow:0 0 18px rgba(255,215,94,.2); }
.player-place { font:700 11px 'Torus',sans-serif; letter-spacing:.06em; color:var(--place-color); }
.player-place b { font-size:22px; font-weight:900; margin-right:1px; }
.player-name { max-width:100%; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font:700 15px 'Microsoft YaHei',sans-serif; }
.player-points { font:700 12px 'Torus',sans-serif; color:rgba(255,255,255,.55); font-variant-numeric:tabular-nums; }
.player-points b { font-size:17px; color:#fff; }
.player-points i { margin:0 2px; font-style:normal; color:rgba(255,255,255,.3); }

.battle-list { display:flex; flex-direction:column; gap:6px; }
.battle-row { align-items:stretch; min-height:76px; border:1px solid #ffffff12; border-radius:12px; background:#09051458; overflow:hidden; }
.battle-row:nth-child(even) { background:#ffffff05; }
.song-cell { display:flex; align-items:center; gap:11px; padding:10px 8px 10px 12px; min-width:0; }
.row-number { font:700 12px 'Torus',sans-serif; color:#b09abc; width:19px; flex-shrink:0; text-align:center; }
.song-copy { min-width:0; }
.song-title { font:900 15px/1.32 'SEGA NewRodin',sans-serif; overflow-wrap:anywhere; color:#f8f3fb; }
.song-title.has-han { font-family:'Microsoft YaHei',sans-serif; }
.chart-meta { display:flex; align-items:center; gap:8px; margin-top:4px; white-space:nowrap; }
.chart-meta>b { font:900 10px 'SEGA NewRodin',sans-serif; }
.constant { color:#ddcce9; font:700 12px 'Torus',sans-serif; }
.score-cell { display:flex; flex-direction:column; justify-content:center; align-items:center; gap:5px; min-width:0; }
.score-cell.best { background:linear-gradient(180deg,rgba(255,215,94,.06),rgba(255,215,94,.16)); box-shadow:inset 0 -2px 0 rgba(255,215,94,.75); }
.achievement { font:900 19px/1 'Torus',sans-serif; font-variant-numeric:tabular-nums; color:#e7dfed; }
.best .achievement { color:#ffe27a; }
.achievement small { font-size:11px; margin-left:1px; color:#ab99b9; }
.score-badges { display:flex; align-items:center; justify-content:center; gap:4px; height:18px; }
.rank { height:16px; width:auto; display:block; }
.mark { height:18px; width:auto; display:block; }
.missing { color:rgba(255,255,255,.25); font:400 22px 'Torus',sans-serif; }
.batch-footer { display:flex; justify-content:space-between; align-items:end; margin-top:20px; }
.batch-footer>div { display:flex; flex-direction:column; gap:8px; }
.range-label { color:#b29bc2; font:700 11px 'Microsoft YaHei',sans-serif; }
.hint { color:#bba7c9; font:700 12px 'Microsoft YaHei',sans-serif; }
</style>
