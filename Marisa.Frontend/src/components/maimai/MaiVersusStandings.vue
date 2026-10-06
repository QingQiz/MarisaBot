<template>
    <section class="standings" :style="{'--accent': accent}">
        <header class="standings-head">
            <span class="standings-title">累计战绩<small>{{ rounds }} 轮</small></span>
            <span class="col-label rate-label">对位胜率</span>
            <span class="col-label">对位</span>
            <span class="col-label">参与</span>
        </header>
        <div v-for="(row, index) in standings" :key="index" class="standing" :class="{left: row.Left}">
            <span class="standing-place" :style="{color: row.Left ? undefined : placeColor(placeOf(row))}">{{ placeOf(row) }}</span>
            <span class="standing-name"><span>{{ row.Name }}</span><small v-if="row.Left">已退出</small></span>
            <div class="rate-track"><div class="rate-fill" :style="{width: percent(row) + '%'}"></div></div>
            <b class="rate">{{ row.Rate == null ? '—' : percent(row) }}<small v-if="row.Rate != null">%</small></b>
            <span class="record">{{ formatPoints(row.Wins) }}<i>/</i>{{ row.Matchups }}</span>
            <span class="rounds">{{ row.Rounds }}<i>/</i>{{ rounds }}</span>
        </div>
    </section>
</template>

<script setup lang="ts">
import {formatPoints, placeColor, type VersusStanding} from '@/components/maimai/utils/versus'

const props = defineProps<{standings: VersusStanding[]; rounds: number; accent: string}>()

function percent(row: VersusStanding) { return Math.round((row.Rate ?? 0) * 100) }
function placeOf(row: VersusStanding) { return 1 + props.standings.filter(x => (x.Rate ?? -1) > (row.Rate ?? -1)).length }
</script>

<style scoped lang="postcss">
.standings { margin-top:22px; padding:16px 22px 12px; border:1px solid rgba(255,255,255,.14); border-radius:16px; background:linear-gradient(180deg,rgba(10,8,26,.62),rgba(10,8,26,.36)); }
.standings-head,.standing { display:grid; grid-template-columns:34px minmax(0,1fr) 150px 66px 64px 50px; align-items:center; column-gap:12px; }
.standings-head { padding-bottom:9px; margin-bottom:4px; border-bottom:1px solid rgba(255,255,255,.12); }
.standings-title { grid-column:1 / 3; display:flex; align-items:baseline; gap:10px; font:700 17px 'Microsoft YaHei',sans-serif; letter-spacing:.12em; }
.standings-title small { font-size:12px; letter-spacing:.04em; color:rgba(255,255,255,.45); }
.col-label { font:700 11px 'Microsoft YaHei',sans-serif; color:rgba(255,255,255,.42); text-align:right; white-space:nowrap; }
.rate-label { grid-column:3 / 5; }
.standing { min-height:38px; }
.standing + .standing { border-top:1px solid rgba(255,255,255,.06); }
.standing-place { font:900 19px 'Torus',sans-serif; text-align:center; color:rgba(255,255,255,.5); }
.standing-name { min-width:0; display:flex; align-items:center; gap:8px; font:700 16px 'Microsoft YaHei',sans-serif; }
.standing-name>span { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.standing-name small { flex-shrink:0; padding:1px 7px; border-radius:99px; background:rgba(255,255,255,.1); font-size:11px; color:rgba(255,255,255,.55); }
.rate-track { height:6px; border-radius:99px; background:rgba(255,255,255,.1); overflow:hidden; }
.rate-fill { height:100%; border-radius:99px; background:linear-gradient(90deg,color-mix(in srgb,var(--accent) 55%,transparent),var(--accent)); box-shadow:0 0 8px color-mix(in srgb,var(--accent) 50%,transparent); }
.rate { text-align:right; font:900 21px 'Torus',sans-serif; font-variant-numeric:tabular-nums; }
.rate small { margin-left:1px; font-size:12px; opacity:.6; }
.record,.rounds { text-align:right; font:700 14px 'Torus',sans-serif; font-variant-numeric:tabular-nums; color:rgba(255,255,255,.72); }
.record i,.rounds i { margin:0 2px; font-style:normal; color:rgba(255,255,255,.35); }
.standing.left { opacity:.45; }
</style>
