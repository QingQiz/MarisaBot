<template>
    <MaiCardShell v-if="song" class="chu-song chu-theme" :class="{'chu-ultima': themeIndex === 4, 'chu-we': hasWe, 'chu-we-only': themeIndex === 5}"
                  :width="1240" pad-bottom="pb-8" bg-key="DMY" :background="background" :accent="accent"
                  :style="{'--accent': accent}">
        <SongHeader :song="heading" large-cover>
            <template #artwork>
                <section v-if="toleranceCharts.length" class="tolerance-list">
                    <div class="song-section">
                        <span class="section-tag">容错率</span>
                        <span class="section-subtitle">TOLERANCE</span>
                        <div class="section-rule"></div>
                    </div>
                    <SongTolerance v-for="chart in toleranceCharts" :key="chart.LevelName" :chart="chart" :level-chars="levelChars"/>
                </section>
            </template>

            <div class="song-section">
                <span class="section-tag">谱面信息</span>
                <span class="section-subtitle">CHART DATA</span>
                <div class="section-rule"></div>
                <span class="chart-count">{{ charts.length }} {{ charts.length === 1 ? 'CHART' : 'CHARTS' }}</span>
            </div>

            <section ref="chartList" class="chart-list">
                <article v-for="(chart, index) in charts" :key="chart.LevelName + ':' + chart.ChartName + ':' + index"
                         class="chart-row mai-accent-rail" :class="{'we-chart': isWorldsEnd(chart)}" :style="rowStyle(chart)"
                         :data-level-name="chart.LevelName">
                    <div class="chart-top">
                        <ChartChip :chart="chart" :level-chars="levelChars"/>
                        <div class="chart-charter-box">
                            <div class="chart-charter" :class="{missing: !chart.Charter?.trim()}">{{ chart.Charter?.trim() || '—' }}</div>
                        </div>
                    </div>
                    <div class="chart-body">
                        <div class="chart-stats">
                            <div class="chart-stat">
                                <div class="stat-label">MAX COMBO</div>
                                <div class="stat-value chart-combo">{{ chartCombo(chart) }}</div>
                            </div>
                            <div class="stat-rule"></div>
                            <div class="chart-stat">
                                <div class="stat-label">BPM</div>
                                <div class="stat-value chart-bpm">{{ bpms[index].main }}<small v-if="bpms[index].range" class="bpm-range">{{ bpms[index].range }}</small></div>
                            </div>
                        </div>
                    </div>
                    <div v-if="isWorldsEnd(chart)" class="chart-badge we-badge">{{ chart.LevelStr || '—' }}</div>
                    <div v-else class="chart-badge" :class="{unavailable: chartConstant(chart) === '—'}">{{ chartConstant(chart) }}</div>
                </article>
            </section>
        </SongHeader>

        <footer class="song-footer">
            <span class="footer-text">MARISA BOT · CHUNITHM</span>
        </footer>
    </MaiCardShell>
    <div v-else class="song-loading">{{ failed ? '歌曲数据未能加载' : '正在加载歌曲信息…' }}</div>
</template>

<script setup lang="ts">
import {computed, nextTick, ref, watch} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import ChartChip from '@/components/chunithm/partial/ChartChip.vue'
import SongHeader from '@/components/chunithm/partial/SongHeader.vue'
import SongTolerance from '@/components/chunithm/partial/SongTolerance.vue'
import {backgroundOf, chartSurface, difficultyColor} from '@/components/chunithm/utils/song-card'
import {chartBpm, chartCombo, chartConstant, chartDifficulty, isWorldsEnd, summaryBpm, type ChuSongBeatmap, type ChuSongData} from '@/components/chunithm/utils/song'
import {toleranceCounts} from '@/components/chunithm/utils/tolerance'

const route = useRoute()
const song = ref<ChuSongData | null>(null)
const failed = ref(false)
axios.get(context_get, {params: {id: route.query.id, name: 'SongData'}}).then(response => {
    song.value = response.data
}).catch(() => { failed.value = true })

const charts = computed(() => (song.value?.Beatmaps ?? []).filter((chart): chart is ChuSongBeatmap => chart != null))
const hasWe = computed(() => charts.value.some(isWorldsEnd))
const themeIndex = computed(() => {
    const standard = charts.value.map(chartDifficulty).filter(index => index >= 0 && index < 5)
    return standard.length ? Math.max(...standard) : hasWe.value ? 5 : 3
})
const accent = computed(() => difficultyColor(themeIndex.value))
const background = computed(() => backgroundOf(themeIndex.value))
const toleranceCharts = computed(() => {
    const wanted = themeIndex.value === 4 ? [3, 4] : themeIndex.value < 4 ? [themeIndex.value] : []
    return wanted.map(index => charts.value.find(chart => chartDifficulty(chart) === index))
        .filter((chart): chart is ChuSongBeatmap => chart != null && toleranceCounts(chart.MaxCombo, 'sss') != null)
})
const bpms = computed(() => charts.value.map(chartBpm))
const levelChars = computed(() => Math.max(1, ...charts.value.filter(chart => !isWorldsEnd(chart)).map(chart => (chart.LevelStr || '—').length)))
const heading = computed(() => ({...song.value!, Bpm: summaryBpm(charts.value)}))

function rowStyle(chart: ChuSongBeatmap) {
    const index = chartDifficulty(chart)
    return {...chartSurface(index, 34), '--badge-color': index === 4 ? '#ff3a5f' : difficultyColor(index)}
}

const chartList = ref<HTMLElement | null>(null)
watch(song, fitCharters, {flush: 'post'})

async function fitCharters() {
    await nextTick()
    try { await document.fonts.ready } catch { /* 测量兜底 */ }
    chartList.value?.querySelectorAll<HTMLElement>('.chart-charter').forEach(element => {
        const apply = (size: number, wrap: boolean) => {
            element.classList.toggle('wrapped', wrap)
            element.style.fontSize = `${size}px`
            return element.scrollHeight
        }
        let size = 22
        apply(size, false)
        while (element.scrollWidth > element.clientWidth && size > 15) apply(--size, false)
        if (element.scrollWidth <= element.clientWidth) return
        const height = element.parentElement!.clientHeight
        for (size = 17; size > 12; size--) if (apply(size, true) <= height + 1) return
        apply(12, true)
    })
}
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/song.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/theme.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/we-theme.pcss"/>
