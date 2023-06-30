<template>
    <template v-if="data_fetched ">
        <div class="p-10 w-fit" style="background-color: hsl(200, 15%, 15%)" v-if="error_message === ''">
            <div class="grid grid-cols-1 gap-10 w-fit">
                <div v-for="i in d" class="z-50" v-if="mode === '3'">
                    <mania-recommend-card :data="i as ManiaRecommendData"/>
                </div>
                <div v-for="i in d" class="z-50" v-else>
                    <osu-recommend-card :data="i as OsuRecommendData"/>
                </div>
            </div>
        </div>
        <div v-else class="bg-red-500 text-white text-2xl w-[1000px]">
            <pre>{{ error_message }}</pre>
        </div>
    </template>
</template>

<script lang="ts" setup>
import {ref} from "vue";
import {ManiaRecommendData, OsuRecommendData, RecommendData} from "@/components/osu/Osu.Data";
import ManiaRecommendCard from "@/components/osu/partial/ManiaRecommendCard.vue";
import OsuRecommendCard from "@/components/osu/partial/OsuRecommendCard.vue";
import axios from "axios";
import {osu_getRecommend} from "@/GlobalVars";
import {useRoute} from "vue-router";


const route = useRoute()
let uid     = ref(route.query.uid)
let mode    = ref(route.query.mode)

const data_fetched  = ref(true)
const error_message = ref('')

let d = ref([] as RecommendData[])

axios.get(osu_getRecommend, {params: {uid: uid.value, modeInt: mode.value}})
    .then(data => {
        if (data.data.success === true) {
            d.value = data.data.data.list
        } else {
            error_message.value = data.data.message
        }
    })
    .catch(err => {
        error_message.value = JSON.stringify(err, null, 4)
    })
    .finally(() => data_fetched.value = true)
</script>