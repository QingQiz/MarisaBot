<template>
    <div class="flex bg" v-if="!errorMessage">
        <div class="flex flex-col h-full gap-5">
            <div class="cover-shadow">
                <div class="cover">
                    <div class="flex h-full">
                        <div class="diff-bar"/>
                        <div
                            class="py-5 pl-2.5 flex flex-col place-content-between text-white bg-black bg-opacity-5 osu-cover-text-shadow grow">
                            <!-- diff name -->
                            <div class="text-3xl">
                                {{ beatmap.version }}
                            </div>
                            <div class="flex flex-col place-content-between gap-7">
                                <!-- title -->
                                <div class="text-5xl">
                                    {{ beatmap.beatmapset.title_unicode }}
                                    <!-- artist -->
                                    <div class="text-3xl">
                                        {{ beatmap.beatmapset.artist_unicode }}
                                    </div>
                                </div>
                                <!-- mapper -->
                                <div class="font-bold text-2xl">
                                    mapped by <span class="text-[#65ccfe]">{{ beatmap.beatmapset.creator }}</span>
                                </div>
                                <!-- beatmap detail -->
                                <div class="song-info-with-icon">
                                    <!-- length -->
                                    <div>
                                        <img :src="`/assets/osu/icon-total_length.png`" alt="">
                                        {{ secondsToTime(beatmap.total_length) }}
                                    </div>
                                    <!-- bpm -->
                                    <div>
                                        <img :src="`/assets/osu/icon-bpm.png`" alt="">
                                        {{ beatmap.bpm }}
                                    </div>
                                    <!-- circles -->
                                    <div>
                                        <img :src="`/assets/osu/icon-count_circles.png`" alt="">
                                        {{ beatmap.count_circles }}
                                    </div>
                                    <!-- sliders -->
                                    <div>
                                        <img :src="`/assets/osu/icon-count_sliders.png`" alt="">
                                        {{ beatmap.count_sliders }}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="absolute right-10 top-5 flex flex-col gap-2 items-center">
                        <div class="diff-text">
                            {{ sr.toFixed(2) }}
                        </div>
                        <div class=rank-status>
                            {{ beatmap.status }}
                        </div>
                    </div>
                </div>
            </div>

            <div class="detail">
                <div class="font-bold underline underline-offset-4 decoration-2 text-[#ffd996]">
                    Details
                    <hr class="-mt-[1px] -z-1 border-gray-500 opacity-60"/>
                </div>
                <div class="bg-black bg-opacity-50 w-full grow text-white px-7 pt-7">
                    <div class="flex flex-col h-full gap-5">
                        <div class="grid grid-cols-2 gap-5">
                            <div class="flex flex-col gap-5 place-content-between">
                                <!-- beatmap detail -->
                                <div class="grid-detail place-content-between">
                                    <template v-for="val in beatmapDetail">
                                        <div>
                                            {{ val[0] }}
                                        </div>
                                        <div class="h-2 bg-[#808080] relative w-full">
                                            <div class="absolute h-2 left-0 top-0"
                                                 :style="`width: ${val[1] > 10 ? 100 : val[1] / 10 * 100}%; background-color: ${val[2]}`"/>
                                        </div>
                                        <div class="place-self-center">
                                            {{ val[1] }}
                                        </div>
                                    </template>
                                </div>
                                <!-- user rating -->
                                <div class="flex flex-col grow">
                                    <div class="text-center">
                                        User Rating
                                    </div>
                                    <div class="relative bg-[#88b300] w-full h-2">
                                        <div class="absolute bg-[#ea0] top-0 left-0 h-2"
                                             :style="`width: ${userRating[1] / userRating[0] * 100}%`">
                                        </div>
                                    </div>
                                    <div class="flex place-content-between">
                                        <div>{{ userRating[1] }}</div>
                                        <div>{{ userRating[2] }}</div>
                                    </div>
                                    <div class="text-center">
                                        Rating Spread
                                    </div>
                                    <div class="grid grid-cols-10 grow min-h-[100px]">
                                        <div class="relative h-full" v-for="x in userRating[3].slice(1)">
                                            <div :style="`height: ${x}%`"
                                                 class="bg-[#4ad] absolute bottom-0 left-0 right-0"/>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div class="flex flex-col place-content-between gap-5">
                                <!-- description -->
                                <div class="flex flex-col gap-5">
                                    <div>
                                        <div class="font-bold">
                                            Tags
                                        </div>
                                        <div class="text-[#29b]">
                                            {{ beatmap.beatmapset.tags }}
                                        </div>

                                    </div>
                                </div>
                                <!-- pp chart -->
                                <ManiaPpChart v-bind="score.statistics" :beatmapset-id="score.beatmapset.id"
                                              :beatmap-checksum="score.beatmap.checksum" :beatmap-id="score.beatmap.id"
                                              :mods="score.mods"
                                              v-if="score.mode_int === 3"/>
                            </div>
                        </div>
                        <!-- Points of Failure-->
                        <div class="flex flex-col grow min-h-[100px]">
                            <div class="font-bold">
                                Points of Failure
                            </div>
                            <div class="w-full self-center grid grow"
                                 :style="`grid-template-columns: repeat(${failureRating.length}, minmax(0, 1fr))`">
                                <div v-for="x in failureRating" class="point-of-failure">
                                    <div :style="`height: ${x[0]}%`"/>
                                    <div :style="`height: ${x[1]}%`"/>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
        <div class="profile_pos flex flex-col gap-5">
            <!-- profile -->
            <div class="profile-clip profile_wrapper">
                <div class="profile-size bg-center bg-cover flex flex-col p-5 place-content-between"
                     :style="`background-image: url(${user.cover_url})`">
                    <div
                        class="flex flex-row-reverse content-end gap-5 items-center bg-black bg-opacity-5 rounded-r-[60px]">
                        <img :src="user.avatar_url" height="160" width="160" alt="" class="rounded-[60px]">
                        <div class="text-white flex flex-col h-full place-content-between py-2">
                            <div class="text-7xl font-bold text-right">
                                {{ user.username }}
                            </div>
                            <div class="flex self-end items-end gap-2">
                                <span class="text-4xl">{{ user.country.name }}</span>
                                <img
                                    class="w-[70px] rounded-xl"
                                    :src="`https://purecatamphetamine.github.io/country-flag-icons/3x2/${user.country.code}.svg`"
                                    :alt="user.country.name">
                            </div>
                        </div>
                    </div>

                    <div class="grid grid-cols-4 grid-rows-2 text-white bg-black bg-opacity-40">
                        <div></div>
                        <div class="text-center text-2xl">pp</div>
                        <div class="text-center text-2xl">Global Rank</div>
                        <div class="text-center text-2xl">Local Rank</div>
                        <div></div>
                        <div class="text-center text-4xl">{{ user.statistics.pp }}</div>
                        <div class="text-center text-4xl">{{ user.statistics.global_rank }}</div>
                        <div class="text-center text-4xl">{{ user.statistics.country_rank }}</div>
                    </div>
                </div>
            </div>
            <div class="w-full grow flex flex-col gap-2">
                <div class="font-bold underline underline-offset-4 decoration-2 text-[#ffd996]">
                    Score
                    <hr class="-mt-[1px] -z-1 border-gray-500 opacity-60"/>
                </div>
                <div class="bg-black bg-opacity-50 w-full grow text-white p-7">
                    <div class="flex flex-col items-center gap-5 place-content-between h-full">
                        <div class="text-9xl text-center">
                            {{ score.score.toLocaleString() }}
                        </div>
                        <div class="flex gap-10 items-center">
                            <div
                                class="relative h-[200px] w-[200px] bg-cover bg-center flex justify-center items-center"
                                :style="`background-image: url(${osu_accRing_builder(score.accuracy, score.mode_int)})`">
                                <div class="absolute text-7xl font-osu-rank osu-rank-text-shadow mt-2">
                                    {{ score.rank.toUpperCase() }}
                                </div>
                            </div>
                            <div class="flex flex-col gap-10">
                                <div class="flex gap-2 items-center" v-if="score.mods">
                                    <img v-for="mod in score.mods" alt="" :src="osu_modIcon_builder(mod)"/>
                                </div>
                                <div class="text-4xl">
                                    <span class="text-[#65ccfe]">{{
                                            new Date(score.created_at).toLocaleString('zh-CN')
                                        }}</span>
                                </div>
                            </div>
                        </div>
                        <div class="flex flex-col w-full gap-5">
                            <div class="osu-grade-detail-table">
                                <div>
                                    <div>Accuracy</div>
                                    <div>{{ (score.accuracy * 100).toFixed(2) }}%</div>
                                </div>
                                <div v-if="score.mode_int === 3">
                                    <div>pp acc</div>
                                    <div>{{ (ppAcc * 100).toFixed(2) }}%</div>
                                </div>
                                <div>
                                    <div>max combo</div>
                                    <div>{{ score.max_combo }} /
                                        <div class="inline text-green-300">{{ beatmap.max_combo }}</div>
                                    </div>
                                </div>
                                <div>
                                    <div>pp</div>
                                    <div>{{ pp.toFixed(2) }}</div>
                                </div>
                            </div>

                            <div class="osu-grade-detail-table">
                                <!-- mania -->
                                <div v-if="score.mode_int === 3">
                                    <div class="text-[#97ecfd]">max</div>
                                    <div>{{ score.statistics.count_geki }}</div>
                                </div>
                                <!-- all -->
                                <div>
                                    <div class="text-[#66cafe]">300</div>
                                    <div>{{ score.statistics.count_300 }}</div>
                                </div>
                                <!-- mania / fruit -->
                                <div v-if="score.mode_int === 3 || score.mode_int === 2">
                                    <div class="text-[#b2d943]">{{ score.mode_int === 3 ? '200' : 'drp miss' }}</div>
                                    <div>{{ score.statistics.count_katu }}</div>
                                </div>
                                <!-- all -->
                                <div>
                                    <div class="text-[#86b002]">100</div>
                                    <div>{{ score.statistics.count_100 }}</div>
                                </div>
                                <!-- osu / mania -->
                                <div v-if="score.mode_int === 0 || score.mode_int === 3">
                                    <div class="text-[#ffd996]">50</div>
                                    <div>{{ score.statistics.count_50 }}</div>
                                </div>
                                <!-- all -->
                                <div>
                                    <div class="text-[#f51121]">miss</div>
                                    <div>{{ score.statistics.count_miss }}</div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div v-else>
        <div class="bg-red-500 text-white text-2xl w-[1000px]">
            <pre>{{ errorMessage }}</pre>
        </div>
    </div>
</template>

<script setup lang="ts">
import beatmap_info from '@/assets/osu/beatmap.json'
import user_info from '@/assets/osu/user.json'
import * as d3 from 'd3';
import {computed, ref} from "vue";
import {max} from "d3";
import axios from "axios";
import {
    osu_accRing_builder,
    osu_beatmapCover_builder, osu_beatmapInfo, osu_best,
    osu_modIcon_builder, osu_pp_builder,
    osu_recent,
    osu_userInfo,
    PpAcc
} from "@/GlobalVars";
import {Score} from "@/components/osu/osu.data";
import ManiaPpChart from "@/components/osu/ManiaPpChart.vue";
import {useRoute} from "vue-router";


const route = useRoute()
let name = ref(route.query.name)
let bpRank = ref(route.query.bpRank)
let mode = ref(route.query.mode)
let recent = ref(route.query.recent != null)
let fail = ref(route.query.fail != null)

const user = ref(user_info)
const score = ref((require('@/assets/osu/score.json') as Score[])[0]);
const beatmap = ref(beatmap_info)
const sr = ref(beatmap.value.difficulty_rating)
const pp = ref(score.value.pp)


const errorMessage = ref('')

axios.get(osu_userInfo, {params: {username: name.value, modeInt: mode.value}})
    .then(data => user.value = data.data)
    .then(_ => axios.get(recent.value ? osu_recent : osu_best, {
            params: {
                userId: user.value.id,
                modeInt: mode.value,
                bpRank: bpRank.value,
                fail: fail.value
            }
        })
    )
    .then(data => score.value = data.data)
    .then(_ => axios.get(osu_beatmapInfo, {params: {beatmapId: score.value.beatmap.id}}))
    .then(data => beatmap.value = data.data)
    .then(_ => axios.get(osu_pp_builder(score.value.beatmapset.id, score.value.beatmap.checksum, score.value.beatmap.id, score.value.mode_int, score.value.mods, score.value.accuracy, score.value.max_combo, score.value.statistics.count_geki, score.value.statistics.count_300, score.value.statistics.count_katu, score.value.statistics.count_100, score.value.statistics.count_50, score.value.statistics.count_miss, score.value.score)))
    .then(data => {
        pp.value = data.data.pp;
        sr.value = data.data.starRating
    })
    .catch(setErrorMessage);


// see https://github.com/cl8n/osu-web/blob/94e14a47fc2606b1f3ddf45acf86b2677b881aec/resources/assets/lib/utils/beatmap-helper.ts#L23
const difficultyColourSpectrum = d3.scaleLinear<string>()
    .domain([0.1, 1.25, 2, 2.5, 3.3, 4.2, 4.9, 5.8, 6.7, 7.7, 9])
    .clamp(true)
    .range(['#4290FB', '#4FC0FF', '#4FFFD5', '#7CFF4F', '#F6F05C', '#FF8068', '#FF4E6F', '#C645B8', '#6563DE', '#18158E', '#000000'])
    .interpolate(d3.interpolateRgb.gamma(2.2));

function getDiffColor(rating: number) {
    if (rating < 0.1) return '#AAAAAA';
    if (rating >= 9) return '#000000';
    return difficultyColourSpectrum(rating);
}

function setErrorMessage(err: any) {
    errorMessage.value = err.response.data
        .split("HEADERS")[0]
        .split('\n')
        .filter((v: string) => v !== '' && v.indexOf('AspNetCore') === -1)
        .join('\n')
}

function secondsToTime(seconds: number): string {
    if (seconds > 3600) {
        return `${Math.floor(seconds / 3600)}:${secondsToTime(seconds % 3600)}`;
    } else {
        return `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
    }
}

const rankStatusColor = computed(() => {
    switch (beatmap.value.status[0].toLowerCase()) {
        case "a":
        case "r":
            return ["#b3ff66", '#000000'];
        case "g":
            return ["#000000", '#4d7365'];
        case "l":
            return ["#ff66ab", '#000000'];
        case 'w':
        case "p":
            return ["#ffd996", '#000000'];
        case 'q':
            return ["#66ccff", '#000000'];
    }
})

const cs = computed(() => {
    switch (beatmap.value.mode_int) {
        case 0:
        case 2:
            return ['Circle Size', beatmap.value.cs, '#fff']
        case 1:
            return null;
        case 3:
            return ['Key Count', beatmap.value.cs, '#fff']
    }
})
const hp = computed(() => ['HP Drain', beatmap.value.drain, '#fff'])
const acc = computed(() => ['Accuracy', beatmap.value.accuracy, '#fff'])
const ar = computed(() => {
    switch (beatmap.value.mode_int) {
        case 0:
        case 2:
            return ['Approach Rate', beatmap.value.ar, '#fff']
        default:
            return null
    }
})
const starRating = computed(() => ['Star Rating', beatmap.value.difficulty_rating, '#fc2'])
const beatmapDetail = computed(() => [cs.value, hp.value, acc.value, ar.value, starRating.value].filter((v) => v !== null))

const userRating = computed(() => {
    let x = [...beatmap.value.beatmapset.ratings];
    for (let i = 1; i < x.length; i++) {
        x[i] += x[i - 1];
    }
    let y = [...beatmap.value.beatmapset.ratings]
    let y_max = max(y)!;
    y = y.map((v) => v / y_max * 100);
    return [x[10], x[5], x[10] - x[5], y]
})

const failureRating = computed(() => {
    let x = [...beatmap.value.failtimes.exit];
    let y = [...beatmap.value.failtimes.fail]
    let x_max = max(x)!;
    let y_max = max(y)!;
    let m = max([x_max, y_max])!;
    x = x.map((v) => v / m * 100);
    y = y.map((v) => v / m * 100);
    return x.map((v, i) => [v, y[i]])
});

const ppAcc = computed(() =>
    PpAcc(score.value.statistics.count_geki, score.value.statistics.count_300, score.value.statistics.count_katu, score.value.statistics.count_100, score.value.statistics.count_50, score.value.statistics.count_miss)
)

const page_width = 1700;
const page_height = 1100;
const cover_path = computed(() => {
    return `url(${osu_beatmapCover_builder(score.value.beatmapset.id, score.value.beatmap.checksum, score.value.beatmap.id)}),url(${beatmap.value.beatmapset.covers["cover@2x"]})`
});
const cover_width = 1000;
const cover_height = 400;
const cover_padding = 3;

const diff_bar_width = 20;
const diffColor = computed(() => getDiffColor(sr.value))
const diffTextColor = computed(() => sr.value >= 7.5 ? '#ffd996' : '#000000')

const detail_width = cover_width * 0.9 - diff_bar_width - diff_bar_width / 2;

const profile_width = page_width - cover_width;
const profile_add_width = cover_width * 0.1 / 2;
const profile_clip_size = cover_width * 0.1 / (page_width - cover_width + profile_add_width) * 100
const profile_clip = computed(() => `polygon(${profile_clip_size}% 0, 100% 0%, 100% 100%, 0 100%)`)
const profile_padding_y = 50;
const profile_wrapper_height = cover_height - cover_padding * 2;
const profile_height = cover_height - cover_padding * 2 - profile_padding_y * 2;

</script>

<style scoped>

.song-info-with-icon {
    @apply flex gap-7 text-[#ffd996] font-bold text-2xl
}

.song-info-with-icon > div {
    @apply flex items-center gap-2
}

.song-info-with-icon > div > img {
    width: 40px;
    height: 40px;
}

.profile-clip {
    clip-path: v-bind(profile_clip)
}

.profile_wrapper {
    height: v-bind(profile_wrapper_height+ 'px');
    width: v-bind(profile_width+profile_add_width+ 'px');
    padding-top: v-bind(profile_padding_y+ 'px');
}

.profile-size {
    height: v-bind(profile_height+ 'px');
    width: v-bind(profile_width+profile_add_width+ 'px');
}

.profile_pos {
    margin-left: v-bind(-profile_add_width+ 'px');
}

.cover-clip {
    clip-path: polygon(0 0, 100% 0%, 90% 100%, 0 100%);
}

.cover-size {
    height: v-bind(cover_height+ 'px');
    width: v-bind(cover_width+ 'px');
}

.bg {
    @apply font-osu-web py-4;
    width: v-bind(page_width+ 'px');
    height: v-bind(page_height+ 'px');
    overflow: hidden;
}

.bg:after {
    @apply w-full h-full absolute blur-md bg-cover bg-center ;
    width: v-bind(page_width+ 'px');
    height: v-bind(page_height+ 'px');
    background-image: v-bind(cover_path);
    content: '';
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    z-index: -2;
}

.cover-shadow {
    filter: drop-shadow(0 0 15px #ddffff) drop-shadow(0 0 15px #ddffff);
}

.cover {
    @apply w-fit relative;
    @apply cover-clip cover-size;
    background-color: #ddffff;
    padding: v-bind(cover_padding+ 'px') v-bind(cover_padding+ 'px') v-bind(cover_padding+ 'px') 0;
}

.cover:after {
    @apply bg-cover bg-center absolute;
    @apply cover-clip;
    z-index: -1;
    content: '';
    background-image: v-bind(cover_path);
    top: v-bind(cover_padding+ 'px');
    left: 0;
    right: v-bind(cover_padding+ 'px');
    bottom: v-bind(cover_padding+ 'px');
}

.diff-bar {
    @apply h-full;
    display: inline-block;
    width: v-bind(diff_bar_width+ 'px');
    margin-right: v-bind(diff_bar_width / 2 + 'px');
    background-color: v-bind(diffColor);
}

.diff-bar:after {
    @apply h-full;
    display: inline-block;
    content: '';
    opacity: 0.5;
    width: v-bind(diff_bar_width / 2 + 'px');
    margin-left: v-bind(diff_bar_width+ 'px');
    background-color: v-bind(diffColor);
}

.diff-text {
    @apply rounded-3xl px-2 py-0.5 font-bold flex place-content-center gap-1 text-xl pr-3;
    color: v-bind(diffTextColor);
    background-color: v-bind(diffColor);
}

.diff-text:before {
    content: '★';
}

.rank-status {
    @apply rounded-3xl px-2 py-0.5 text-black uppercase text-sm font-bold;
    background-color: v-bind(rankStatusColor [0]);
    color: v-bind(rankStatusColor [1]);
}

.detail {
    @apply flex flex-col gap-2 grow;
    width: v-bind(detail_width+ 'px');
    margin-left: v-bind(diff_bar_width+diff_bar_width / 2 + 'px');
}

.grid-detail {
    @apply grid items-center gap-x-5 gap-y-1;
    grid-template-columns: auto 1fr auto;
    grid-template-rows: auto;
}

.point-of-failure {
    @apply relative h-full;
}

.point-of-failure > div:nth-child(1) {
    @apply absolute bottom-0 left-0 right-0 bg-[#fc2] bg-opacity-80;
}

.point-of-failure > div:nth-child(2) {
    @apply absolute bottom-0 left-0 right-0 bg-[#c60] bg-opacity-80;
}

.osu-cover-text-shadow {
    text-shadow: 0 0 10px black;
}

.osu-rank-text-shadow {
    text-shadow: 0 0 20px hsl(200, 40%, 100%);
}

.osu-grade-detail-table {
    @apply flex w-full uppercase gap-1;
    white-space: nowrap;
}

.osu-grade-detail-table > div {
    @apply flex flex-col items-center grow basis-0;
}

.osu-grade-detail-table > div > div:nth-child(1) {
    @apply bg-black w-full text-center rounded-full text-lg bg-opacity-50;

}

.osu-grade-detail-table > div > div:nth-child(2) {
    @apply text-3xl
}

pre {
    white-space: pre-wrap;
    word-wrap: anywhere;
}
</style>
