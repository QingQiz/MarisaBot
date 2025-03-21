<script setup lang="ts">
import axios from "axios";
import {context_get} from "@/GlobalVars";
import {ref} from "vue";
import ScoreCard from "@/components/chunithm/partial/ScoreCard.vue";
import {range} from "@/utils/list";
import {shuffle} from "d3";
import {useRoute} from "vue-router";
import {ToFixedNoRound} from "@/utils/str";

const route = useRoute()
const id    = ref(route.query.id)
const b50   = ref(route.query.b50)

let best         = ref({} as any)
let data_fetched = ref(false);

axios.get(context_get, {params: {id: id.value, name: 'rating'}})
    .then(data => {
        best.value         = data.data;
        data_fetched.value = true
    });

function GetB30Ra() {
    return (best.value.records.b30 as []).reduce((ra, cur: { ra: number }) => ra + cur.ra, 0) / 30;
}

function GetR10Ra() {
    return (best.value.records.r10 as []).reduce((ra, cur: { ra: number }) => ra + cur.ra, 0) / (b50.value ? 20 : 10);
}
</script>

<template>
    <template v-if="data_fetched">
        <div style="background-image: url('/assets/chunithm/pic/bg.png')"
             class="p-10 bg-center bg-cover flex flex-col gap-5 rounded-3xl items-center relative -z-50">
            <img src="/assets/chunithm/pic/kv_pc.png" class="absolute -z-10" alt="bg_ch">
            <div class="bg-mask"></div>
            <div class="h-[800px] text-5xl flex w-full bg-top bg-cover relative">
                <img :src="`/assets/chunithm/pic/kv_logo.png`"
                     class="absolute object-cover right-0 w-[550px] mt-5 -z-20" alt="logo">
                <div class="info-card">
                    <div class="avatar">
                        <img :src="`/assets/chunithm/pic/logo.png`" alt="avatar">
                    </div>
                    <div class="info-card-detail shrink">
                        <div class="nickname font-osu-web">
                            {{ best.nickname }}
                        </div>
                        <div class="flex flex-col gap-1">
                            <div class="flex gap-2">
                                <div class="my-2 w-[15px] bg-black"></div>
                                <div class="text-[50px]">RATING: {{ ToFixedNoRound(best.rating, 2) }}</div>
                            </div>
                            <div class="flex gap-5">
                                <div class="w-[210px] min-w-[210px]">
                                    <div class="flex gap-2 font-console">
                                        <div class="my-1 w-[15px] bg-gray-500"></div>
                                        <div class="text-[33px]">B30: {{ ToFixedNoRound(GetB30Ra(), 2) }}</div>
                                    </div>
                                    <div class="flex gap-2 font-console">
                                        <div class="my-1 w-[15px] bg-gray-500"></div>
                                        <div class="text-[33px]">
                                            {{ b50 ? "N20" : "R10" }}: {{ ToFixedNoRound(GetR10Ra(), 2) }}
                                        </div>
                                    </div>
                                </div>
                                <div class="datasource" v-if="best.DataSource">
                                    {{ best.DataSource }}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            <div class="grid grid-cols-5-chu card-gap">
                <score-card v-for="(data, i) in best.records.b30" v-bind:key="i" :score="data"/>
            </div>
            <div class="flex w-full justify-between">
                <div class="splitter-img-box" v-for="i in shuffle(range(1, 17))">
                    <img :src="`/assets/chunithm/pic/ch_${i}.png`" class="-scale-x-100" alt="分割线">
                </div>
            </div>
            <div class="grid grid-cols-5-chu card-gap">
                <score-card v-for="(data, i) in best.records.r10" v-bind:key="i" :score="data"/>
            </div>
        </div>
    </template>
</template>

<style scoped lang="postcss">
.grid-cols-5-chu {
    grid-template-columns: repeat(5, minmax(400px, 400px));
}

.card-gap {
    gap: 2rem;
}

.splitter-img-box {
    @apply flex justify-center;
    width: 125px;
    height: 125px;

    img {
        width: auto;
        height: 100%;
        object-fit: cover;
    }
}

.datasource {
    @apply font-arial italic text-gray-400 text-opacity-40;
    @apply overflow-hidden overflow-ellipsis text-nowrap;
    font-size: 70px;
    font-weight: bolder;
}

.nickname {
    @apply overflow-hidden overflow-ellipsis text-nowrap font-bold;
    font-size: 80px;
}

.info-card {
    @apply w-fit h-fit flex bg-cover bg-center mt-16 shadow-xl;
    @apply bg-opacity-60 bg-black;
    border-radius: 5rem;
    max-width: 2000px;
    min-width: 800px;
}

.info-card-detail {
    @apply text-white my-12 flex flex-col place-content-between mr-12;
    @apply font-osu-web;
    max-width: 1600px;

}

.avatar {
    @apply bg-gray-400 bg-opacity-75 rounded-full m-5 -scale-x-100;
    min-width: 300px;

    img {
        @apply w-full h-full object-cover;
    }
}

.bg-mask {
    @apply absolute inset-0;
    background-image: linear-gradient(to bottom, #ffffff00 0%, #ffffff00 28%, #ffffff6f 30%, #ffffff6f 100%)
}

</style>