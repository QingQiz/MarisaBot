<template>
    <template v-if="data_fetched">
        <div class="p-10 w-fit" style="background-color: hsl(200, 15%, 15%)" v-if="error_message === ''">
            <div class="grid grid-cols-1 gap-y-10 w-[1020px]">
                <div v-for="i in d" class="z-50">
                    <template v-if="(i as ManiaRecommendData).keyCount !== undefined">
                        <mania-recommend-card :data="i as ManiaRecommendData" />
                    </template>
                    <template v-else>
                        <osu-recommend-card :data="i as OsuRecommendData" />
                    </template>
                </div>
            </div>
        </div>
        <div v-else class="bg-red-500 text-white text-2xl w-fit min-w-[1000px]">
            <pre>{{ error_message }}</pre>
        </div>
    </template>
</template>

<script lang="ts" setup>
import { ref } from "vue";
import { ManiaRecommendData, OsuRecommendData, RecommendData } from "@/components/osu/Osu.Data";
import ManiaRecommendCard from "@/components/osu/partial/ManiaRecommendCard.vue";
import OsuRecommendCard from "@/components/osu/partial/OsuRecommendCard.vue";
import axios from "axios";
import { context_get } from "@/GlobalVars";
import { useRoute } from "vue-router";


const route = useRoute()

let id = ref(route.query.id)

const data_fetched = ref(true)
const error_message = ref('')

let d = ref([] as RecommendData[])

axios.get(context_get, { params: { id: id.value, name: "recommend" } })
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