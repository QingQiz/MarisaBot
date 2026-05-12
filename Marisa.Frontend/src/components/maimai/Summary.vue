<script setup lang="ts">
import {useRoute} from 'vue-router'
import {ref, computed} from 'vue'
import axios from 'axios'
import {context_get} from '@/GlobalVars'
import type {GroupedSong, Score, PlateInfo} from '@/components/maimai/utils/summary_t'
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

function getScore(songId: number, levelIdx: number): Score | undefined {
    return scores.value[`(${songId}, ${levelIdx})`]
}

function achievementOrdinal(a: number): number {
    if (a >= 100.5) return 13
    if (a >= 100)   return 12
    if (a >= 99.5)  return 11
    if (a >= 99)    return 10
    if (a >= 98)    return 9
    if (a >= 97)    return 8
    if (a >= 94)    return 7
    if (a >= 90)    return 6
    if (a >= 80)    return 5
    if (a >= 75)    return 4
    if (a >= 70)    return 3
    if (a >= 60)    return 2
    if (a >= 50)    return 1
    return 0
}
function fcOrdinal(fc: string): number {
    return ({fc: 1, fcp: 2, ap: 3, app: 4} as Record<string, number>)[fc] ?? 0
}
function fsOrdinal(fs: string): number {
    return ({sync: 1, fs: 2, fsp: 3, fdx: 4, fdxp: 5} as Record<string, number>)[fs] ?? 0
}

function isPassed(songId: number, levelIdx: number): boolean {
    if (!plate.value) return false
    const s = getScore(songId, levelIdx)
    if (!s) return false
    const lv = plate.value.Dim === 'Achievement' ? achievementOrdinal(s.achievements)
            : plate.value.Dim === 'Fc'           ? fcOrdinal(s.fc)
            :                                       fsOrdinal(s.fs)
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

function getMarker(score: Score): string | null {
    if (!plate.value) return null
    switch (plate.value.Dim) {
        case 'Achievement': return `/assets/maimai/pic/rank_${score.rate}.png`
        case 'Fc':          return score.fc ? `/assets/maimai/pic/icon_${score.fc}.png` : null
        case 'Fs':          return score.fs ? `/assets/maimai/pic/icon_${score.fs}.png` : null
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
    if (plate.value) return null
    let minA = Infinity
    for (const s of g.x) {
        const sc = getScore(s.Item3.Id, s.Item2)
        if (!sc) return null
        if (sc.achievements < minA) minA = sc.achievements
    }
    if (!isFinite(minA)) return null
    return `/assets/maimai/pic/rank_${calcRank(minA)}.png`
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
            <span class="title">{{ title }}</span>
            <StatsBar :charts="allCharts" :scores="scores" :detail="true" class="title-stats"/>
        </div>
        <div class="groups">
            <div class="group" v-for="g in grouped" :key="g.Key">
                <div class="group-title" :style="{color: groupKeyColor(g)}">
                    <span>{{ g.Key }}</span>
                    <img v-if="groupMinRank(g)" :src="groupMinRank(g)!" class="min-rank" alt=""/>
                    <StatsBar :charts="g.x" :scores="scores" :detail="false" class="group-stats"/>
                </div>
                <div class="row">
                    <template v-for="s in g.x" :key="`${s.Item3.Id}-${s.Item2}`">
                        <div v-for="score in [getScore(s.Item3.Id, s.Item2)]" class="cell">
                            <div class="cover" :style="`background-image: url('/assets/maimai/cover/${s.Item3.Id}.png')`"></div>
                            <!-- plate mode 达成 → 印章罩 + 居中 marker（带边框） -->
                            <template v-if="plate && score && isPassed(s.Item3.Id, s.Item2)">
                                <img v-if="getBorder(score)" :src="getBorder(score)!" class="border-img" alt=""/>
                                <div class="plate-stamp"></div>
                                <img v-if="getMarker(score)"
                                     :src="getMarker(score)!"
                                     class="plate-marker"
                                     :class="{rank: plate.Dim === 'Achievement'}"
                                     alt=""/>
                            </template>
                            <!-- plate mode 未达成 — 仅原色曲绘 + 难度三角，无边框、无成绩条 -->
                            <!-- sum mode: 底部成绩 + 中心 rank（带边框） -->
                            <template v-else-if="!plate && score">
                                <img v-if="getBorder(score)" :src="getBorder(score)!" class="border-img" alt=""/>
                                <img :src="`/assets/maimai/pic/rank_${score.rate}.png`" class="sum-rank" alt=""/>
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
