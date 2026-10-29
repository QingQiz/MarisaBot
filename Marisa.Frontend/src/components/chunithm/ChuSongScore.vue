<template>
    <MaiCardShell v-if="data" v-bind="cardTheme(themeIndex)" class="chu-score" :class="{'chu-we': hasWe, 'chu-we-only': themeIndex === 5}" :width="1000">
        <SongHeader :song="data.Song" :player="data.Nickname"/>

        <div class="song-section mt-8">
            <span class="section-tag">乐曲成绩</span>
            <span class="section-subtitle">SONG INFO</span>
            <div class="section-rule"></div>
        </div>

        <div class="score-table">
            <div class="score-head">
                <span></span><span>CONST</span><span>SCORE</span><span>RANK</span><span>RATING</span><span>OP</span><span>COMPLETION</span>
            </div>
            <div v-for="(chart, index) in data.Charts" :key="index" class="score-row mai-accent-rail"
                 :style="chartSurface(chart.LevelIndex, 30)" :data-level-name="chart.LevelName">
                <ChartChip :chart="{LevelName: chart.LevelName, LevelStr: chart.Level}" :level-chars="levelChars"/>
                <span v-if="chart.LevelIndex === 5" class="score-const"><span class="we-kanji">{{ chart.Level }}</span></span>
                <span v-else class="score-const">{{ chart.Constant?.toFixed(1) ?? '—' }}</span>
                <template v-if="chart.Score.State === 'played'">
                    <b class="score-value">{{ formatScore(chart.Score.Score) }}</b>
                    <span class="score-rank"><img :src="rankIcon(chart.Score.Rank)" alt=""></span>
                    <b class="score-rating" :class="{missing: chart.Score.Rating == null}">{{ formatRating(chart.Score.Rating) }}</b>
                    <span v-if="ops[index]" class="score-op"><b>{{ ops[index].op }}/{{ ops[index].max }}</b><small>{{ ops[index].rate }}%</small></span>
                    <span v-else class="score-op missing">—</span>
                    <span class="score-marks"><ScoreMarks family="common" direction="column" :combo="chart.Score.FullCombo" :chain="chart.Score.FullChain" :clear="chart.Score.Clear"/></span>
                </template>
                <span v-else class="score-unplayed">No Play Record</span>
            </div>
        </div>

        <footer class="mt-7"><span class="footer-text">MARISA BOT · SONG INFO</span></footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import ChartChip from '@/components/chunithm/partial/ChartChip.vue'
import ScoreMarks from '@/components/chunithm/partial/ScoreMarks.vue'
import SongHeader from '@/components/chunithm/partial/SongHeader.vue'
import {cardTheme, chartSurface, type ChuSongHeaderData} from '@/components/chunithm/utils/song-card'
import {formatRating, formatScore, rankIcon, type ChuScore} from '@/components/chunithm/utils/score'
import {calcMaxOverPower, calcOverPower} from '@/components/chunithm/utils/overpower'

interface ChartScore { LevelIndex: number; LevelName: string; Level: string; Constant: number | null; Score: ChuScore }
interface SongScoreData { Song: ChuSongHeaderData; Nickname: string; Charts: ChartScore[] }

const route = useRoute()
const data = ref<SongScoreData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'chuSongScore'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})

const hasWe = computed(() => data.value?.Charts.some(chart => chart.LevelIndex === 5) ?? false)
const themeIndex = computed(() => {
    const standard = data.value?.Charts.map(chart => chart.LevelIndex).filter(index => index < 5) ?? []
    return standard.length ? Math.max(...standard) : 5
})
const levelChars = computed(() => Math.max(1, ...(data.value?.Charts.filter(chart => chart.LevelIndex < 5).map(chart => chart.Level.length) ?? [])))
const ops = computed(() => data.value?.Charts.map(({Constant, Score}) => {
    if (Constant == null || Score.Score == null) return null
    const op = calcOverPower({ds: Constant, score: Score.Score, fc: Score.FullCombo})
    const max = calcMaxOverPower(Constant)
    return {op: op.toFixed(2), max: max.toFixed(2), rate: (op / max * 100).toFixed(2)}
}) ?? [])
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/section.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/theme.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/we-theme.pcss"/>
<style scoped lang="postcss">
.score-table { --columns:168px 64px 1fr 88px 80px 140px 130px;display:flex; flex-direction:column; gap:8px; margin-top:16px; }
.score-head { display:grid; grid-template-columns:var(--columns); column-gap:6px; align-items:center; padding:0 16px 0 20px; font:700 12px/1 'Torus',sans-serif; letter-spacing:.14em; color:#ffffff80; text-align:center; }
.score-row { --rail-width:4px; --rail-top:10px; --rail-bottom:10px; display:grid; grid-template-columns:var(--columns); column-gap:6px; align-items:center; height:60px; padding:0 16px 0 20px; border:1px solid #ffffff17; border-radius:10px; }
.score-row > * { justify-self:center; }
.score-row > .chart-chip { justify-self:start; --chip-size:13px; }
.score-const { font:700 22px/1 'Torus',sans-serif; color:#ffffffd9; font-variant-numeric:tabular-nums; }
.we-kanji { display:inline-grid; place-items:center; width:34px; height:34px; border:2px solid transparent; border-radius:9px; background:linear-gradient(135deg,#252937,#12151d 72%) padding-box,var(--we-spectrum) border-box; color:#fff; font:700 19px/1 'Microsoft YaHei',sans-serif; }
.score-value { font:700 28px/1 'Torus',sans-serif; color:#fff; font-variant-numeric:tabular-nums; text-shadow:0 2px 4px #0000008c; }
.score-rank img { display:block; height:34px; width:auto; }
.score-rating { font:700 22px/1 'Torus',sans-serif; color:#fff; font-variant-numeric:tabular-nums; }
.score-rating.missing, .score-op.missing { color:#ffffff59; }
.score-op { display:flex; flex-direction:column; align-items:center; gap:5px; font:800 19px/1 'Torus',sans-serif; color:#ffffffe6; font-variant-numeric:tabular-nums; }
.score-op small { font:700 14px/1 'Torus',sans-serif; color:#ffffff8c; }
.score-row > .score-marks { justify-self:stretch; --chu-mark-width:76px; }
.score-row > .score-unplayed { grid-column:3 / -1; justify-self:start; padding-left:28px; font:700 19px 'SEGA NewRodin',sans-serif; letter-spacing:.22em; color:#ffffff4d; }
</style>
