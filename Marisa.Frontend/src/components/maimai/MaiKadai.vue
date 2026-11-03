<template>
    <MaiCardShell v-if="data" bg-key="MST" :accent="ACCENT" :width="1200" pad-bottom="pb-9">
        <div class="eyebrow">MARISA BOT · 课题</div>

        <section v-for="(item, k) in data.Items" :key="item.Goal" class="kadai" :class="{'mt-12': k > 0}"
                 :style="{'--goal': goalColor(item.Goal)}">
            <div class="flex items-end gap-6 mt-3">
                <div class="flex-1 min-w-0">
                    <div class="goal-title"><b>{{ item.Goal }}</b><span>课题</span></div>
                    <div class="byline">
                        <span>{{ item.Assigner ? `${item.Assigner} 布置给 ${data.Nickname}` : `${data.Nickname} 的课题` }}</span>
                        <span class="dot">·</span><span>{{ dateOf(item.CreatedAt) }}</span>
                        <span v-if="item.Filter" class="chip">{{ item.Filter }}</span>
                    </div>
                </div>
                <div class="progress">
                    <b>{{ doneCount(item) }}</b><span>/ {{ item.Charts.length }}</span>
                    <div v-if="doneCount(item) === item.Charts.length" class="all-clear">全部完成</div>
                </div>
            </div>

            <div v-if="data.ColdStart" class="hint">你还没有几首{{ item.Goal }}记录，先从这个水平里最容易的几首开始</div>

            <div class="steps">
                <div v-for="(c, i) in item.Charts" :key="`${c.Id}-${c.LevelIndex}`" class="step" :class="{done: c.Done}">
                    <div class="step-head">
                        <span class="step-tag" :style="{background: STEPS[i].color}">{{ STEPS[i].name }}</span>
                        <img :src="typeBadgeSrc(c.Type)" class="type-badge" alt="">
                    </div>
                    <div class="cover-wrap">
                        <MaiCover :song-id="c.Id" :size="280" :frame-radius="22" :img-radius="16"/>
                        <div v-if="c.Done" class="clear-stamp">CLEAR</div>
                    </div>
                    <div class="title mai-title">{{ c.Title }}</div>
                    <div class="diff">
                        <span class="diff-pill" :class="{light: c.LevelIndex === 4}" :style="{background: DIFF_COLORS[c.LevelIndex]}">
                            {{ DIFF_NAMES[c.LevelIndex] }} {{ c.Level }}
                        </span>
                        <span class="const">{{ c.Constant.toFixed(1) }}</span>
                    </div>
                    <div class="status">
                        <template v-if="c.Played">
                            <div class="ach">
                                <span class="ach-int">{{ Math.floor(c.Achievement!) }}</span><span class="ach-frac">.{{ c.Achievement!.toFixed(4).split('.')[1] }}%</span>
                                <img :src="`${PIC}/rank_summary_${c.Rank}.png`" class="rank" alt="">
                            </div>
                            <div class="marks">
                                <img :src="`${PIC}/icon_${c.Fc || 'blank'}.png`" :class="{blank: !c.Fc}" alt="">
                                <img :src="`${PIC}/icon_${c.Fs || 'blank'}.png`" :class="{blank: !c.Fs}" alt="">
                                <span class="dx">
                                    <img v-if="star(c)" :src="`${PIC}/music_icon_dxstar_${star(c)}.png`" alt="">
                                    <b>{{ c.DxScore }}</b><small>/{{ c.MaxDx }}</small>
                                </span>
                            </div>
                            <div v-if="!c.Done && gapText(item.Goal, c)" class="gap">还差 {{ gapText(item.Goal, c) }}</div>
                        </template>
                        <div v-else class="unplayed">未游玩</div>
                    </div>
                </div>
            </div>
        </section>

        <footer class="mt-9 flex justify-end"><span class="footer-text">MARISA BOT · KADAI</span></footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {dxScoreStar} from '@/components/maimai/utils/ordinal'
import {DIFF_COLORS, DIFF_NAMES, typeBadgeSrc} from '@/components/maimai/utils/song_card'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'

interface KadaiChart {
    Id: number; Title: string; Type: string; LevelIndex: number; Level: string; Constant: number
    Played: boolean; Achievement: number | null; Rank: string | null; Fc: string | null; Fs: string | null
    DxScore: number | null; MaxDx: number; Done: boolean
}
interface KadaiItem { Goal: string; Filter: string; Assigner: string | null; CreatedAt: string; Charts: KadaiChart[] }
interface KadaiData { Nickname: string; ColdStart: boolean; Items: KadaiItem[] }

const PIC = '/assets/maimai/pic'
const ACCENT = '#ff9d3c'
const STEPS = [
    {name: '热身', color: '#3fbf6e'},
    {name: '进阶', color: '#f0a020'},
    {name: '挑战', color: '#ef4f5f'},
]
const GOAL_COLORS: Record<string, string> = {'SSS': '#f6c445', 'SSS+': '#ffb347', 'AP': '#ff8a3d', 'AP+': '#ff5fa2', '五星': '#ffd34e'}

const route = useRoute()
const data = ref<KadaiData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'kadai'}})
    .then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })

function goalColor(goal: string) { return GOAL_COLORS[goal] ?? ACCENT }
function doneCount(item: KadaiItem) { return item.Charts.filter(c => c.Done).length }
function star(c: KadaiChart) { return dxScoreStar(c.DxScore ?? 0, c.MaxDx) }
// 达成率目标给出差多少百分比，五星给出差多少 DX 分（与后端同为 DX × 100 ≥ MaxDx × 97）；AP 类看 FC 图标即可
function gapText(goal: string, c: KadaiChart) {
    const target = {'SSS': 100, 'SSS+': 100.5}[goal]
    if (target !== undefined) return `${(target - (c.Achievement ?? 0)).toFixed(4)}%`
    if (goal === '五星') return `${Math.ceil(c.MaxDx * 97 / 100) - (c.DxScore ?? 0)} DX分`
    return null
}
function dateOf(text: string) {
    const d = new Date(text)
    return `${d.getFullYear()}.${d.getMonth() + 1}.${d.getDate()}`
}
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.eyebrow {
    font: 700 14px 'Torus', sans-serif;
    letter-spacing: .32em;
    color: rgba(255, 255, 255, .55);
}

.goal-title {
    display: flex;
    align-items: baseline;
    gap: 14px;
    line-height: 1;
}

.goal-title b {
    font: 800 76px 'Torus', sans-serif;
    color: var(--goal);
    text-shadow: 0 4px 18px color-mix(in srgb, var(--goal) 45%, transparent);
}

.goal-title span {
    font: 900 50px 'Microsoft YaHei', sans-serif;
}

.byline {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 10px;
    margin-top: 14px;
    font: 700 24px 'Microsoft YaHei', sans-serif;
    color: rgba(255, 255, 255, .75);
}

.byline .dot {
    opacity: .5;
}

.chip {
    padding: 2px 14px 3px;
    border-radius: 9999px;
    font-size: 20px;
    background: rgba(255, 255, 255, .1);
    box-shadow: inset 0 0 0 1px rgba(255, 255, 255, .2);
}

.progress {
    text-align: right;
    font-family: 'Torus', sans-serif;
    line-height: 1;
}

.progress b {
    font-size: 84px;
    font-weight: 800;
    color: var(--goal);
}

.progress span {
    margin-left: 6px;
    font-size: 38px;
    font-weight: 700;
    opacity: .7;
}

.all-clear {
    margin-top: 6px;
    font: 900 24px 'Microsoft YaHei', sans-serif;
    color: var(--goal);
}

.hint {
    margin-top: 18px;
    padding: 10px 18px;
    border-radius: 14px;
    font: 700 22px 'Microsoft YaHei', sans-serif;
    color: rgba(255, 255, 255, .85);
    background: rgba(255, 255, 255, .08);
}

.steps {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 24px;
    margin-top: 24px;
}

.step {
    position: relative;
    min-width: 0;
    padding: 18px 18px 20px;
    border-radius: 26px;
    background: rgba(255, 255, 255, .07);
    box-shadow: inset 0 0 0 1.5px rgba(255, 255, 255, .12);
}

.step.done {
    background: color-mix(in srgb, var(--goal) 16%, rgba(255, 255, 255, .05));
    box-shadow: inset 0 0 0 2px color-mix(in srgb, var(--goal) 70%, transparent);
}

.step-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 14px;
}

.step-tag {
    padding: 3px 16px 4px;
    border-radius: 9999px;
    font: 900 24px 'Microsoft YaHei', sans-serif;
    color: #fff;
    text-shadow: 0 1px 2px rgba(0, 0, 0, .25);
}

.type-badge {
    height: 30px;
}

.cover-wrap {
    position: relative;
    display: flex;
    justify-content: center;
}

.clear-stamp {
    position: absolute;
    left: 50%;
    top: 50%;
    transform: translate(-50%, -50%) rotate(-12deg);
    padding: 6px 22px;
    border: 5px solid var(--goal);
    border-radius: 14px;
    font: 900 54px 'Torus', sans-serif;
    letter-spacing: .06em;
    color: var(--goal);
    background: rgba(10, 6, 20, .72);
}

.title {
    margin-top: 16px;
    font-size: 30px;
    text-overflow: ellipsis;
}

.diff {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-top: 12px;
}

.diff-pill {
    padding: 3px 14px 2px;
    border-radius: 9999px;
    font: 700 20px 'Torus', sans-serif;
    color: #fff;
}

.diff-pill.light {
    color: #5b2a86;
}

.const {
    font: 700 28px 'Torus', sans-serif;
}

.status {
    margin-top: 16px;
    min-height: 94px;
}

.ach {
    display: flex;
    align-items: baseline;
    font: 700 40px/1 'Torus', sans-serif;
}

.ach-frac {
    font-size: 28px;
}

.rank {
    height: 30px;
    margin-left: auto;
}

.marks {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-top: 12px;
}

.marks > img {
    height: 40px;
}

.marks > img.blank {
    opacity: .35;
}

.dx {
    display: flex;
    align-items: center;
    margin-left: auto;
    font: 700 24px 'Torus', sans-serif;
}

.dx img {
    width: 36px;
    height: 36px;
    object-fit: contain;
    margin-right: 4px;
}

.dx small {
    font-size: 18px;
    opacity: .6;
}

.gap {
    margin-top: 12px;
    font: 700 24px 'Torus', 'Microsoft YaHei', sans-serif;
    color: var(--goal);
}

.unplayed {
    display: flex;
    align-items: center;
    height: 94px;
    font: 900 32px 'Microsoft YaHei', sans-serif;
    color: rgba(255, 255, 255, .5);
}
</style>
