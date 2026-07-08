<template>
    <MaiCardShell v-if="rows.length" class="mai-curve-rank" :bg-key="bgKey" :accent="accent" :width="cardWidth" pad-bottom="pb-8">
            <!-- ── header ── -->
            <div class="flex items-baseline gap-4">
                <h1 class="rank-title">{{ heading }}</h1>
                <span class="font-rodin text-[16px] tracking-[0.2em] text-white/70 whitespace-nowrap">DIFFICULTY RANKING</span>
                <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
                <span class="count-chip font-rodin shrink-0">{{ rows.length }} CHARTS</span>
            </div>

            <!-- ── ranking columns（列内从上到下，列间接续） ── -->
            <div class="flex gap-6 mt-6 items-start">
                <div v-for="(col, ci) in columns" :key="ci" class="flex-1 min-w-0 flex flex-col gap-[6px]">
                    <div class="rank-head">
                        <span class="rh-rank">#</span>
                        <span class="rh-song">曲目</span>
                        <span class="rh-diff">难度</span>
                        <span class="rh-ds">定数</span>
                        <span class="rh-val">{{ valueHead }}</span>
                    </div>
                    <div v-for="r in col" :key="r.songId + '-' + r.li" class="rank-row">
                        <span class="rr-rank tabular-nums">#{{ r.rank }}</span>
                        <img :src="coverSrcOf(r.songId)" @error="onCoverErr" alt="" class="rr-cover">
                        <span class="rr-title">{{ r.title }}</span>
                        <span v-if="r.type === 'DX'" class="rr-dx font-rodin">DX</span>
                        <span class="rr-diff font-rodin" :style="{ color: DIFF_COLORS[Math.min(r.li, 4)] }">{{ DIFF_ABBR[Math.min(r.li, 4)] }}</span>
                        <span class="rr-ds tabular-nums">{{ r.ds.toFixed(1) }}</span>
                        <span class="rr-val tabular-nums" :style="isDsKind ? { color: accent } : undefined">{{ r.value }}</span>
                    </div>
                </div>
            </div>

            <footer class="mt-5 flex items-baseline justify-between gap-6">
                <div class="foot-note min-w-0 whitespace-nowrap">数据来源：水鱼查分器 · {{ legend }}</div>
                <span class="footer-text shrink-0">MARISA BOT · DIFFICULTY RANKING</span>
            </footer>
    </MaiCardShell>

    <div v-else-if="loaded" class="mai-curve-rank mai-card w-[840px] px-12 py-10 antialiased">
        <div class="text-[26px] font-bold">该范围内暂无数据</div>
        <div class="mt-3 text-[16px] text-white/60">数据覆盖 Lv11 及以上的常规谱面，宴会场谱面与收录后新增的曲目暂不支持。</div>
    </div>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {DIFF_COLORS, coverSrcOf, COVER_FALLBACK, bgKeyOf} from '@/components/maimai/utils/song_card'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'

interface RankChart {
    li: number; ds: number; kind: string; pooled: number | null
    band_pct: number | null; n: number
    score_rank: { rank: number; of: number } | null
}
interface RankSong { title: string; type: string; charts: RankChart[] }

const DIFF_ABBR = ['BSC', 'ADV', 'EXP', 'MAS', 'ReM']

const route = useRoute()
const db = ref<Record<string, RankSong> | null>(null)
const loaded = ref(false)

axios.get('/assets/maimai/difficulty_curves.json')
    .then(res => { db.value = res.data })
    .finally(() => { loaded.value = true })

// 查询模式：?level=13+（官方等级，x.0-.5 = N / x.6-.9 = N+）或 ?ds=14.7（同定数组）
const levelQ = computed(() => typeof route.query.level === 'string' ? route.query.level : null)
const dsQ = computed(() => typeof route.query.ds === 'string' ? Number(route.query.ds) : null)

function inScope(ds: number): boolean {
    const key = Math.round(ds * 10)
    if (dsQ.value != null) return key === Math.round(dsQ.value * 10)
    if (!levelQ.value) return false
    const n = parseInt(levelQ.value)
    return levelQ.value.endsWith('+') ? key >= n * 10 + 6 && key <= n * 10 + 9
                                      : key >= n * 10 && key <= n * 10 + 5
}

interface Row {
    songId: number; title: string; type: string; li: number; ds: number
    pooled: number | null; band_pct: number | null; scoreRank: number
    rank: number; value: string
}

const rows = computed<Row[]>(() => {
    if (!db.value) return []
    const list: Omit<Row, 'rank' | 'value'>[] = []
    for (const [id, song] of Object.entries(db.value)) {
        for (const c of song.charts) {
            if (!inScope(c.ds)) continue
            list.push({songId: Number(id), title: song.title, type: song.type, li: c.li, ds: c.ds,
                       pooled: c.pooled, band_pct: c.band_pct, scoreRank: c.score_rank?.rank ?? 1e9})
        }
    }
    // 排序：定数模式沿用单曲卡的同定数难度排名（与卡上 #X/Y 一致）；等级模式按综合拟合定数
    // 降序（≥13.6 全 fitted），低等级带按带内难度百分位降序（口径与单曲卡切换一致）
    const fitted = list.length > 0 && list.every(r => r.pooled != null)
    if (dsQ.value != null && list.every(r => r.scoreRank < 1e9)) {
        list.sort((a, b) => a.scoreRank - b.scoreRank)
    } else if (fitted) {
        list.sort((a, b) => (b.ds + b.pooled!) - (a.ds + a.pooled!) || b.ds - a.ds)
    } else {
        list.sort((a, b) => (b.band_pct ?? -1) - (a.band_pct ?? -1))
    }
    return list.map((r, i) => ({
        ...r, rank: i + 1,
        value: fitted ? (r.ds + r.pooled!).toFixed(2) : r.band_pct != null ? r.band_pct.toFixed(1) : '—',
    }))
})

const isDsKind = computed(() => rows.value.length > 0 && rows.value.every(r => r.pooled != null))

const heading = computed(() => dsQ.value != null
    ? `定数 ${dsQ.value.toFixed(1)} 拟合难度排名`
    : `等级 ${levelQ.value} 拟合难度排名`)
const legend = computed(() => isDsKind.value
    ? '排序依据：综合拟合定数'
    : '排序依据：同等级难度百分位')
const valueHead = computed(() => isDsKind.value ? '拟合定数' : '百分位')

// 列数按体量：>240 三列、>80 两列，卡宽随列数
const colCount = computed(() => rows.value.length > 240 ? 3 : rows.value.length > 80 ? 2 : 1)
const cardWidth = computed(() => [840, 840, 1000, 1400][colCount.value])
// 行优先排布：#1 #2 #3 横向读、#4 换行（review 意见）
const columns = computed(() =>
    Array.from({length: colCount.value}, (_, c) => rows.value.filter((_, i) => i % colCount.value === c)))

// 主题固定走紫谱档（排名跨难度，无单一难度归属）
const bgKey = bgKeyOf(3, false)
const accent = DIFF_COLORS[4]

function onCoverErr(e: Event) {
    const img = e.target as HTMLImageElement
    if (!img.src.endsWith(COVER_FALLBACK)) img.src = COVER_FALLBACK
}
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.rank-title { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 26px; color: #fff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); white-space: nowrap; }
.count-chip { font-size: 14px; letter-spacing: 0.14em; color: rgba(255,255,255,0.55); white-space: nowrap; }

.rank-head { display: flex; align-items: center; gap: 8px; padding: 0 10px 0 8px; margin-bottom: -2px; }
.rank-head > span { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 11px; letter-spacing: 0.05em; color: rgba(255,255,255,0.42); }
.rh-rank { flex: 0 0 44px; }
.rh-song { flex: 1 1 auto; min-width: 0; }
.rh-diff { flex: 0 0 40px; text-align: center; }
.rh-ds { flex: 0 0 36px; text-align: right; }
.rh-val { flex: 0 0 50px; text-align: right; white-space: nowrap; }

.rank-row { display: flex; align-items: center; gap: 8px; height: 34px; padding: 0 10px 0 8px; border-radius: 8px; background: rgba(0,0,0,0.30); border: 1px solid rgba(255,255,255,0.08); }
.rr-rank { flex: 0 0 44px; font-family: 'Torus',sans-serif; font-weight: bold; font-size: 15px; color: rgba(255,255,255,0.6); }
.rr-cover { flex: 0 0 26px; width: 26px; height: 26px; object-fit: cover; border-radius: 5px; box-shadow: 0 0 0 1px rgba(255,255,255,0.25); }
.rr-title { flex: 1 1 auto; min-width: 0; font-family: 'SEGA NewRodin','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 14.5px; color: #fff; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.rr-dx { flex: 0 0 auto; font-size: 10px; letter-spacing: 0.08em; color: #ffb02c; padding: 1px 5px; border: 1px solid #ffb02c66; border-radius: 4px; }
.rr-diff { flex: 0 0 40px; font-size: 12px; text-align: center; }
.rr-ds { flex: 0 0 36px; font-family: 'Torus',sans-serif; font-weight: bold; font-size: 14px; color: rgba(255,255,255,0.55); text-align: right; }
.rr-val { flex: 0 0 50px; font-family: 'Torus',sans-serif; font-weight: 800; font-size: 15px; color: rgba(255,255,255,0.92); text-align: right; }

.footer-text { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.4em; color: rgba(255,255,255,0.45); }
.foot-note { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 12px; letter-spacing: 0.02em; color: rgba(255,255,255,0.45); }
.tabular-nums { font-variant-numeric: tabular-nums; }
</style>
