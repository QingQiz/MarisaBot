<template>
    <template v-if="data_fetched">
        <ScoreCard :data="data" v-if="!errorMessage"/>
        <div v-else>
            <div class="bg-red-500 text-white text-2xl w-fit min-w-1000px">
                <pre>{{ errorMessage }}</pre>
            </div>
        </div>
    </template>
</template>

<script setup lang="ts">
import {ref} from "vue";
import axios from "axios";
import {osu_beatmapInfo, osu_best, osu_recent, osu_userInfo} from "@/GlobalVars";
import {BeatmapInfo, Score, UserInfo} from "@/components/osu/Osu.Data";
import {useRoute} from "vue-router";
import ScoreCard from "@/components/osu/partial/ScoreCard.vue";


const route = useRoute()
let name    = ref(route.query.name)
let bpRank  = ref(route.query.bpRank)
let mode    = ref(route.query.mode)
let recent  = ref(route.query.recent != null)
let fail    = ref(route.query.fail != null)

const user    = ref({} as UserInfo)
const beatmap = ref({} as BeatmapInfo)
const score   = ref({} as Score);

const data = ref({
    beatmap: beatmap,
    score: score,
    user: user
})

const data_fetched = ref(false)
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
    .then(_ => data.value = {beatmap: beatmap.value, score: score.value, user: user.value})
    .catch(SetErrorMessage)
    .finally(() => data_fetched.value = true);

function SetErrorMessage(err: any) {
    errorMessage.value = err.response.data
        .split("HEADERS")[0]
        .split('\n')
        .filter((v: string) => v !== '' && v.indexOf('AspNetCore') === -1)
        .join('\n')
}

</script>

