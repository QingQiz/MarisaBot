<script setup lang="ts">

import { Score } from "@/components/maimai/Recommend.vue";

const props = defineProps<{ a: Score | null, b: Score | null }>();

</script>

<template>
    <div class="flex">
        <div class="w-[500px]" :class="{ 'bg-red-300': a != null && (b == null || a?.Item4 != b?.Item4) }">
            <div class="flex gap-3" v-if="a != null">
                <div :class="{ 'bg-red-400': b != null && a.Item4 != b.Item4 }">
                    {{ a.Item4.toFixed(0).padStart(3, '0') }}
                </div>
                <div :class="{ 'bg-red-400': b != null && a.Item3 != b.Item3 }">
                    {{ a.Item3.toFixed(4).padStart(8, '0') }}
                </div>
                <div class="text">
                    {{ a.Item1.Title }}
                </div>
            </div>
            <div v-else class="grow diff"></div>
        </div>
        <div class="w-[500px]" :class="{ 'bg-green-300': b != null && (a == null || a?.Item4 != b?.Item4) }">
            <div class="flex gap-3" v-if="b != null">
                <div :class="{ 'bg-green-500': a != null && a.Item4 != b.Item4 }">
                    {{ b?.Item4.toFixed(0).padStart(3, '0') }}
                </div>
                <div :class="{ 'bg-green-500': a != null && a.Item3 != b.Item3 }">
                    {{ b?.Item3.toFixed(4).padStart(8, '0') }}
                </div>
                <div class="text">
                    {{ b?.Item1.Title }}
                </div>
            </div>
            <div v-else class="grow diff"></div>
        </div>
    </div>
</template>

<style scoped>
.text {
    @apply text-ellipsis overflow-hidden whitespace-nowrap
}

.diff {
    background-image: repeating-linear-gradient(-45deg, #ccc, #ccc 3px, #fff 3px, #fff 8.55px);
}
.diff::before {
    @apply opacity-0;
    content: 'a';
}
</style>