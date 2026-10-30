<script setup lang="ts">
import {useRoute} from 'vue-router';
import {ref, computed} from 'vue';
import axios from 'axios';
import {context_get} from "@/GlobalVars";
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import MaiCardShell from "@/components/maimai/MaiCardShell.vue";
import {GroupSongInfo, Score} from "@/components/chunithm/utils/summary_t";
import {getRank, isMaxScore, rankIcon} from "@/components/chunithm/utils/score";
import {difficultyColor} from "@/components/chunithm/utils/song-card";
import {bgKeyOf, themeMainOf, tallCardBackground} from "@/components/maimai/utils/song_card";

const route = useRoute()
const id    = ref(route.query.id)

const data_fetched = ref(false)

const grouped = ref([] as GroupSongInfo[])
const scores  = ref({} as { [key: string]: Score })
const title   = ref('')

// 标题单独取，取不到（旧 context）时就当没有，不影响正片
axios.get(context_get, {params: {id: id.value, name: 'Title'}})
    .then(res => {
        const data = res.data
        title.value = typeof data === 'string'
            ? (data.startsWith('"') ? JSON.parse(data) : data)
            : data == null ? '' : String(data)
    })
    .catch(() => {
        title.value = ''
    })

axios.all([
    axios.get(context_get, {params: {id: id.value, name: 'GroupedSongs'}}),
    axios.get(context_get, {params: {id: id.value, name: 'Scores'}}),
]).then(data => {
    grouped.value = data[0].data
    scores.value  = data[1].data

    const sort = route.query.sort as string | undefined;
    for (let i = 0; i < grouped.value.length; i++) {
        grouped.value[i].x.sort((a, b) => {
            if (sort === 'asc' || sort === 'desc') {
                const sa = GetScore(a.Item3.Id, a.Item2);
                const sb = GetScore(b.Item3.Id, b.Item2);
                const va = sa ? sa.score : 0;
                const vb = sb ? sb.score : 0;
                return sort === 'asc' ? va - vb : vb - va;
            }
            if (a.Item2 != b.Item2) return a.Item2 - b.Item2;
            return a.Item3.Id - b.Item3.Id;
        }).reverse()
    }
}).finally(() => {
    data_fetched.value = true
})

function GetScore(id: number, level: number) {
    return scores.value[`(${id}, ${level})`]
}

const allCharts = computed(() => grouped.value.flatMap(g => g.x))
const allScores = computed(() => allCharts.value.map(c => GetScore(c.Item3.Id, c.Item2)))
const total     = computed(() => allCharts.value.length)
const played    = computed(() => allScores.value.filter(Boolean).length)

// 卡片主题取出现次数最多的难度，与舞萌汇总一致
const themeIndex = computed(() => {
    const count = [0, 0, 0, 0, 0]
    for (const c of allCharts.value) count[c.Item2] = (count[c.Item2] ?? 0) + 1
    return count.reduce((best, n, i) => n >= count[best] ? i : best, 0)
})

// 卡片背景：完全照搬舞萌汇总的三行（bgKeyOf / themeMainOf / tallCardBackground），
// 主题索引同样取出现次数最多的难度；难度配色也沿用舞萌那套，保证两张图的底色一致。
// ULTIMA 的官方红只用在谱面上的难度色带，不参与背景。
const bgKey      = computed(() => bgKeyOf(themeIndex.value, false))
const accent     = computed(() => themeMainOf(themeIndex.value, false))
const background = computed(() => tallCardBackground(bgKey.value, accent.value))

function GroupScores(group: GroupSongInfo['x']) {
    return group.map(c => GetScore(c.Item3.Id, c.Item2))
}

function GroupPlayed(group: GroupSongInfo['x']) {
    return GroupScores(group).filter(Boolean).length
}

function Pct(n: number, t: number) {
    return `${t ? Math.round(n / t * 1000) / 10 : 0}%`
}

function CoverSrc(songId: number) {
    return `/assets/chunithm/cover/${songId}.png`
}

// fc / 达成率 -> 档位，数值越大越好。
// 1010000 是独立的最顶档，对应 OverPower.vue 里的 ajc：只看 fc 的话它和普通 AJ 分不开。
function GetTier(score?: Score) {
    if (!score) return 0
    if (isMaxScore(score)) return 5

    switch (score.fc) {
        case 'fullcombo':
            return 1
        case 'fullchain':
            return 2
        case 'fullchain2':
            return 3
        case 'alljustice':
            return 4
        default:
            return 0
    }
}

// 档位 -> 达成率文字颜色。最顶档淡金黄色，与统计条里的 ajc(amber-200) 同色。
function GetTierColor(tier: number, fallback = '#FFFFFF') {
    switch (tier) {
        case 5:
            return '#FDE68A'
        case 4:
            return '#FFDF00'
        case 3:
            return '#A4DE02'
        case 2:
            return '#ACDF87'
        case 1:
            return '#76BA1B'
        default:
            return fallback
    }
}

// 组标题取组内最低的 rank 图标；有任何一张没打就不显示（返回空串）
function GetGroupMinRank(group: GroupSongInfo['x']) {
    let min = 1145141919;

    for (const song of group) {
        const score = GetScore(song.Item3.Id, song.Item2)

        if (score) {
            if (score.score < min) min = score.score
        } else {
            return ''
        }
    }

    return min === 1145141919 ? '' : getRank(min)
}

// 难度色沿用 chunithm 主题色：ULTIMA 用官方红，在封面上方那道色带外面额外补一圈黑边区分
// （纯黑色带贴在深色卡片上会糊成一片，看不出难度，所以改成「主题红 + 黑描边」）。
function LevelColor(index: number) {
    return difficultyColor(index)
}
</script>

<template>
    <div v-if="data_fetched">
        <MaiCardShell :bg-key="bgKey" :accent="accent" :background="background" :width="1480"
                      class="chu-summary" :class="{'chu-ultima': themeIndex === 4}">
            <div class="eyebrow">MARISA BOT · SUMMARY</div>

            <!-- 标题行：右侧数字与标题同排，统计条独占下一行 -->
            <div class="flex items-end gap-10 mt-2">
                <div class="summary-title flex-1 min-w-0">{{ title || '完成表' }}</div>
                <div class="big-count">
                    <div><b>{{ played }}</b><span>/{{ total }}</span></div>
                    <small>PLAYED · {{ Pct(played, total) }}</small>
                </div>
            </div>

            <!-- 顶部统计条：左侧 OP 分式（opSum/opMax）、右侧完成百分比，分段条居中 -->
            <div class="mt-6">
                <OverPower :group="allCharts" :scores="allScores" layout="bars" numbers percent
                           constant-source="chart"/>
            </div>

            <div class="flex flex-col gap-9 mt-9">
                <section v-for="{Key: key, x: group} in grouped" :key="key">
                    <div class="group-head">
                        <span class="g-capsule" :class="{complete: GroupPlayed(group) === group.length}">
                            <span class="g-tag">{{ key }}</span>
                            <span class="g-count"><b>{{ GroupPlayed(group) }}</b> / {{ group.length }}</span>
                        </span>
                        <div class="flex-1 border-t-2 border-dashed border-white/15"></div>
                        <!-- 没打全的分组没有最低 rank 图标，但位置要留着，否则各组统计条右边缘对不齐 -->
                        <span class="g-min">
                            <img v-if="GetGroupMinRank(group)" :src="rankIcon(GetGroupMinRank(group))" alt=""
                                 onerror="this.style.opacity=0">
                        </span>
                        <OverPower :group="group" :scores="GroupScores(group)" layout="mini" constant-source="chart"/>
                    </div>

                    <div class="cells">
                        <div v-for="song in group" :key="`${song.Item3.Id}-${song.Item2}`" class="slot"
                             :class="{got: !!GetScore(song.Item3.Id, song.Item2)}">
                            <div class="photo">
                                <div class="inner">
                                    <img :src="CoverSrc(song.Item3.Id)" class="cover" alt="">
                                    <template v-if="GetScore(song.Item3.Id, song.Item2)">
                                        <img :src="rankIcon(getRank(GetScore(song.Item3.Id, song.Item2)!.score))"
                                             class="sum-rank" alt="">
                                        <div class="achievement-bar"
                                             :style="`color: ${GetTierColor(GetTier(GetScore(song.Item3.Id, song.Item2)))}`">
                                            <span class="ach-text">{{
                                                    GetScore(song.Item3.Id, song.Item2)!.score.toString().padStart(7, '0')
                                                }}</span>
                                        </div>
                                    </template>
                                </div>
                            </div>
                            <div class="tape" :class="{ultima: song.Item2 === 4}"
                                 :style="`background: ${LevelColor(song.Item2)}`"></div>
                        </div>
                    </div>
                </section>
            </div>

            <footer class="mt-9 flex justify-end">
                <span class="footer-text">MARISA BOT · SUMMARY</span>
            </footer>
        </MaiCardShell>
    </div>
</template>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/theme.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/summary.pcss"/>
