<script setup lang="ts">
import {useRoute} from 'vue-router'
import {ref, computed, nextTick, watch} from 'vue'
import axios from 'axios'
import {context_get} from '@/GlobalVars'
import type {GroupedSong, Score, PlateInfo} from '@/components/maimai/utils/summary_t'
import {achievementOrdinal, fcOrdinal, fsOrdinal, dxScoreStar} from '@/components/maimai/utils/ordinal'
import StatsBar from '@/components/maimai/partial/StatsBar.vue'

const route          = useRoute()
const id             = ref(route.query.id)
const data_fetched   = ref(false)

const grouped        = ref([] as GroupedSong[])
const scores         = ref({} as Record<string, Score>)
const title          = ref('SUMMARY')
const plate          = ref<PlateInfo | null>(null)

axios.all([
    axios.get(context_get, {params: {id: id.value, name: 'GroupedSongs'}}),
    axios.get(context_get, {params: {id: id.value, name: 'Scores'}}),
    axios.get(context_get, {params: {id: id.value, name: 'Title'}}),
    axios.get(context_get, {params: {id: id.value, name: 'Plate'}}),
]).then(res => {
    grouped.value = res[0].data ?? []
    scores.value  = res[1].data ?? {}
    title.value   = res[2].data ?? 'SUMMARY'
    plate.value   = res[3].data ?? null

    // sum 模式按白紫红黄绿排再按 ID 排（plate 已在服务端按定数排好，不动）
    if (!plate.value) {
        for (const g of grouped.value) {
            g.x.sort((a, b) => {
                if (a.Item2 !== b.Item2) return b.Item2 - a.Item2
                return b.Item3.Id - a.Item3.Id
            })
        }
    }
}).finally(() => { data_fetched.value = true })

const allCharts = computed(() => grouped.value.flatMap(g => g.x))

// 游戏内成就姓名框（「樱将完成表」这类查询才有；无对应姓名框时 null，布局回落纯文本标题）
const nameplateSrc = computed(() =>
    plate.value?.NamePlateImg ? `/assets/maimai/pic/plate/${plate.value.NamePlateImg}` : null)

// 标题真实测宽 auto-shrink：从 TITLE_MAX 起，按 scrollWidth/clientWidth 迭代缩到塞进 title
// 弹性槽（title-row 1250px − stats-bar 720px − gap 30px ≈ 500px）。短标题保持 MAX、不再像按
// 字符数估算那样把西文/短标题过度缩小留出空隙；长标题缩到刚好填满，与右侧 stats-bar 之间不留缝。
const TITLE_MAX = 100
const TITLE_MIN = 32
const titleEl   = ref<HTMLElement | null>(null)
const titleSize = ref(TITLE_MAX)

async function fitTitle() {
    const el = titleEl.value
    if (!el) return
    titleSize.value = TITLE_MAX
    await nextTick()
    try { await document.fonts.ready } catch { /* 测量兜底 */ }
    await nextTick()
    for (let pass = 0; pass < 5 && el.scrollWidth > el.clientWidth; pass++) {
        const next = Math.floor(titleSize.value * el.clientWidth / el.scrollWidth)
        titleSize.value = Math.max(TITLE_MIN, Math.min(next, titleSize.value - 1))
        await nextTick()
        if (titleSize.value <= TITLE_MIN) break
    }
}

watch(data_fetched, v => { if (v) fitTitle() }, {flush: 'post'})

function getScore(songId: number, levelIdx: number): Score | undefined {
    return scores.value[`(${songId}, ${levelIdx})`]
}

function isPassed(songId: number, levelIdx: number, maxDx: number = 0): boolean {
    if (!plate.value) return false
    const s = getScore(songId, levelIdx)
    if (!s) return false
    const lv = plate.value.Dim === 'Achievement' ? achievementOrdinal(s.achievements)
            : plate.value.Dim === 'Fc'           ? fcOrdinal(s.fc)
            : plate.value.Dim === 'Fs'           ? fsOrdinal(s.fs)
            :                                       dxScoreStar(s.dxScore, maxDx)
    return lv >= plate.value.Level
}

function calcRank(a: number): string {
    if (a >= 100.5) return 'sssp'
    if (a >= 100)   return 'sss'
    if (a >= 99.5)  return 'ssp'
    if (a >= 99)    return 'ss'
    if (a >= 98)    return 'sp'
    if (a >= 97)    return 's'
    if (a >= 94)    return 'aaa'
    if (a >= 90)    return 'aa'
    if (a >= 80)    return 'a'
    if (a >= 75)    return 'bbb'
    if (a >= 70)    return 'bb'
    if (a >= 60)    return 'b'
    if (a >= 50)    return 'c'
    return 'd'
}

// MaiMaiSong.LevelColor 5 个（白紫红黄绿）的对应 hex
const levelColors = ['#52e72b', '#ffa801', '#ff5a66', '#c64fe4', '#dbaaff']
function getLevelColor(i: number): string { return levelColors[i] ?? '#000' }

function getBorder(score: Score | undefined): string | null {
    if (!score) return null
    const a = score.achievements
    if (a >= 100) return '/assets/maimai/pic/border_SSS.png'
    if (a >= 99)  return '/assets/maimai/pic/border_SS.png'
    if (a >= 97)  return '/assets/maimai/pic/border_S.png'
    return null
}

function getMarker(score: Score, maxDx: number = 0): string | null {
    if (!plate.value) return null
    switch (plate.value.Dim) {
        case 'Achievement': return `/assets/maimai/pic/rank_summary_${score.rate}.png`
        case 'Fc':          return score.fc ? `/assets/maimai/pic/icon_${score.fc}.png` : null
        case 'Fs': {
            if (!score.fs) return null
            // diving-fish 返回 fsd/fsdp（老命名），但素材里 icon_fsd.png 印的是旧 FSD/FSD+ 字。
            // 现行游戏内称为 FDX/FDX+，repo 里 icon_fdx.png / icon_fdxp.png 是新版。
            const fsNorm = score.fs === 'fsd' ? 'fdx' : score.fs === 'fsdp' ? 'fdxp' : score.fs
            return `/assets/maimai/pic/icon_${fsNorm}.png`
        }
        case 'DxScore': {
            // 展示用户实际达到的星档（不是命令的 threshold 星）。跟 Achievement / Fc 维度
            // 的 "显示实际达到的 rank/fc" 行为一致 — 用户拿 5★ 不会因命令是 4星 就只显示 4★。
            const star = dxScoreStar(score.dxScore, maxDx)
            return star >= 1 ? `/assets/maimai/pic/music_icon_dxstar_${star}.png` : null
        }
    }
}

function fcColor(fc: string): string {
    return ({fc: '#32CD32', fcp: '#7CFC00', ap: '#DAA520', app: '#FFD700'} as Record<string, string>)[fc] ?? '#FFFFFF'
}

function groupKeyColor(g: GroupedSong): string {
    if (plate.value) return '#1a1a1a'
    let min = 5
    for (const s of g.x) {
        const sc = getScore(s.Item3.Id, s.Item2)
        if (!sc) return '#1a1a1a'
        const r = fcOrdinal(sc.fc)
        if (r < min) min = r
    }
    return ({1: '#32CD32', 2: '#7CFC00', 3: '#DAA520', 4: '#FFD700'} as Record<number, string>)[min] ?? '#1a1a1a'
}

function groupMinRank(g: GroupedSong): string | null {
    // 该组所有歌中最低成绩对应的 icon。
    // 任何一首未打过 (np / scores 字典里没条目) → 整组不显示 min-rank（用户诉求：组内必须全打过才有意义）。
    // 打过但 ach=0 仍算入（其 rank=d，min 会落到 d）。
    //
    // DxScore 维度时显示最低星档 icon (music_icon_dxstar_${minStar}.png)，其他维度（含 sum/Fc/Fs）
    // 显示最低达成率 rank icon (rank_${minAch}.png)。
    if (plate.value && plate.value.Dim === 'DxScore') {
        let minStar = Infinity
        for (const s of g.x) {
            const sc = getScore(s.Item3.Id, s.Item2)
            if (!sc) return null
            const star = dxScoreStar(sc.dxScore, s.Item3.MaxDx)
            if (star < minStar) minStar = star
        }
        if (!isFinite(minStar) || minStar < 1) return null
        return `/assets/maimai/pic/music_icon_dxstar_${minStar}.png`
    }

    let minA = Infinity
    for (const s of g.x) {
        const sc = getScore(s.Item3.Id, s.Item2)
        if (!sc) return null
        if (sc.achievements < minA) minA = sc.achievements
    }
    if (!isFinite(minA)) return null
    return `/assets/maimai/pic/rank_summary_${calcRank(minA)}.png`
}

function formatAch(a: number): {intPart: string, fracPart: string} {
    const [i, f] = a.toFixed(4).split('.')
    return {
        intPart:  i,           // 不再补 '0' 到 3 位 — "99.xxxx" 比 "099.xxxx" 自然
        fracPart: '.' + f,
    }
}
</script>

<template>
    <div class="mai-summary" v-if="data_fetched">
        <div class="title-row">
            <span class="title" ref="titleEl" :style="{fontSize: titleSize + 'px'}">{{ title }}</span>
            <StatsBar :charts="allCharts" :scores="scores" :detail="true" :plate="plate" class="title-stats"/>
        </div>
        <div v-if="nameplateSrc" class="nameplate-row">
            <img :src="nameplateSrc" alt=""/>
        </div>
        <div class="groups">
            <div class="group" v-for="g in grouped" :key="g.Key">
                <div class="group-title" :style="{color: groupKeyColor(g)}">
                    <span>{{ g.Key }}</span>
                    <img v-if="groupMinRank(g)" :src="groupMinRank(g)!" class="min-rank" alt=""/>
                    <StatsBar :charts="g.x" :scores="scores" :detail="false" :plate="plate" class="group-stats"/>
                </div>
                <div class="row">
                    <template v-for="s in g.x" :key="`${s.Item3.Id}-${s.Item2}`">
                        <div v-for="score in [getScore(s.Item3.Id, s.Item2)]" class="cell">
                            <div class="cover" :style="`background-image: url('/assets/maimai/cover/${s.Item3.Id}.png')`"></div>
                            <!-- plate mode 达成 → 印章罩 + 居中 marker（带边框） -->
                            <template v-if="plate && score && isPassed(s.Item3.Id, s.Item2, s.Item3.MaxDx)">
                                <img v-if="getBorder(score)" :src="getBorder(score)!" class="border-img" alt=""/>
                                <div class="plate-stamp"></div>
                                <img v-if="getMarker(score, s.Item3.MaxDx)"
                                     :src="getMarker(score, s.Item3.MaxDx)!"
                                     class="plate-marker"
                                     :class="{rank: plate.Dim === 'Achievement', dxstar: plate.Dim === 'DxScore'}"
                                     alt=""/>
                            </template>
                            <!-- plate mode 未达成 — 仅原色曲绘 + 难度三角，无边框、无成绩条 -->
                            <!-- sum mode: 底部成绩 + 中心 rank（带边框） -->
                            <template v-else-if="!plate && score">
                                <img v-if="getBorder(score)" :src="getBorder(score)!" class="border-img" alt=""/>
                                <img :src="`/assets/maimai/pic/rank_summary_${score.rate}.png`" class="sum-rank" alt=""/>
                                <div class="achievement-bar" :style="{color: fcColor(score.fc)}">
                                    <span class="ach-text"><span class="ach-int">{{ formatAch(score.achievements).intPart }}</span><span class="ach-frac">{{ formatAch(score.achievements).fracPart }}</span></span>
                                </div>
                            </template>
                            <div class="level-mark" :style="`background: ${getLevelColor(s.Item2)}`"></div>
                        </div>
                    </template>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped lang="postcss" src="@/assets/css/maimai/summary.pcss"/>
