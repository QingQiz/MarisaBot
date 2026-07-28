<template>
    <article class="recommend-row" :style="{'--diff-color': diffColor}">
        <div class="diff-bar"></div>
        <div v-if="showStep" class="step-badge">{{ item.Step }}</div>
        <img :src="cover" @error="onCoverError" alt="" class="cover">

        <div class="content min-w-0">
            <div class="flex items-center gap-2 min-w-0">
                <div class="song-title">{{ item.Title }}</div>
                <span class="type-chip">{{ item.Type }}</span>
                <span class="diff-chip" :style="{color: diffColor, borderColor: diffColor}">
                    {{ diffName }} {{ item.Constant.toFixed(1) }}
                </span>
                <span class="action-chip">{{ item.Action === 'upgrade' ? '提升成绩' : `进入 ${item.Bucket === 'old' ? 'B35' : 'B15'}` }}</span>
            </div>

            <div class="metrics mt-2">
                <div class="metric achievement-metric">
                    <span class="metric-label">达成率</span>
                    <template v-if="item.CurrentAchievement != null">
                        <span class="metric-old">{{ item.CurrentAchievement.toFixed(4) }}%</span>
                        <span class="arrow">→</span>
                    </template>
                    <span class="metric-main">{{ item.TargetAchievement.toFixed(4) }}%</span>
                </div>
                <div class="metric rating-metric">
                    <span class="metric-label">单曲 Rating</span>
                    <span class="metric-old">{{ item.BaselineRating }}</span>
                    <span class="arrow">→</span>
                    <span class="metric-main">{{ item.TargetRating }}</span>
                    <span class="gain">+{{ item.Gain }}</span>
                </div>
            </div>

            <div class="subline mt-2">
                <span v-if="item.Difficulty" class="fit-info">
                    {{ difficultyText }}
                    <template v-if="item.Difficulty.Rank != null"> · 同定数 #{{ item.Difficulty.Rank }}/{{ item.Difficulty.Of }}</template>
                </span>
                <span v-else class="fit-info muted">暂无拟合难度数据</span>
                <span v-if="item.Replaced" class="replacement">
                    替换地板：{{ item.Replaced.Title }} · Ra {{ item.Replaced.Rating }}
                </span>
            </div>
        </div>
    </article>
</template>

<script setup lang="ts">
import {computed, ref, watchEffect} from 'vue'
import type {RecommendationItem} from '@/components/maimai/utils/recommend_t'
import {COVER_FALLBACK, DIFF_COLORS, DIFF_NAMES, coverSrcOf} from '@/components/maimai/utils/song_card'

const props = defineProps<{item: RecommendationItem; showStep: boolean}>()

const cover = ref('')
watchEffect(() => { cover.value = coverSrcOf(props.item.SongId) })
function onCoverError() { cover.value = COVER_FALLBACK }

const diffColor = computed(() => DIFF_COLORS[Math.min(props.item.LevelIndex, 4)] ?? '#999')
const diffName = computed(() => DIFF_NAMES[Math.min(props.item.LevelIndex, 4)] ?? props.item.Level)
const difficultyText = computed(() => {
    const d = props.item.Difficulty!
    if (d.Kind === 'fitted_ds') {
        return d.Personalized
            ? `当前 Rating 拟合定数 ${d.Value.toFixed(2)}`
            : `综合拟合定数 ${d.Value.toFixed(2)}`
    }
    return d.Personalized
        ? `当前 Rating 难度百分位 ${d.Value.toFixed(1)}%`
        : `同等级难度百分位 ${d.Value.toFixed(1)}%`
})
</script>

<style scoped lang="postcss">
.recommend-row { position: relative; display: flex; align-items: center; gap: 14px; min-height: 116px; padding: 12px 14px 12px 18px; border-radius: 15px; overflow: hidden; background: rgba(0,0,0,0.29); border: 1px solid rgba(255,255,255,0.10); box-shadow: inset 0 1px 0 rgba(255,255,255,0.035); }
.diff-bar { position: absolute; inset: 10px auto 10px 6px; width: 4px; border-radius: 9999px; background: var(--diff-color); box-shadow: 0 0 10px color-mix(in srgb, var(--diff-color) 45%, transparent); }
.step-badge { position: absolute; top: 7px; left: 8px; z-index: 2; display: grid; place-items: center; width: 22px; height: 22px; border-radius: 9999px; background: var(--diff-color); color: #fff; font-family: 'Torus',sans-serif; font-weight: 900; font-size: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.35); }
.cover { flex: 0 0 88px; width: 88px; height: 88px; object-fit: cover; border-radius: 12px; box-shadow: 0 0 0 3px rgba(255,255,255,0.78), 0 7px 16px -8px rgba(0,0,0,0.8); }
.content { flex: 1 1 auto; }
.song-title { flex: 1 1 auto; min-width: 0; font-family: 'SEGA NewRodin','Microsoft YaHei',sans-serif; font-weight: 800; font-size: 20px; color: #fff; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; text-shadow: 0 2px 3px rgba(0,0,0,0.45); }
.type-chip, .diff-chip, .action-chip { flex: 0 0 auto; white-space: nowrap; border-radius: 9999px; }
.type-chip { font-family: 'Torus',sans-serif; font-weight: 900; font-size: 10px; color: #ffbd47; border: 1px solid #ffbd4777; padding: 1px 6px; }
.diff-chip { font-family: 'SEGA NewRodin',sans-serif; font-weight: 900; font-size: 11px; border: 1px solid; padding: 1px 7px; background: rgba(0,0,0,0.22); }
.action-chip { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 10px; color: rgba(255,255,255,0.66); background: rgba(255,255,255,0.08); padding: 2px 7px; }
.metrics { display: flex; align-items: center; gap: 18px; }
.metric { display: flex; align-items: baseline; gap: 6px; min-width: 0; font-family: 'Torus','Microsoft YaHei',sans-serif; font-variant-numeric: tabular-nums; white-space: nowrap; }
.achievement-metric { flex: 1 1 auto; }
.rating-metric { flex: 0 0 auto; }
.metric-label { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 10px; color: rgba(255,255,255,0.4); margin-right: 2px; }
.metric-old { font-weight: bold; font-size: 14px; color: rgba(255,255,255,0.52); }
.metric-main { font-weight: 900; font-size: 18px; color: #fff; }
.arrow { font-size: 12px; color: rgba(255,255,255,0.28); }
.gain { font-weight: 900; font-size: 14px; color: #65efad; }
.subline { display: flex; align-items: center; justify-content: space-between; gap: 12px; min-width: 0; }
.fit-info, .replacement { font-family: 'Torus','Microsoft YaHei',sans-serif; font-weight: bold; font-size: 10.5px; color: rgba(255,255,255,0.48); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.replacement { text-align: right; color: rgba(255,255,255,0.38); }
.muted { opacity: 0.7; }
</style>
