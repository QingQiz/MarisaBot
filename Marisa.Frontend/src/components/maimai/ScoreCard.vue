<template>
    <div class="relative w-[400px] h-[200px] ">
        <!-- cover -->
        <fallback-image
            ref="imgEl" @load="UpdateColor"
            :src="available_cover" fallback="/assets/maimai/cover/0.png"
            class="inline w-[200px] h-[200px] rounded-r-3xl ml-[200px]"/>
        <div class="absolute top-0 right-0 left-0 right-0 w-[400px] h-[200px] rounded-l-3xl"
             :style="['background-image:' + bgColor]">
            <div class="font-osu-web mt-4 ml-5 relative flex flex-col gap-[2px]" :style="['color:' + textColor]">
                <div class="text-3xl flex w-fit justify-center items-center" :style="`background-color: ${level_color}`">
                    <div class="text-center bg-gray-400 text-black font-bold w-[65px] m-[1.5px] px-[5px]">
                        {{ ds.toFixed(1) }}
                    </div>
                    <div class="text-center w-[65px] text-white pl-[2.5px] px-[5px] font-bold">{{ ra }}</div>
                </div>
                <!-- title -->
                <div class="tracking-wider pb-2 text-4xl w-400 whitespace-nowrap overflow-hidden overflow-ellipsis">
                    {{ title }}
                </div>
                <!-- 私の中の幻想的世界観及びその顕現を想起させたある現実での出来事に関する一考察-->
                <!-- achievement -->
                <div class="tracking-wide flex items-end -mt-4">
                    <span class="text-6xl font-bold">
                        {{ achievements < 100 ? '0' : '' }}{{ achievements.toString().split('.')[0] }}.
                    </span>
                    <span class="text-4xl mb-[2px]">
                        {{ achievements.toFixed(4).split('.')[1] }}
                    </span>
                </div>
                <!-- footer -->
                <div class="h-fit flex gap-1.5 items-center mt-1">
                    <img :src="`/assets/maimai/pic/type_${type.toLowerCase()}.png`" alt class="h-[35px]">
                    <img :src="`/assets/maimai/pic/rank_${rate}.png`" alt class="inline-block h-[35px]">
                    <img :src="`/assets/maimai/pic/icon_${fc === '' ? 'blank' : fc}.png`" alt class="h-[35px]">
                    <img :src="`/assets/maimai/pic/icon_${fs === '' ? 'blank' : fs}.png`" alt class="h-[35px]">
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts">
import {defineComponent} from 'vue';

import FallbackImage from "@/components/utils/FallbackImage.vue";

export default defineComponent({
    name: 'ScoreCard',
    components: {FallbackImage},
    props: {
        song_id: Number,

        title: String,
        type: String,
        level_index: Number,

        achievements: Number,

        rate: String,
        ds: Number,
        ra: Number,

        fs: String,
        fc: String,
    },
    data() {
        return {
            img_color: {r: -1, g: -1, b: -1},
            level_colors: [
                '#52e72b',
                '#ffa801',
                '#ff5a66',
                '#c64fe4',
                '#dbaaff'
            ],
        }
    },
    computed: {
        level_color() {
            return this.level_colors[this.level_index ?? 0]
        },
        textColor() {
            if (this.img_color.r * 0.299 + this.img_color.g * 0.587 + this.img_color.b * 0.114 > 186) {
                return '#000000'
            } else {
                return '#ffffff';

            }
        },
        bgColor() {
            return `linear-gradient(to right, ${this.MakeRgba(this.img_color, 1)} 50%, ${this.MakeRgba(this.img_color, 0)})`
        },
        available_cover() {
            return [
                `/assets/maimai/cover/${this.song_id}.png`,
                `/assets/maimai/cover/${this.song_id}.jpg`,
                `/assets/maimai/cover/${(this.song_id ?? 0) + 10000}.jpg`,
                `/assets/maimai/cover/${(this.song_id ?? 0) + 10000}.png`,
                `/assets/maimai/cover/${(this.song_id ?? 0) - 10000}.jpg`,
                `/assets/maimai/cover/${(this.song_id ?? 0) - 10000}.png`,
            ]
        },
    },
    methods: {
        UpdateColor() {
            this.img_color = (this.$refs.imgEl as typeof FallbackImage).GetAverageRGB(0, 0, 12, 200)
        },
        MakeRgba(rgb: { r: number, g: number, b: number }, a?: number) {
            if (a == undefined) {
                return `rgb(${rgb.r},${rgb.g},${rgb.b})`
            }
            return `rgba(${rgb.r},${rgb.g},${rgb.b},${a})`
        },
    }
});
</script>