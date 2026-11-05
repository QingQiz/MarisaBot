<template>
    <div class="sc" :style="{'--lv': level_color}">
        <div class="ambient"><fallback-image :src="covers" fallback="/assets/maimai/cover/0.png" alt=""/></div>
        <div class="veil"></div>

        <div class="head">
            <div class="title">{{ score.title }}</div>
            <span class="score-tag">
                <span class="ds" :class="{light: score.level_index === 4}">{{ score.ds.toFixed(1) }}</span>
                <span class="ra">{{ score.ra }}</span>
            </span>
        </div>

        <div class="jacket">
            <div class="jacket-img"><fallback-image :src="covers" fallback="/assets/maimai/cover/0.png" alt=""/></div>
            <span class="rank-no">#{{ index }}</span>
            <img :src="`/assets/maimai/pic/type_${score.type.toLowerCase()}.png`" class="type-tag" alt="">
        </div>

        <div class="body">
            <div class="ach-row">
                <span class="ach">
                    <span class="ach-int">{{ Math.floor(score.achievements) }}</span><span class="ach-frac">.{{ score.achievements.toFixed(4).split('.')[1] }}</span><span class="ach-pct">%</span>
                </span>
                <img :src="`/assets/maimai/pic/rank_summary_${score.rate}.png`" class="rank" alt="">
            </div>

            <div class="badges">
                <img :src="`/assets/maimai/pic/icon_${score.fc || 'blank'}.png`" class="icon" alt="">
                <img :src="`/assets/maimai/pic/icon_${score.fs || 'blank'}.png`" class="icon" alt="">
                <img v-if="dx_star" :src="`/assets/maimai/pic/music_icon_dxstar_${dx_star}.png`" class="star" alt="">
                <span v-else class="star star-blank"></span>
                <span class="dx"><b>{{ score.dxScore }}</b><small>/{{ score.max_dx }}</small></span>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import {computed} from "vue";
import FallbackImage from "@/components/utils/FallbackImage.vue";
import {maimai_alternativeCover, maimai_levelColors} from "@/GlobalVars";
import {Score as MaiScore} from "@/components/maimai/utils/best_t";
import {dxScoreStar} from "@/components/maimai/utils/ordinal";

const props = defineProps<{ score: MaiScore, index: number }>()

const level_color = computed(() => maimai_levelColors[props.score.level_index ?? 0])
const covers = computed(() => maimai_alternativeCover(props.score.song_id ?? 0))
const dx_star = computed(() => dxScoreStar(props.score.dxScore, props.score.max_dx))
</script>

<style scoped>
.sc {
    --ink: #2b2147;
    --edge: 16px;
    position: relative;
    width: 412px;
    height: 180px;
    border-radius: 24px;
    overflow: hidden;
    isolation: isolate;
    background: #fff;
    box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.9),
                0 14px 30px -14px rgba(84, 36, 110, 0.55);
    color: var(--ink);
}

.ambient {
    position: absolute;
    inset: -40px;
    filter: blur(26px) saturate(1.6);
    opacity: 0.55;
    z-index: -2;
}

.veil {
    position: absolute;
    inset: 0;
    z-index: -1;
    background: linear-gradient(90deg, rgba(255, 255, 255, 0.35) 0%, rgba(255, 255, 255, 0.72) 40%, rgba(255, 255, 255, 0.8) 100%);
    box-shadow: inset 0 1.5px 0 rgba(255, 255, 255, 0.95);
    border-radius: inherit;
}

.head {
    position: absolute;
    left: 18px;
    right: var(--edge);
    top: 7px;
    height: 44px;
    display: flex;
    align-items: center;
    gap: 10px;
}

.title {
    flex: 1;
    min-width: 0;
    font-family: 'SEGA NewRodin', 'Microsoft YaHei', sans-serif;
    font-weight: 900;
    font-size: 34px;
    line-height: 1.2;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    padding-block: 4px 0.2em;
    margin-block: -4px -0.2em;
}

/* 定数（难度色）+ RA（墨色）合成一枚双色胶囊。 */
.score-tag {
    flex-shrink: 0;
    display: flex;
    border-radius: 9999px;
    overflow: hidden;
    font: bold 21px/1.15 'Torus', sans-serif;
    letter-spacing: -0.02em;
    box-shadow: 0 3px 8px -2px color-mix(in srgb, var(--lv) 70%, transparent);
}

.ds {
    padding: 2px 6px 1px 9px;
    background: var(--lv);
    color: #fff;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
}

.ds.light {
    color: #5b2a86;
    text-shadow: none;
}

.ra {
    padding: 2px 9px 1px 6px;
    background: var(--ink);
    color: #fff;
}

.jacket {
    position: absolute;
    left: 14px;
    top: 62px;
    width: 104px;
    height: 104px;
    border-radius: 14px;
    box-shadow: 0 0 0 3px var(--lv), 0 8px 18px -6px rgba(0, 0, 0, 0.45);
}

.jacket-img {
    width: 100%;
    height: 100%;
    border-radius: 14px;
    overflow: hidden;
}

.ambient :deep(img),
.jacket-img :deep(img) {
    width: 100%;
    height: 100%;
    object-fit: cover;
}

.rank-no {
    position: absolute;
    left: -3px;
    top: -3px;
    min-width: 42px;
    padding: 2px 7px 1px 6px;
    border-radius: 14px 0 12px 0;
    background: var(--lv);
    color: #fff;
    font: bold 17px/1.1 'Torus', sans-serif;
    text-align: center;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

.type-tag {
    position: absolute;
    left: 5px;
    bottom: 5px;
    height: 19px;
    filter: drop-shadow(0 2px 3px rgba(0, 0, 0, 0.45));
}

.body {
    position: absolute;
    left: 132px;
    right: var(--edge);
    top: 60px;
    bottom: 12px;
    display: flex;
    flex-direction: column;
    justify-content: space-between;
}

.ach-row {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 6px;
}

.ach {
    font-family: 'Torus', sans-serif;
    font-weight: bold;
    line-height: 1;
    white-space: nowrap;
    letter-spacing: -0.02em;
}

.ach-int {
    font-size: 50px;
}

.ach-frac {
    font-size: 36px;
}

.ach-pct {
    font-size: 22px;
    margin-left: 1px;
    opacity: 0.6;
}

/* 评级图标无透明边距，底边落在基线上即与整数大字（字高约 32px）大致居中。 */
.rank {
    height: 29px;
}

.badges {
    display: flex;
    align-items: center;
    gap: 8px;
    height: 44px;
}

.badges .icon {
    height: 42px;
    filter: drop-shadow(0 1px 2px rgba(0, 0, 0, 0.3));
}

.dx {
    display: flex;
    align-items: center;
    font: bold 27px/1 'Torus', sans-serif;
    white-space: nowrap;
}

.dx small {
    font-size: 21px;
    opacity: 0.55;
}

.star {
    width: 42px;
    height: 42px;
    object-fit: contain;
}

/* 无星时用星形剪影占位，配色同 icon_blank（浅灰底 + 深一档描边）；剪影取 5 星图，1–4 星图右下两角被画布截平。 */
.star-blank {
    position: relative;
    background: #d4d5da;
    -webkit-mask: url(/assets/maimai/pic/music_icon_dxstar_5.png) center / contain no-repeat;
            mask: url(/assets/maimai/pic/music_icon_dxstar_5.png) center / contain no-repeat;
}

.star-blank::after {
    content: '';
    position: absolute;
    inset: 1.5px;
    background: #e9eaed;
    -webkit-mask: inherit;
            mask: inherit;
}
</style>
