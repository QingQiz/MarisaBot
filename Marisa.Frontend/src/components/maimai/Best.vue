<template>
    <div class="best-body">
        <div :style="`background-image: url('/assets/maimai/pic/UI_UNL_BG.png')`"
             class="bg-center bg-no-repeat bg-cover w-best">
            <div class="">
                <div class="h-[650px] bg-cover bg-bottom flex items-center justify-center"
                     :style="`background-image:url('/assets/maimai/pic/Sub.png')`">
                    <div class="w-[800px] h-[400px] bg-cover pb-[40px] pt-[80px] px-[120px] relative"
                         :style="`background-image:url('/assets/maimai/pic/name.png')`">
                        <div style="overflow-wrap: anywhere"
                             class="text-8xl font-sans font-bold overflow-hidden w-full h-full flex justify-center items-center"
                             :class="ra_old() + ra_new() >= (b50 ? 15000 : 8500) ? 'rainbow-text-shadow' : ''"
                        >
                            {{ json.nickname }}
                        </div>
                        <div class="absolute text-6xl top-5 left-0 right-0 text-center"
                             :class="ra_old() + ra_new() >= (b50 ? 15000 : 8500) ? 'rainbow-text-shadow' : ''">
                            {{ ra_old() + ra_new() }}
                        </div>
                        <div class="absolute text-4xl top-20 left-0 right-0 text-center mt-2 font-bold">
                            {{ ra_old() }}+{{ ra_new() }}
                        </div>
                    </div>
                </div>
                <div
                    class="w-[var(--best-width)] h-[calc(var(--best-gap)_*_1.5)] overflow-x-hidden bg-center flex -mt-[100px]">
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

<script lang="ts">
import {defineComponent} from 'vue';
import axios from 'axios';

import ScoreCard from "@/components/maimai/ScoreCard.vue"
import {maimai_newRa} from '@/GlobalVars'
import j from '../../assets/maimai/test_b50.json'

export default defineComponent({
    name: "BestScores",
    components: {ScoreCard},
    data() {
        return {
            json: j,
            username: this.$route.query.username,
            qq: this.$route.query.qq,
            b50: this.$route.query.b50 !== undefined
        }
    },
    methods: {
        ra_old() {
            if (this.b50) {
                return this.json.charts.sd.reduce((ra, cur) => ra + cur.ra, 0);
            } else {
                return this.json.charts.sd.reduce((ra, cur) => ra + cur.ra, 0) + this.json.charts.dx.reduce((ra, cur) => ra + cur.ra, 0);
            }
        },
        ra_new() {
            if (this.b50) {
                return this.json.charts.dx.reduce((ra, cur) => ra + cur.ra, 0);
            } else {
                return this.json.additional_rating
            }
        },
        calcRaNew() {
            let ds = this.json.charts.dx.map(x => x.ds)
            let ach = this.json.charts.dx.map(x => x.achievements)

            ds = ds.concat(this.json.charts.sd.map(x => x.ds))
            ach = ach.concat(this.json.charts.sd.map(x => x.achievements))

            let ds_str = ds.map(x => 'constants=' + x).join('&')
            let ach_str = ach.map(x => 'achievements=' + x).join('&')

            return axios.get(maimai_newRa + '?' + ds_str + '&' + ach_str)
        }
    },
    mounted() {
        axios.post('https://www.diving-fish.com/api/maimaidxprober/query/player', this.username !== undefined ? {
            username: this.username,
            b50: this.b50 ? true : undefined
        } : {
            qq: this.qq,
            b50: this.b50 ? true : undefined
        }).then(data => {
            this.json = data.data
            if (this.b50) {
                this.calcRaNew().then(data => {
                    for (let i = 0; i < this.json.charts.dx.length; i++) {
                        this.json.charts.dx[i].ra = data.data[i + this.json.charts.sd.length]
                    }
                    for (let i = 0; i < this.json.charts.sd.length; i++) {
                        this.json.charts.sd[i].ra = data.data[i]
                    }
                }).catch(err => {
                    console.log(err)
                });
            }
        }).catch(err => {
            console.log(err)
            document.body.innerHTML = err.response.status + ': ' + err.response.data.message
        })
    }
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

.h-best-t {
    height: var(--best-height-t);
}

.h-best-b {
    height: var(--best-height-b)
}

.rainbow-text {
    background-image: linear-gradient(to right, violet, indigo, blue, green, yellow, orange, red);
    color: transparent;
    -webkit-background-clip: text;
}

.rainbow-text-shadow {
    color: #ef3550;
    letter-spacing: 7px;
    text-shadow: 1px 0 #f48fb1, 2px 0 #7e57c2, 3px 0 #2196f3, 4px 0 #26c6da, 5px 0 #43a047, 6px 0 #eeff41, 7px 0 #f9a825, 8px 0 #ff5722, -1px 0 #f48fb1, -2px 0 #7e57c2, -3px 0 #2196f3, -4px 0 #26c6da, -5px 0 #43a047, -6px 0 #eeff41, -7px 0 #f9a825, -8px 0 #ff5722;
}
</style>