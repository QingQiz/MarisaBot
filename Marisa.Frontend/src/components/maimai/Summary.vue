<script setup lang="ts">
import {useRoute} from 'vue-router'
import {ref, computed} from 'vue'
import axios from 'axios'
import {context_get} from '@/GlobalVars'
import type {GroupedSong, Score, PlateInfo} from '@/components/maimai/utils/summary_t'
import {achievementOrdinal, fcOrdinal, fsOrdinal, dxScoreStar} from '@/components/maimai/utils/ordinal'
import {DIFF_COLORS, DIFF_NAMES, UTAGE, bgKeyOf, themeMainOf, tallCardBackground, coverSrcOf, COVER_FALLBACK} from '@/components/maimai/utils/song_card'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'

type Chart = GroupedSong['x'][number]

const route        = useRoute()
const data_fetched = ref(false)

const grouped = ref([] as GroupedSong[])
const scores  = ref({} as Record<string, Score>)
const title   = ref('SUMMARY')
const plate   = ref<PlateInfo | null>(null)

axios.all(['GroupedSongs', 'Scores', 'Title', 'Plate'].map(name =>
    axios.get(context_get, {params: {id: route.query.id, name}}),
)).then(res => {
    grouped.value = res[0].data ?? []
    scores.value  = res[1].data ?? {}
    title.value   = res[2].data ?? 'SUMMARY'
    plate.value   = res[3].data ?? null

    // sum 模式按白紫红黄绿排再按 ID 排（plate 已在服务端按定数排好，不动）
    if (!plate.value) {
        for (const g of grouped.value) {
            g.x.sort((a, b) => b.Item2 - a.Item2 || b.Item3.Id - a.Item3.Id)
        }
    }
}).finally(() => { data_fetched.value = true })

const RANK_COLORS: Record<string, string> = {sssp: '#ffd84f', sss: '#f2a93b', ssp: '#7fd6ff', ss: '#4a8dff', oth: '#ece8f4', np: '#6b6480'}
const FC_COLORS: Record<string, string> = {app: '#ffeaa0', ap: '#ffc23d', fcp: '#9df0a0', fc: '#33c36b', oth: '#ece8f4', np: '#6b6480'}

const allCharts = computed(() => grouped.value.flatMap(g => g.x))
const overall   = computed(() => stats(allCharts.value))

const utageOnly = computed(() => allCharts.value.length > 0 && allCharts.value.every(c => c.Item3.Id > 100000))
// 主题取出现最多的难度
const themeIdx = computed(() => {
    const count = [0, 0, 0, 0, 0]
    for (const c of allCharts.value) count[c.Item2]++
    return count.reduce((best, n, i) => n >= count[best] ? i : best, 0)
})
const accent     = computed(() => themeMainOf(themeIdx.value, utageOnly.value))
const background = computed(() => tallCardBackground(bgKeyOf(themeIdx.value, utageOnly.value), accent.value))

const subtitle = computed(() => {
    const diffs = utageOnly.value
        ? 'UTAGE'
        : [...new Set(allCharts.value.map(c => c.Item2))].sort().map(i => DIFF_NAMES[i]).join(' / ')
    return [diffs, plate.value?.DisplayName, `${allCharts.value.length} CHARTS`].filter(Boolean).join('  ·  ')
})
const condition = computed(() => {
    const rows = plate.value?.Difficulties
    if (!rows) return null
    return `姓名框 · ${rows.length > 4 ? 'BASIC – Re:MASTER' : 'BASIC – MASTER'} 全谱面 ${plate.value!.DisplayName}`
})
const nameplateSrc = computed(() =>
    plate.value?.NamePlateImg ? `/assets/maimai/pic/plate/${plate.value.NamePlateImg}` : null)

function getScore(c: Chart): Score | undefined {
    return scores.value[`(${c.Item3.Id}, ${c.Item2})`]
}

// sum 模式按 SSS 计完成
function isDone(c: Chart): boolean {
    const s = getScore(c)
    if (!s) return false
    const dim = plate.value?.Dim ?? 'Achievement'
    const lv  = dim === 'Achievement' ? achievementOrdinal(s.achievements)
              : dim === 'Fc'          ? fcOrdinal(s.fc)
              : dim === 'Fs'          ? fsOrdinal(s.fs)
              :                         dxScoreStar(s.dxScore, c.Item3.MaxDx)
    return lv >= (plate.value?.Level ?? 12)
}

function isCollected(c: Chart): boolean {
    return plate.value ? isDone(c) : !!getScore(c)
}

function stats(charts: Chart[]) {
    const rank = {sssp: 0, sss: 0, ssp: 0, ss: 0, oth: 0, np: 0}
    const fc   = {app: 0, ap: 0, fcp: 0, fc: 0, oth: 0, np: 0}
    let done = 0
    for (const c of charts) {
        const s = getScore(c)
        if (isDone(c)) done++
        if (!s) {
            rank.np++
            fc.np++
            continue
        }
        const a = s.achievements
        rank[s.fc === 'app' || a >= 100.5 ? 'sssp' : a >= 100 ? 'sss' : a >= 99.5 ? 'ssp' : a >= 99 ? 'ss' : 'oth']++
        fc[(['app', 'ap', 'fcp', 'fc'].includes(s.fc) ? s.fc : 'oth') as keyof typeof fc]++
    }
    return {
        total: charts.length,
        done,
        rank: [['sssp', 'SSS+'], ['sss', 'SSS'], ['ssp', 'SS+'], ['ss', 'SS'], ['oth', 'OTH'], ['np', 'NP']]
            .map(([key, label]) => ({key, label, n: rank[key as keyof typeof rank]})),
        fc: [['app', 'AP+'], ['ap', 'AP'], ['fcp', 'FC+'], ['fc', 'FC'], ['oth', 'OTH'], ['np', 'NP']]
            .map(([key, label]) => ({key, label, n: fc[key as keyof typeof fc]})),
    }
}

function pct(n: number, total: number): string {
    return `${total ? Math.round(n / total * 1000) / 10 : 0}%`
}

function diffColor(c: Chart): string {
    return c.Item3.Id > 100000 ? UTAGE.main : DIFF_COLORS[c.Item2] ?? '#999'
}

// diving-fish 返回 fsd/fsdp（老命名），素材里 icon_fsd.png 印的是旧 FSD 字；现行 FDX 用 icon_fdx
function fsIcon(fs: string): string {
    return `/assets/maimai/pic/icon_${fs === 'fsd' ? 'fdx' : fs === 'fsdp' ? 'fdxp' : fs}.png`
}

// 展示实际达到的档位（拿到 5★ 不会因命令是 4 星就只显示 4★）
function marker(c: Chart): string | null {
    const s = getScore(c)
    if (!plate.value || !s) return null
    switch (plate.value.Dim) {
        case 'Achievement': return `/assets/maimai/pic/rank_summary_${s.rate}.png`
        case 'Fc':          return s.fc ? `/assets/maimai/pic/icon_${s.fc}.png` : null
        case 'Fs':          return s.fs ? fsIcon(s.fs) : null
        case 'DxScore': {
            const star = dxScoreStar(s.dxScore, c.Item3.MaxDx)
            return star >= 1 ? `/assets/maimai/pic/music_icon_dxstar_${star}.png` : null
        }
    }
}

// 完成表里打过但未达成：有对应维度图标就显示图标，否则显示达成率
function progressIcon(c: Chart): string | null {
    const s = getScore(c)
    if (!plate.value || !s || isDone(c)) return null
    switch (plate.value.Dim) {
        case 'Fc':      return s.fc ? `/assets/maimai/pic/icon_${s.fc}.png` : null
        case 'Fs':      return s.fs ? fsIcon(s.fs) : null
        case 'DxScore': {
            const star = dxScoreStar(s.dxScore, c.Item3.MaxDx)
            return star >= 1 ? `/assets/maimai/pic/music_icon_dxstar_${star}.png` : null
        }
        default:        return null
    }
}

function showProgressAch(c: Chart): boolean {
    return !!plate.value && !!getScore(c) && !isDone(c) && !progressIcon(c)
}

function achParts(a: number): [string, string] {
    const [i, f] = a.toFixed(4).split('.')
    return [i, '.' + f]
}

function fcColor(fc: string): string {
    return ({fc: '#32CD32', fcp: '#7CFC00', ap: '#DAA520', app: '#FFD700'} as Record<string, string>)[fc] ?? '#FFFFFF'
}

// 组内全打过才显示最低评级；星级维度显示最低星档
function groupMinRank(g: GroupedSong): string | null {
    if (plate.value?.Dim === 'DxScore') {
        let min = Infinity
        for (const c of g.x) {
            const s = getScore(c)
            if (!s) return null
            min = Math.min(min, dxScoreStar(s.dxScore, c.Item3.MaxDx))
        }
        return isFinite(min) && min >= 1 ? `/assets/maimai/pic/music_icon_dxstar_${min}.png` : null
    }
    let min = Infinity
    for (const c of g.x) {
        const s = getScore(c)
        if (!s) return null
        min = Math.min(min, s.achievements)
    }
    return isFinite(min) ? `/assets/maimai/pic/rank_summary_${rateOf(min)}.png` : null

    function rateOf(a: number): string {
        return a >= 100.5 ? 'sssp' : a >= 100 ? 'sss' : a >= 99.5 ? 'ssp' : a >= 99 ? 'ss' : a >= 98 ? 'sp'
             : a >= 97 ? 's' : a >= 94 ? 'aaa' : a >= 90 ? 'aa' : a >= 80 ? 'a' : a >= 75 ? 'bbb'
             : a >= 70 ? 'bb' : a >= 60 ? 'b' : a >= 50 ? 'c' : 'd'
    }
}

function onCoverError(e: Event) {
    const img = e.target as HTMLImageElement
    if (!img.src.endsWith(COVER_FALLBACK)) img.src = COVER_FALLBACK
}
</script>

<template>
    <div v-if="data_fetched" class="mai-summary mai-card antialiased" :style="{background, '--accent': accent}">
        <div class="dots"></div>
        <div class="relative px-12 pt-9 pb-9">
            <div class="eyebrow">MARISA BOT · {{ plate ? 'PLATE PROGRESS' : 'SUMMARY' }}</div>
            <div class="flex items-end gap-10 mt-2">
                <div class="heading flex-1 min-w-0" :class="{han: /[㐀-鿿]/.test(title)}">
                    <MaiSongHeading :title="title" :artist="subtitle" :max="76" :min="34" :row-top="6" :artist-size="17" :artist-top="4">
                        <template v-if="condition" #sub><span class="cond-chip">{{ condition }}</span></template>
                    </MaiSongHeading>
                </div>
                <div class="big-count shrink-0">
                    <div><b>{{ overall.done }}</b><span>/{{ overall.total }}</span></div>
                    <small>{{ plate ? 'COMPLETE' : 'SSS CLEAR' }} · {{ pct(overall.done, overall.total) }}</small>
                </div>
            </div>

            <div v-if="nameplateSrc" class="plate-row mt-6">
                <img :src="nameplateSrc" class="nameplate" alt="">
                <div v-if="plate?.Difficulties" class="plate-table" :style="{gridTemplateColumns: `repeat(${plate.Difficulties.length}, minmax(0, 1fr))`}">
                    <div v-for="r in plate.Difficulties" :key="r.LevelIdx" class="plate-col">
                        <div class="plate-th" :class="{light: r.LevelIdx === 4}" :style="{background: DIFF_COLORS[r.LevelIdx]}">{{ DIFF_NAMES[r.LevelIdx] }}</div>
                        <div class="plate-td">
                            <div v-if="r.Done < r.Total" class="remain"><b>{{ r.Total - r.Done }}</b><span>剩余</span></div>
                            <div v-else class="remain clear" :style="{color: DIFF_COLORS[r.LevelIdx]}">CLEAR</div>
                            <div class="frac">{{ r.Done }} / {{ r.Total }}</div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="flex flex-col gap-[6px] mt-6">
                <div v-for="row in [{segs: overall.rank, colors: RANK_COLORS}, {segs: overall.fc, colors: FC_COLORS}]" class="seg-bar">
                    <div v-for="seg in row.segs.filter(x => x.n > 0)" :key="seg.key" class="seg" :class="{np: seg.key === 'np'}"
                         :style="{flexGrow: seg.n, background: row.colors[seg.key]}">
                        <span v-if="seg.n / overall.total > 0.06">{{ seg.label }} <b>{{ seg.n }}</b></span>
                    </div>
                </div>
            </div>

            <div class="flex flex-col gap-9 mt-9">
                <section v-for="g in grouped" :key="g.Key">
                    <div v-for="st in [stats(g.x)]" class="group-head">
                        <span class="g-capsule" :class="{complete: st.done === st.total}">
                            <span class="g-tag">{{ g.Key }}</span>
                            <span class="g-count"><b>{{ st.done }}</b> / {{ st.total }}</span>
                        </span>
                        <div class="flex-1 border-t-2 border-dashed border-white/15"></div>
                        <span v-if="groupMinRank(g)" class="g-min"><img :src="groupMinRank(g)!" alt=""></span>
                        <span class="mini">
                            <span v-for="row in [{segs: st.rank, colors: RANK_COLORS}, {segs: st.fc, colors: FC_COLORS}]" class="mini-row">
                                <i v-for="seg in row.segs.filter(x => x.n > 0)" :key="seg.key" :style="{flexGrow: seg.n, background: row.colors[seg.key]}"></i>
                            </span>
                        </span>
                    </div>
                    <div class="cells">
                        <div v-for="c in g.x" :key="`${c.Item3.Id}-${c.Item2}`" class="slot" :class="{got: isCollected(c)}">
                            <div class="photo">
                                <div class="inner">
                                    <img :src="coverSrcOf(c.Item3.Id)" @error="onCoverError" class="cover" alt="">
                                    <template v-if="!plate && getScore(c)">
                                        <img :src="`/assets/maimai/pic/rank_summary_${getScore(c)!.rate}.png`" class="sum-rank" alt="">
                                        <div class="achievement-bar" :style="{color: fcColor(getScore(c)!.fc)}">
                                            <span class="ach-text"><span class="ach-int">{{ achParts(getScore(c)!.achievements)[0] }}</span><span class="ach-frac">{{ achParts(getScore(c)!.achievements)[1] }}</span></span>
                                        </div>
                                    </template>
                                    <div v-else-if="showProgressAch(c)" class="ach-pill">
                                        <b>{{ achParts(getScore(c)!.achievements)[0] }}</b><small>{{ achParts(getScore(c)!.achievements)[1] }}</small>
                                    </div>
                                    <img v-else-if="progressIcon(c)" :src="progressIcon(c)!" class="progress-icon" alt="">
                                </div>
                            </div>
                            <div class="tape" :style="{background: diffColor(c)}"></div>
                            <img v-if="plate && isDone(c) && marker(c)" :src="marker(c)!" class="sticker"
                                 :class="{rank: plate.Dim === 'Achievement', dxstar: plate.Dim === 'DxScore'}" alt="">
                        </div>
                    </div>
                </section>
            </div>

            <footer class="mt-9 flex justify-end">
                <span class="footer-text">MARISA BOT · {{ plate ? 'PLATE PROGRESS' : 'SUMMARY' }}</span>
            </footer>
        </div>
    </div>
</template>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/maimai/summary.pcss"/>
