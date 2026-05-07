<template>
    <div class="min-h-screen bg-gray-900 text-gray-100 p-4 font-wenkai text-2xl whitespace-nowrap">
        <div>
            <h1 class="text-5xl font-bold text-pink-400 mb-4">MarisaBot Help</h1>
            <p class="text-gray-400 mb-1 text-base">本bot为开源bot · 仙人指路：QingQiz/MarisaBot</p>
            <p class="text-gray-500 mb-2 text-base">粉色 = 命令/别名 · 青色 = 代码/参数值 · 琥珀色 = 参数说明 · 缩进 = 子命令 · 触发：<span class="text-pink-300">主命令 子命令</span> (如"<span class="text-pink-300">mai best</span>")</p>

            <div v-if="loading" class="text-gray-400">Loading...</div>

            <div v-else-if="error" class="text-red-400">{{ error }}</div>

            <div v-else class="space-y-1">
                <HelpSection v-for="(section, i) in helpData" :key="i" :section="section" :depth="0" />
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import axios from 'axios'
import { useRoute } from 'vue-router'
import { host } from '@/GlobalVars'

import HelpSection from '@/components/help/HelpSection.vue'

interface HelpNode {
    cmd: string[]
    doc: string
    param?: string
    sub: HelpNode[]
}

const route = useRoute()
const helpData = ref<HelpNode[]>([])
const loading = ref(true)
const error = ref('')

onMounted(async () => {
    try {
        const resp = await axios.get(`${host}/Help/Get`, { params: { name: route.query.plugin || undefined } })
        helpData.value = resp.data
    } catch (e) {
        if (axios.isAxiosError(e) && e.response) {
            error.value = `${e.response.status}: ${e.response.data?.message ?? e.message}`
        } else {
            error.value = 'Failed to load help data'
        }
    } finally {
        loading.value = false
    }
})
</script>
