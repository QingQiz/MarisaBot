<template>
    <div :style="{ marginLeft: depth ? '40px' : undefined }">
        <div class="bg-gray-800 rounded px-4 py-2 border border-gray-700 mt-1 w-full">
            <div class="flex flex-wrap items-baseline gap-x-1 gap-y-1">
                <span v-if="section.cmd.length" class="font-mono text-xl whitespace-nowrap">
                    <template v-for="(c, ci) in section.cmd" :key="ci">
                        <span v-if="ci" class="text-gray-600 mx-1">|</span>
                        <span class="text-pink-300">{{ c }}</span>
                    </template>
                </span>
                <span class="text-gray-500 text-xl">—</span>
                <span class="text-gray-200 text-xl break-words" v-html="renderDoc(section.doc)"></span>
                <span v-if="section.param" class="ml-auto flex items-baseline gap-1 max-w-full">
                    <span class="text-amber-400/70 text-xl shrink-0">参数：</span>
                    <span class="text-amber-400/70 text-xl break-words text-right" v-html="renderDoc(section.param)"></span>
                </span>
            </div>
        </div>
        <div v-if="section.sub.length">
            <HelpSection v-for="(child, i) in section.sub" :key="i" :section="child" :depth="depth + 1" />
        </div>
    </div>
</template>

<script setup lang="ts">
import {renderDoc} from '@/components/help/render'

interface HelpNode {
    cmd: string[]
    doc: string
    param?: string
    sub: HelpNode[]
}

defineProps<{
    section: HelpNode
    depth: number
}>()
</script>
