<template>
    <div class="tolerance-block mai-accent-rail" :style="blockStyle" :data-level-name="chart.LevelName">
        <ChartChip :chart="chart" :level-chars="levelChars" class="tolerance-chip"/>
        <img v-for="judgement in judgements" :key="judgement" :src="`/assets/chunithm/pic/tolerance_${judgement}.png`" :alt="judgement.toUpperCase()" class="tolerance-judgement" :data-judgement="judgement">
        <template v-for="row in rows" :key="row.rank">
            <img :src="rankIcon(row.rank)" :alt="row.rank === 'sss' ? 'SSS' : 'SSS+'" class="tolerance-rank" :data-tolerance-rank="row.rank">
            <b v-for="judgement in judgements" :key="judgement" class="tolerance-value" :class="{zero: !row.counts[judgement]}" :data-judgement="judgement">{{ row.counts[judgement] ? `−${row.counts[judgement]}` : 0 }}</b>
        </template>
    </div>
</template>

<script setup lang="ts">
import {computed} from 'vue'
import ChartChip from '@/components/chunithm/partial/ChartChip.vue'
import {chartDifficulty, type ChuSongBeatmap} from '@/components/chunithm/utils/song'
import {chartSurface} from '@/components/chunithm/utils/song-card'
import {rankIcon} from '@/components/chunithm/utils/score'
import {toleranceCounts, toleranceRanks, type ToleranceJudgement} from '@/components/chunithm/utils/tolerance'

const props = defineProps<{chart: ChuSongBeatmap; levelChars: number}>()
const judgements: ToleranceJudgement[] = ['justice', 'attack', 'miss']
const rows = computed(() => toleranceRanks.map(rank => ({rank, counts: toleranceCounts(props.chart.MaxCombo, rank)!})))
const blockStyle = computed(() => chartSurface(chartDifficulty(props.chart), 30))
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss">
.tolerance-block { --rail-width:4px; --rail-top:10px; --rail-bottom:10px; display:grid; grid-template-columns:132px repeat(3,minmax(0,1fr)); grid-template-rows:30px 40px 40px; align-items:center; justify-items:center; row-gap:2px; width:100%; padding:11px 12px 9px 20px; border:1px solid #ffffff17; border-radius:10px; }
.tolerance-chip { justify-self:start; --chip-size:12px; --name-width:6.7em; }
.tolerance-judgement { display:block; height:22px; width:auto; }
.tolerance-rank { --rank-height:38px; justify-self:start; display:block; height:var(--rank-height); width:auto; margin-left:calc(var(--rank-height) * var(--letters-left) / -226 + 2px); filter:drop-shadow(0 2px 4px #0006); }
/* 两张评级图同画布，三个 S 的起点横坐标不同；按 S 对齐。 */
.tolerance-rank[data-tolerance-rank="sss"] { --letters-left:66; }
.tolerance-rank[data-tolerance-rank="sssp"] { --letters-left:26; }
.tolerance-value { font:700 27px/1 'Torus',sans-serif; color:#fff; font-variant-numeric:tabular-nums; text-shadow:0 2px 4px #0000008c; }
.tolerance-value.zero { color:#ffffff59; }
</style>
