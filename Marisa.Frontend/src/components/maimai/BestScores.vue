<template>
    <template v-if="data_fetched">
        <div class="best-body" v-if="err_msg === ''">
            <div :style="`background-image: url('/assets/maimai/pic/UI_UNL_BG.png')`"
                 class="bg-center bg-no-repeat bg-cover w-best">
                <div class="font-osu-web">
                    <div class="h-[650px] bg-cover bg-bottom flex items-center justify-center relative"
                         :style="`background-image:url('/assets/maimai/pic/Sub.png')`">
                        <img src="/assets/maimai/pic/name.png" alt="" class="absolute h-[450px] m-auto inset-0">
                        <div class="w-[800px] h-[400px] pb-[50px] px-[130px] relative">
                            <div
                                class="text-6xl font-bold overflow-hidden w-full h-full flex justify-center items-center text-center"
                                :class="{ 'rainbow-text-shadow': ra_old + ra_new >= 15000 }">
                                {{ json.nickname }}
                            </div>
                            <div class="absolute text-6xl -top-3 left-0 right-0 text-center"
                                 :class="{ 'rainbow-text-shadow': ra_old + ra_new >= 15000 }">
                                {{ ra_old + ra_new }}
                            </div>
                            <div class="absolute text-4xl top-12 left-0 right-0 text-center mt-2 font-bold">
                                {{ ra_old }}+{{ ra_new }}
                            </div>
                        </div>
                    </div>
                    <div
                        class="w-[var(--best-width)] h-[calc(var(--best-gap)_*_1.5)] overflow-x-hidden bg-center flex -mt-[100px]">
                        <img :src="`/assets/maimai/pic/UI_TST_BG_Parts_01.png`" alt="" class="z-10">
                        <img :src="`/assets/maimai/pic/UI_TST_BG_Parts_01.png`" alt="" class="z-10">
                    </div>
                </div>
                <div>
                    <div class="grid grid-cols-5-maimai p-card gap-card">
                        <score-card v-for="(data, i) in json.charts.sd" v-bind:key="i" :score="data"/>
                    </div>
                    <div class="px-[var(--card-padding)]">
                        <div class="h-gap overflow-x-hidden bg-center flex">
                            <img :src="`/assets/maimai/pic/UI_RSL_BG_Parts_01.png`" alt="">
                            <img :src="`/assets/maimai/pic/UI_RSL_BG_Parts_01.png`" alt="">
                        </div>
                    </div>
                    <div class="grid grid-cols-5-maimai p-card gap-card">
                        <score-card v-for="(data, i) in json.charts.dx" v-bind:key="i" :score="data"/>
                    </div>
                </div>
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
import {ref} from 'vue';
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
.best-body {
    --card-gap: 1.75rem;
    --card-padding: 3rem;
    --best-gap: 150px;
    --best-width-inner: calc(400px * 5 + var(--card-gap) * 4);
    --best-width: calc(var(--best-width-inner) + var(--card-padding) * 2);
    --best-height-t: calc(200px * 5 + var(--card-padding) * 2 + var(--card-gap) * 4);
    --best-height-b: calc(200px * 3 + var(--card-padding) * 2 + var(--card-gap) * 4);
}

.grid-cols-5-maimai {
    grid-template-columns: repeat(5, minmax(400px, 400px));
}

.gap-card {
    gap: var(--card-gap);
}

.p-card {
    padding: var(--card-padding);
}

.w-best {
    width: var(--best-width);
}

.h-gap {
    height: var(--best-gap);
}

.rainbow-text-shadow {
    color: #ef3550;
    letter-spacing: 7px;
    text-shadow: 1px 0 #f48fb1, 2px 0 #7e57c2, 3px 0 #2196f3, 4px 0 #26c6da, 5px 0 #43a047, -1px 0 #f48fb1, -2px 0 #7e57c2, -3px 0 #2196f3, -4px 0 #26c6da, -5px 0 #43a047;
}
</style>