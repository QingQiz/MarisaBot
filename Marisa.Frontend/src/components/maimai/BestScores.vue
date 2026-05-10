<template>
    <template v-if="data_fetched">
        <div class="best-shell relative overflow-hidden" v-if="err_msg === ''">
            <!-- Backdrop: maimai でらっくす PRiSM PLUS pastel vertical gradient -->
            <div class="absolute inset-0 pointer-events-none mai-deco-bg"></div>

            <div class="relative w-best font-osu-web mai-text-shadow">
                <!-- Header -->
                <header class="relative px-card-x pt-16 pb-14 flex items-end justify-between gap-10 text-white">
                    <!-- Left: maimai でらっくす PRiSM PLUS logo -->
                    <img src="/assets/maimai/pic/logo_prism.png" alt="maimai でらっくす PRiSM PLUS"
                         class="w-[675px] shrink-0 drop-shadow-[0_6px_12px_rgba(80,30,90,0.45)]"
                         style="transform: translateY(-10px)"/>

                    <!-- Right: rating + breakdown -->
                    <div class="text-right shrink-0">
                        <div class="text-[11rem] leading-none font-black tabular-nums tracking-tight">
                            <template v-if="total_ra >= 15000">
                                <span v-for="(ch, i) in totalRaChars" :key="i"
                                      class="mai-rainbow-char"
                                      :class="[
                                          `mai-rainbow-char--c${i % 6}`,
                                          { 'mai-rainbow-char--banded': total_ra >= 16000 },
                                      ]">{{ ch }}</span>
                            </template>
                            <span v-else>{{ total_ra }}</span>
                        </div>
                        <div class="text-3xl font-semibold mt-3 tabular-nums flex items-baseline justify-end gap-3">
                            <span>{{ ra_old }}</span>
                            <span class="text-white/70">+</span>
                            <span>{{ ra_new }}</span>
                            <span class="text-base text-white/85 ml-3 uppercase tracking-[0.3em]">old · new</span>
                        </div>
                    </div>

                    <!-- Center (absolute, page-centered): subtitle + nickname -->
                    <div class="absolute left-1/2 -translate-x-1/2 bottom-[71px] flex flex-col items-center text-center pointer-events-none">
                        <div class="text-2xl uppercase tracking-[0.5em] font-bold mb-2 mai-subtitle">
                            maimai DX · best 50
                        </div>
                        <div :style="{ fontSize: nicknameFontSize }"
                             class="leading-none font-extrabold tracking-tight whitespace-nowrap">
                            <template v-if="total_ra >= 15000">
                                <span v-for="(ch, i) in nicknameChars" :key="i"
                                      class="mai-rainbow-char"
                                      :class="[
                                          `mai-rainbow-char--c${i % 6}`,
                                          { 'mai-rainbow-char--banded': total_ra >= 16000 },
                                      ]">{{ ch }}</span>
                            </template>
                            <span v-else>{{ json.nickname }}</span>
                        </div>
                    </div>
                </header>

                <!-- B35 (Old) section — "Old" rather than "Standard" because old-version
                     charts can include DX charts from previous releases too. -->
                <section class="px-card-x">
                    <div class="flex items-baseline gap-5 pb-7">
                        <span class="mai-section-tag bg-[#f93eac]">B35</span>
                        <div class="text-lg uppercase tracking-[0.3em] font-bold text-white drop-shadow-[0_2px_3px_rgba(160,30,90,0.55)]">Old</div>
                        <div class="flex-1 h-1 bg-white/55 rounded-full self-center"></div>
                        <div class="text-2xl tabular-nums text-white font-bold drop-shadow-[0_2px_3px_rgba(160,30,90,0.55)]">{{ ra_old }}</div>
                    </div>
                    <div class="grid grid-cols-5-maimai gap-card pb-12">
                        <score-card v-for="(data, i) in json.charts.sd" v-bind:key="i" :score="data"/>
                    </div>
                </section>

                <!-- B15 (New) section -->
                <section class="px-card-x">
                    <div class="flex items-baseline gap-5 pb-7">
                        <span class="mai-section-tag bg-[#6dbefe]">B15</span>
                        <div class="text-lg uppercase tracking-[0.3em] font-bold text-white drop-shadow-[0_2px_3px_rgba(20,40,120,0.55)]">NEW</div>
                        <div class="flex-1 h-1 bg-white/55 rounded-full self-center"></div>
                        <div class="text-2xl tabular-nums text-white font-bold drop-shadow-[0_2px_3px_rgba(20,40,120,0.55)]">{{ ra_new }}</div>
                    </div>
                    <div class="grid grid-cols-5-maimai gap-card pb-[70px]">
                        <score-card v-for="(data, i) in json.charts.dx" v-bind:key="i" :score="data"/>
                    </div>
                </section>
            </div>
        </div>
        <div v-else class="w-[1000px] h-[700px] flex items-center justify-center bg-red-600">
            <div class="text-white font-bold font-osu-web text-8xl text-center break-all">
                {{ err_msg }}
            </div>
        </div>
    </template>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue';
import axios from 'axios';
import {useRoute} from "vue-router";

import ScoreCard from "@/components/maimai/partial/ScoreCard.vue"
import {context_get} from '@/GlobalVars'
import {MaiMaiRating} from "@/components/maimai/utils/best_t";

const route = useRoute()
let json    = ref({} as MaiMaiRating)
let id      = ref(route.query.id)
let err_msg = ref('')

let data_fetched = ref(false)

let ra_old = ref(NaN)
let ra_new = ref(NaN)

const total_ra = computed(() => ra_old.value + ra_new.value)

const nicknameChars = computed(() => Array.from(json.value?.nickname ?? ''))
const totalRaChars  = computed(() => Array.from(String(total_ra.value)))

// Auto-shrink nickname so long names don't collide with logo / rating columns.
// Fullwidth chars count as 1.0, halfwidth ~0.55 — matches their actual rendered width ratio.
const nicknameFontSize = computed(() => {
    const nick = json.value?.nickname ?? ''
    let weight = 0
    for (const c of nick) {
        weight += (c.codePointAt(0) ?? 0) > 0xFF ? 1.0 : 0.55
    }
    if (weight <= 5)  return '8rem'
    if (weight <= 7)  return '7rem'
    if (weight <= 9)  return '6rem'
    if (weight <= 12) return '5rem'
    if (weight <= 16) return '4rem'
    return '3rem'
})

axios.get(context_get, {params: {id: id.value, name: 'b50'}}).then(data => {
    json.value   = ParseMaiMaiRating(data.data)
    ra_old.value = json.value.charts.sd.reduce((ra, cur) => ra + cur.ra, 0);
    ra_new.value = json.value.charts.dx.reduce((ra, cur) => ra + cur.ra, 0);
}).catch(err => {
    if (axios.isAxiosError(err) && err.response) {
        err_msg.value = `${err.response.status}: ${err.response.data?.message ?? err.message}`
        return
    }

    err_msg.value = err instanceof Error ? err.message : '加载 b50 失败'
}).finally(() => {
    data_fetched.value = true
})

function ParseMaiMaiRating(payload: unknown): MaiMaiRating {
    const parsed = typeof payload === 'string' ? JSON.parse(payload) : payload

    if (!IsMaiMaiRating(parsed)) {
        throw new Error('b50 数据格式错误')
    }

    return parsed
}

function IsMaiMaiRating(payload: unknown): payload is MaiMaiRating {
    if (payload == null || typeof payload !== 'object') {
        return false
    }

    const rating = payload as Partial<MaiMaiRating>

    return typeof rating.nickname === 'string'
        && rating.charts != null
        && Array.isArray(rating.charts.sd)
        && Array.isArray(rating.charts.dx)
}
</script>

<style scoped>
.best-shell {
    --card-gap: 1.75rem;
    --card-padding: 3rem;
    --best-width-inner: calc(400px * 5 + var(--card-gap) * 4);
    --best-width: calc(var(--best-width-inner) + var(--card-padding) * 2);
    background-color: #ffd5cf;
}

/* PRiSM PLUS pastel vertical gradient (peach bottom → pink → lavender → blue → mint top) */
.mai-deco-bg {
    background-image: linear-gradient(0deg,
        #ffd5cf 0%,
        #ffd5cf 31%,
        #ffc5d5 45%,
        #eaabff 61%,
        #72bcfe 86%,
        #65f2df 100%);
}

.mai-text-shadow {
    text-shadow: 0 2px 4px rgba(160, 30, 90, 0.45),
                 0 1px 2px rgba(0, 0, 0, 0.2);
}

.mai-section-tag {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 0.25rem 0.85rem;
    border-radius: 9999px;
    color: #fff;
    font-weight: 800;
    font-size: 1.5rem;
    letter-spacing: 0.05em;
    box-shadow: 0 0 0 3px #fff,
                0 4px 14px rgba(0, 0, 0, 0.25);
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
}

.grid-cols-5-maimai {
    grid-template-columns: repeat(5, minmax(400px, 400px));
}

.gap-card {
    gap: var(--card-gap);
}

.px-card-x {
    padding-left: var(--card-padding);
    padding-right: var(--card-padding);
}

.w-best {
    width: var(--best-width);
}

/* Subtitle "MAIMAI DX · BEST 50" — dark navy on the light pastel top of the gradient */
.mai-subtitle {
    color: #1e293b;
    text-shadow: 0 1px 2px rgba(255,255,255,0.6);
}

/* Rainbow-tier base (rating ≥ 15000) — per-char solid color cycle. The 6 slots
   below cycle through bright/main/dark color triplets; this base applies only
   the main color, used for the 15000–15999 mid-tier. */
.mai-rainbow-char {
    display: inline-block;
    /* Drop tabular-nums inherited from the rating container — narrow digits
       like "1" otherwise sit inside a wide fixed-width box and the diagonal
       bands of the --banded variant land on whitespace that background-clip:text
       masks away. Apply uniformly so both tiers stay visually consistent. */
    font-variant-numeric: normal;
    background-image: linear-gradient(0deg, var(--rb-color), var(--rb-color));
    -webkit-background-clip: text;
    background-clip: text;
    -webkit-text-fill-color: transparent;
    color: transparent;
    -webkit-text-stroke: 4px #000;
    paint-order: stroke fill;
    filter: drop-shadow(0 4px 8px rgba(0, 0, 0, 0.4)) saturate(1.85) brightness(1.2);
}

/* Banded variant (rating ≥ 16000) — three discrete bands at a true 45° diagonal
   (light → main → dark, hard stops), approximating the in-game logo letter
   style without smooth gradients. */
.mai-rainbow-char--banded {
    background-image: linear-gradient(135deg,
        var(--rb-color-light) 0%,  var(--rb-color-light) 43%,
        var(--rb-color)       43%, var(--rb-color)       68%,
        var(--rb-color-dark)  68%, var(--rb-color-dark)  100%);
}

/* c4 (indigo-violet) needs both filters disabled to render its blue hex truly:
   - saturate(1.85) brightness(1.2) on .mai-rainbow-char shifts blue toward magenta-pink
   - the magenta text-shadow inherited from .mai-text-shadow blends with high-B
     glyphs at edges, also shifting perceived color toward purple
   Other colors keep both effects so they retain the V8 vibrancy + halo. */
.mai-rainbow-char--c4 {
    filter: drop-shadow(0 4px 8px rgba(0, 0, 0, 0.4));
    text-shadow: none;
}

/* Per-color light / main / dark triplet, hand-tuned through pixel-level
   comparison with the official maimai でらっくす logo letters. */
.mai-rainbow-char--c0 { --rb-color-light: #FF8F9A; --rb-color: #FF0028; --rb-color-dark: #70000B; }  /* red */
.mai-rainbow-char--c1 { --rb-color-light: #FF974C; --rb-color: #FF6A00; --rb-color-dark: #702F00; }  /* orange */
.mai-rainbow-char--c2 { --rb-color-light: #FFF580; --rb-color: #FFE800; --rb-color-dark: #BDAD00; }  /* yellow */
.mai-rainbow-char--c3 { --rb-color-light: #33FF6E; --rb-color: #00E040; --rb-color-dark: #00B233; }  /* green */
.mai-rainbow-char--c4 { --rb-color-light: #9477E3; --rb-color: #6553FF; --rb-color-dark: #6132E3; }  /* indigo-violet */
.mai-rainbow-char--c5 { --rb-color-light: #F1B3FF; --rb-color: #D000FF; --rb-color-dark: #70008A; }  /* purple */
</style>
