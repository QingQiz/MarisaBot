<script setup lang="ts">
import {OsuRecommendData} from "@/components/osu/Osu.Data";
import RecommendCard from "@/components/osu/partial/RecommendCard.vue";

const props = defineProps<{ data: OsuRecommendData }>();
</script>

<template>
    <recommend-card :data="props.data">
        <template #predict-key>Current Score</template>
        <template #predict-val>
            {{ props.data.currentScore ?? '-' }}
        </template>
    </recommend-card>
</template>
