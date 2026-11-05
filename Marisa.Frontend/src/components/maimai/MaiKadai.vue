<template>
    <MaiCardShell v-if="data" bg-key="MST" :accent="ACCENT" :width="1200" pad-bottom="pb-9">
        <div class="eyebrow">MARISA BOT · 课题</div>
        <div class="owner">{{ data.Nickname }} 的课题</div>

        <section v-for="(item, k) in data.Items" :key="item.Goal" class="kadai"
                 :style="{'--goal': goalColor(item.Goal), '--outline': `url(#kadai-outline-${k})`}">
            <!-- 透明通道模糊后取阈值，得到各向同性的外扩描边；投影叠加在 1x 下粗细相差一倍 -->
            <svg v-if="isApGoal(item.Goal)" class="defs" aria-hidden="true">
                <filter :id="`kadai-outline-${k}`" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">
                    <feGaussianBlur in="SourceAlpha" stdDeviation="1.6"/>
                    <feComponentTransfer result="ring"><feFuncA type="linear" slope="20" intercept="-0.6"/></feComponentTransfer>
                    <feFlood class="outline-flood"/>
                    <feComposite in2="ring" operator="in"/>
                    <feMerge><feMergeNode/><feMergeNode in="SourceGraphic"/></feMerge>
                </filter>
            </svg>
            <div class="sec-head">
                <div class="goal-title"><b>{{ item.Goal }}</b><span>课题</span></div>
                <div class="progress">
                    <div class="pips">
                        <i v-for="(c, i) in item.Charts" :key="i" :class="{on: c.Done}" :style="{'--step': STEPS[i].color}"/>
                    </div>
                    <div v-if="doneCount(item) === item.Charts.length" class="all-clear">全部完成</div>
                    <div v-else class="count"><b>{{ doneCount(item) }}</b><span>/ {{ item.Charts.length }}</span></div>
                </div>
            </div>
            <div class="byline">
                <span>{{ item.Assigner ? `${item.Assigner} 布置` : '自己布置' }}</span>
                <span class="dot">·</span><span>{{ dateOf(item.CreatedAt) }}</span>
                <span v-if="item.Filter" class="chip">{{ item.Filter }}</span>
            </div>

            <div v-if="data.ColdStart" class="hint">你还没有几首{{ item.Goal }}记录，先从这个水平里最容易的几首开始</div>

            <div class="steps">
                <div v-for="(c, i) in item.Charts" :key="`${c.Id}-${c.LevelIndex}`" class="step" :class="{done: c.Done}">
                    <div class="step-head">
                        <span class="step-tag" :style="{background: STEPS[i].color}">{{ STEPS[i].name }}</span>
                        <span class="diff-pill" :class="{light: c.LevelIndex === 4}" :style="{background: DIFF_COLORS[c.LevelIndex]}">
                            {{ DIFF_NAMES[c.LevelIndex] }} {{ c.Level }}
                        </span>
                        <span class="const">{{ c.Constant.toFixed(1) }}</span>
                    </div>

                    <div class="cover-wrap">
                        <MaiCover :song-id="c.Id" :size="280" :frame-radius="22" :img-radius="16"/>
                        <img :src="typeBadgeSrc(c.Type)" class="type-badge" alt="">
                        <div v-if="c.Done" class="clear-badge">CLEAR</div>
                    </div>

                    <div class="title mai-title">{{ c.Title }}</div>

                    <div class="status">
                        <template v-if="c.Played">
                            <div class="ach" :class="{focus: ACH_TARGET[item.Goal] !== undefined}">
                                <span class="ach-int">{{ Math.floor(c.Achievement!) }}</span><span class="ach-frac">.{{ c.Achievement!.toFixed(4).split('.')[1] }}%</span>
                                <img :src="`${PIC}/rank_summary_${c.Rank}.png`" class="rank" alt="">
                            </div>
                            <div class="marks">
                                <img :src="`${PIC}/icon_${c.Fc || 'blank'}.png`" :class="{blank: !c.Fc, focus: isApGoal(item.Goal)}" alt="">
                                <img :src="`${PIC}/icon_${c.Fs || 'blank'}.png`" :class="{blank: !c.Fs}" alt="">
                                <span class="dx" :class="{focus: item.Goal === '五星'}">
                                    <img v-if="star(c)" :src="`${PIC}/music_icon_dxstar_${star(c)}.png`" alt="">
                                    <b>{{ c.DxScore }}</b><small>/{{ c.MaxDx }}</small>
                                </span>
                            </div>
                        </template>
                        <div v-else class="unplayed">未游玩</div>
                    </div>

                    <div class="goal-row">
                        <span class="label">目标</span>
                        <img v-if="isApGoal(item.Goal)" :src="`${PIC}/icon_${item.Goal === 'AP+' ? 'app' : 'ap'}.png`" class="goal-icon" alt="">
                        <span v-else class="target">{{ targetText(item.Goal, c) }}</span>
                        <span class="gap">
                            <template v-if="c.Done">已达成</template>
                            <template v-else-if="c.Played">{{ gapText(item.Goal, c) }}</template>
                        </span>
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
const ACH_TARGET: Record<string, number> = {'SSS': 100, 'SSS+': 100.5}
const FC_NAMES: Record<string, string> = {'fc': 'FC', 'fcp': 'FC+', 'ap': 'AP', 'app': 'AP+'}

const route = useRoute()
const data = ref<KadaiData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'kadai'}})
    .then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })

function goalColor(goal: string) { return GOAL_COLORS[goal] ?? ACCENT }
function doneCount(item: KadaiItem) { return item.Charts.filter(c => c.Done).length }
function star(c: KadaiChart) { return dxScoreStar(c.DxScore ?? 0, c.MaxDx) }
function isApGoal(goal: string) { return goal === 'AP' || goal === 'AP+' }
// 五星与后端同为 DX × 100 ≥ MaxDx × 97
function fiveStarDx(c: KadaiChart) { return Math.ceil(c.MaxDx * 97 / 100) }
function targetText(goal: string, c: KadaiChart) {
    return goal === '五星' ? `DX ${fiveStarDx(c)}` : `${ACH_TARGET[goal]}%`
}
function gapText(goal: string, c: KadaiChart) {
    if (isApGoal(goal)) return `当前 ${FC_NAMES[c.Fc ?? ''] ?? '未 FC'}`
    if (goal === '五星') return `还差 ${fiveStarDx(c) - (c.DxScore ?? 0)}`
    return `还差 ${(ACH_TARGET[goal] - (c.Achievement ?? 0)).toFixed(4)}%`
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

.owner {
    margin-top: 10px;
    font: 900 34px 'Microsoft YaHei', sans-serif;
}

.kadai {
    margin-top: 40px;
}

.kadai + .kadai {
    margin-top: 56px;
}

.sec-head {
    display: flex;
    align-items: center;
    gap: 24px;
}

.goal-title {
    flex: 1;
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

.progress {
    display: flex;
    align-items: center;
    gap: 18px;
}

.pips {
    display: flex;
    gap: 8px;
}

.pips i {
    width: 46px;
    height: 12px;
    border-radius: 9999px;
    background: rgba(255, 255, 255, .14);
}

.pips i.on {
    background: var(--step);
    box-shadow: 0 0 10px color-mix(in srgb, var(--step) 55%, transparent);
}

.count {
    min-width: 96px;
    text-align: right;
    font-family: 'Torus', sans-serif;
    line-height: 1;
}

.count b {
    font-size: 60px;
    font-weight: 800;
    color: var(--goal);
}

.count span {
    margin-left: 6px;
    font-size: 30px;
    font-weight: 700;
    opacity: .7;
}

.all-clear {
    padding: 6px 18px 7px;
    border-radius: 9999px;
    font: 900 26px 'Microsoft YaHei', sans-serif;
    color: #2a1608;
    background: var(--goal);
}

.byline {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 10px;
    margin-top: 12px;
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
    display: flex;
    flex-direction: column;
    min-width: 0;
    padding: 18px 18px 16px;
    border-radius: 26px;
    background: rgba(255, 255, 255, .07);
    box-shadow: inset 0 0 0 1.5px rgba(255, 255, 255, .12);
}

.step.done {
    background: color-mix(in srgb, var(--goal) 14%, rgba(255, 255, 255, .05));
    box-shadow: inset 0 0 0 2.5px var(--goal), 0 0 24px -6px color-mix(in srgb, var(--goal) 60%, transparent);
}

.step-head {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 16px;
}

.step-tag {
    margin-right: auto;
    padding: 3px 16px 4px;
    border-radius: 9999px;
    font: 900 24px 'Microsoft YaHei', sans-serif;
    color: #fff;
    text-shadow: 0 1px 2px rgba(0, 0, 0, .25);
}

.diff-pill {
    padding: 3px 12px 2px;
    border-radius: 9999px;
    font: 700 19px 'Torus', sans-serif;
    color: #fff;
    white-space: nowrap;
}

.diff-pill.light {
    color: #5b2a86;
}

.const {
    font: 700 26px 'Torus', sans-serif;
}

.cover-wrap {
    position: relative;
    align-self: center;
}

.type-badge {
    position: absolute;
    top: 14px;
    left: 14px;
    height: 26px;
    filter: drop-shadow(0 2px 4px rgba(0, 0, 0, .35));
}

.clear-badge {
    position: absolute;
    left: 50%;
    bottom: -14px;
    transform: translateX(-50%);
    padding: 4px 22px 3px;
    border-radius: 9999px;
    font: 900 26px/1.1 'Torus', sans-serif;
    letter-spacing: .1em;
    color: #2a1608;
    background: var(--goal);
    box-shadow: 0 4px 14px rgba(0, 0, 0, .35);
}

.title {
    margin-top: 26px;
    font-size: 30px;
    text-overflow: ellipsis;
}

.status {
    margin: 16px 0 16px;
    min-height: 92px;
}

.ach {
    display: flex;
    align-items: baseline;
    font: 700 40px/1 'Torus', sans-serif;
}

.ach.focus {
    color: var(--goal);
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

.defs {
    position: absolute;
    width: 0;
    height: 0;
}

.outline-flood {
    flood-color: var(--goal);
}

/* 图标外形不规则（FC+/AP+ 右上角有加号），描边要沿透明通道走 */
.marks > img.focus {
    opacity: 1;
    filter: var(--outline);
}

/* 空图标半透明，描边会透出来；它是正圆，直接画环 */
.marks > img.blank.focus {
    filter: none;
    border-radius: 9999px;
    box-shadow: 0 0 0 2.5px var(--goal);
}

.dx {
    display: flex;
    align-items: center;
    margin-left: auto;
    font: 700 24px 'Torus', sans-serif;
}

.dx.focus b {
    color: var(--goal);
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

.unplayed {
    display: flex;
    align-items: center;
    height: 92px;
    font: 900 32px 'Microsoft YaHei', sans-serif;
    color: rgba(255, 255, 255, .45);
}

.goal-row {
    display: flex;
    align-items: center;
    gap: 10px;
    height: 52px;
    margin-top: auto;
    padding: 0 16px;
    border-radius: 16px;
    background: rgba(0, 0, 0, .22);
    font: 700 22px 'Torus', 'Microsoft YaHei', sans-serif;
    white-space: nowrap;
}

.goal-row .label {
    font-family: 'Microsoft YaHei', sans-serif;
    color: rgba(255, 255, 255, .55);
}

.goal-icon {
    height: 34px;
}

.goal-row .gap {
    margin-left: auto;
    color: var(--goal);
}

.step.done .goal-row .gap {
    font-family: 'Microsoft YaHei', sans-serif;
    font-weight: 900;
}
</style>
