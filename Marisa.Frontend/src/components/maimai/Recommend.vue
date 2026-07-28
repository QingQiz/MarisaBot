<template>
    <MaiCardShell v-if="data" class="mai-recommend" :bg-key="bgKey" :accent="accent" :width="cardWidth" pad-bottom="pb-8">
        <header class="flex items-end justify-between gap-8">
            <div class="min-w-0">
                <div class="flex items-baseline gap-4">
                    <h1 class="page-title">{{ isPlan ? '推分计划' : '推分推荐' }}</h1>
                    <span class="page-title-en">{{ isPlan ? 'RATING PLAN' : 'RATING GUIDE' }}</span>
                </div>
                <div class="player-line mt-2"><span>PLAYER</span>{{ data.Nickname }}</div>
            </div>
            <div class="rating-block shrink-0">
                <div class="rating-label">DX RATING</div>
                <div class="flex items-baseline justify-end gap-3">
                    <span class="rating-current">{{ data.CurrentRating }}</span>
                    <template v-if="isPlan">
                        <span class="rating-arrow">→</span>
                        <span class="rating-target">{{ data.ProjectedRating }}</span>
                    </template>
                </div>
                <div v-if="isPlan" class="rating-goal">目标 {{ data.TargetRating }}</div>
            </div>
        </header>

        <div class="header-rule mt-6"></div>
        <div v-if="!isPlan" class="quick-note mt-3">以下推荐相互独立，可任选其中一项作为目标。</div>

        <section v-if="oldItems.length" class="mt-6">
            <div class="section-head">
                <span class="section-chip old-chip">B35</span>
                <span class="section-name">OLD</span>
                <div class="section-rule"></div>
                <span class="section-count">{{ sectionCount(oldItems.length) }}</span>
            </div>
            <div class="recommend-grid mt-3" :style="gridStyle">
                <RecommendCard v-for="item in oldItems" :key="item.SongId + '-' + item.LevelIndex"
                               :item="item" :show-step="isPlan"/>
            </div>
        </section>

        <section v-if="newItems.length" class="mt-6">
            <div class="section-head">
                <span class="section-chip new-chip-section">B15</span>
                <span class="section-name">NEW</span>
                <div class="section-rule"></div>
                <span class="section-count">{{ sectionCount(newItems.length) }}</span>
            </div>
            <div class="recommend-grid mt-3" :style="gridStyle">
                <RecommendCard v-for="item in newItems" :key="item.SongId + '-' + item.LevelIndex"
                               :item="item" :show-step="isPlan"/>
            </div>
        </section>

        <footer class="mt-6 flex items-baseline justify-between gap-6">
            <div class="foot-note min-w-0 whitespace-nowrap">拟合难度数据来源：水鱼查分器</div>
            <span class="footer-text shrink-0">MARISA BOT · RATING GUIDE</span>
        </footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {DIFF_COLORS, bgKeyOf} from '@/components/maimai/utils/song_card'
import type {RecommendationCardData} from '@/components/maimai/utils/recommend_t'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import RecommendCard from '@/components/maimai/partial/RecommendCard.vue'

const route = useRoute()
const data = ref<RecommendationCardData | null>(null)

axios.get(context_get, {params: {id: route.query.id, name: 'recommendation'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})

const isPlan = computed(() => data.value?.Mode === 'plan')
const oldItems = computed(() => data.value?.Items.filter(x => x.Bucket === 'old') ?? [])
const newItems = computed(() => data.value?.Items.filter(x => x.Bucket === 'new') ?? [])
const twoColumns = computed(() => isPlan.value && (data.value?.Items.length ?? 0) > 8)
const cardWidth = computed(() => twoColumns.value ? 1400 : 840)
const gridStyle = computed(() => ({gridTemplateColumns: `repeat(${twoColumns.value ? 2 : 1}, minmax(0, 1fr))`}))
const sectionCount = (count: number) => `${count} ${isPlan.value ? (count === 1 ? 'STEP' : 'STEPS') : (count === 1 ? 'OPTION' : 'OPTIONS')}`

const bgKey = bgKeyOf(3, false)
const accent = DIFF_COLORS[4]
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>

<style scoped lang="postcss">
.page-title { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 34px; color: #fff; text-shadow: 0 2px 4px rgba(0,0,0,0.5); white-space: nowrap; }
.page-title-en { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 15px; letter-spacing: 0.22em; color: rgba(255,255,255,0.5); white-space: nowrap; }
.player-line { font-family: 'SEGA NewRodin','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 19px; color: rgba(255,255,255,0.9); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.player-line span { font-family: 'Torus',sans-serif; font-size: 11px; letter-spacing: 0.18em; color: rgba(255,255,255,0.4); margin-right: 10px; }
.rating-block { min-width: 250px; text-align: right; font-family: 'Torus',sans-serif; font-variant-numeric: tabular-nums; }
.rating-label { font-weight: bold; font-size: 11px; letter-spacing: 0.22em; color: rgba(255,255,255,0.4); }
.rating-current, .rating-target { font-weight: 900; font-size: 40px; line-height: 1.1; color: #fff; }
.rating-target { color: #dbaaff; }
.rating-arrow { font-size: 21px; color: rgba(255,255,255,0.35); }
.rating-goal { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 12px; color: rgba(255,255,255,0.48); }
.header-rule { height: 2px; border-radius: 9999px; background: linear-gradient(90deg, #dbaaffaa, rgba(255,255,255,0.12)); }
.quick-note { font-family: 'Microsoft YaHei',sans-serif; font-size: 12px; color: rgba(255,255,255,0.48); }
.section-head { display: flex; align-items: center; gap: 12px; }
.section-chip { min-width: 62px; padding: 3px 14px; border-radius: 9999px; text-align: center; font-family: 'Torus',sans-serif; font-weight: 900; font-size: 16px; color: #fff; box-shadow: 0 0 0 2px rgba(255,255,255,0.72); }
.old-chip { background: #f04f9d; }
.new-chip-section { background: #3f9eea; }
.section-name { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 14px; letter-spacing: 0.18em; color: rgba(255,255,255,0.65); }
.section-rule { flex: 1; height: 1px; background: rgba(255,255,255,0.14); }
.section-count { font-family: 'Torus',sans-serif; font-weight: bold; font-size: 11px; letter-spacing: 0.12em; color: rgba(255,255,255,0.38); }
.recommend-grid { display: grid; gap: 10px; }
</style>
