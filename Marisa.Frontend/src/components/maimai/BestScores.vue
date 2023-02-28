<template>
    <div class="best-body">
        <div :style="`background-image: url('/assets/maimai/pic/UI_UNL_BG.png')`"
             class="bg-center bg-no-repeat bg-cover w-best">
            <div class="font-osu-web">
                <div class="h-[650px] bg-cover bg-bottom flex items-center justify-center"
                     :style="`background-image:url('/assets/maimai/pic/Sub.png')`">
                    <img src="/assets/maimai/pic/name.png" alt class="absolute h-[450px]">
                    <div class="w-[800px] h-[400px] bg-cover pb-[50px] px-[130px] relative">
                        <div style="overflow-wrap: anywhere"
                             class="text-8xl font-bold overflow-hidden w-full h-full flex justify-center items-center text-center"
                             :class="{'rainbow-text-shadow' : ra_old + ra_new >= (b50 ? 15000 : 8500)}"
                        >
                            {{ json.nickname }}
                        </div>
                        <div class="absolute text-6xl -top-3 left-0 right-0 text-center"
                             :class="{'rainbow-text-shadow': ra_old + ra_new >= (b50 ? 15000 : 8500)}">
                            {{ ra_old + ra_new }}
                        </div>
                        <div class="absolute text-4xl top-12 left-0 right-0 text-center mt-2 font-bold">
                            {{ ra_old }}+{{ ra_new }}
                        </div>
                    </div>
                </div>
                <div
                    class="w-[var(--best-width)] h-[calc(var(--best-gap)_*_1.5)] overflow-x-hidden bg-center flex -mt-[100px] z-10">
                    <img :src="`/assets/maimai/pic/UI_TST_BG_Parts_01.png`" alt>
                    <img :src="`/assets/maimai/pic/UI_TST_BG_Parts_01.png`" alt>
                </div>
            </div>
            <div>
                <div class="grid grid-cols-5-maimai p-card gap-card">
                    <score-card v-for="(data, i) in json.charts.sd" v-bind:key="i" v-bind="data"/>
                </div>
                <div class="px-[var(--card-padding)]">
                    <div class="h-gap overflow-x-hidden bg-center flex">
                        <img :src="`/assets/maimai/pic/UI_RSL_BG_Parts_01.png`" alt>
                        <img :src="`/assets/maimai/pic/UI_RSL_BG_Parts_01.png`" alt>
                    </div>
                </div>
                <div class="grid grid-cols-5-maimai p-card gap-card">
                    <score-card v-for="(data, i) in json.charts.dx" v-bind:key="i" v-bind="data"/>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import {computed, onMounted, ref} from 'vue';
import axios from 'axios';
import {useRoute} from "vue-router";

import ScoreCard from "@/components/maimai/ScoreCard.vue"
import {maimai_newRa} from '@/GlobalVars'
import j from '@/assets/maimai/test_b40.json'

const route = useRoute()

let json = ref(j)
let username = ref(route.query.username)
let qq = ref(route.query.qq)
let b50 = ref(route.query.b50 != null)


let ra_old = computed(() => {
    if (b50.value) {
        return json.value.charts.sd.reduce((ra, cur) => ra + cur.ra, 0);
    } else {
        return json.value.charts.sd.reduce((ra, cur) => ra + cur.ra, 0) + json.value.charts.dx.reduce((ra, cur) => ra + cur.ra, 0);
    }
})

let ra_new = computed(() => {
    if (b50.value) {
        return json.value.charts.dx.reduce((ra, cur) => ra + cur.ra, 0);
    } else {
        return json.value.additional_rating
    }
});

function calc_b50_ra() {
    let ds = json.value.charts.dx.map(x => x.ds)
    let ach = json.value.charts.dx.map(x => x.achievements)

    ds = ds.concat(json.value.charts.sd.map(x => x.ds))
    ach = ach.concat(json.value.charts.sd.map(x => x.achievements))

    let ds_str = ds.map(x => 'constants=' + x).join('&')
    let ach_str = ach.map(x => 'achievements=' + x).join('&')

    return axios.get(maimai_newRa + '?' + ds_str + '&' + ach_str)
}

onMounted(() => {
    axios.post('https://www.diving-fish.com/api/maimaidxprober/query/player', username.value != null ? {
        username: username.value,
        b50: b50.value ? true : undefined
    } : {
        qq: qq.value,
        b50: b50.value ? true : undefined
    }).then(data => {
        json.value = data.data
        if (b50.value) {
            calc_b50_ra().then(data => {
                for (let i = 0; i < json.value.charts.dx.length; i++) {
                    json.value.charts.dx[i].ra = data.data[i + json.value.charts.sd.length]
                }
                for (let i = 0; i < json.value.charts.sd.length; i++) {
                    json.value.charts.sd[i].ra = data.data[i]
                }
            }).catch(err => {
                console.log(err)
            });
        }
    }).catch(err => {
        console.log(err)
        document.body.innerHTML = err.response.status + ': ' + err.response.data.message
    })
})
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