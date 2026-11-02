<template>
    <div v-if="themes.length" class="themes-shell">
        <div class="heading">
            <h1>b50 背景</h1>
            <p>发送「mai 背景 名称」切换</p>
        </div>
        <div class="tiles">
            <div v-for="t in themes" :key="t.Key" class="tile" :class="`theme-${t.Key}`">
                <div v-if="t.Key === 'collage'" class="absolute inset-0 bg-collage">
                    <img v-for="id in COLLAGE" :key="id" :src="`/assets/maimai/cover/${id}.png`" alt="">
                </div>
                <div class="absolute inset-0 mai-deco-bg"></div>
                <div class="relative">
                    <div class="tile-head">
                        <span class="name">{{ t.Name }}</span>
                        <span v-if="t.Key === current" class="tag current">当前</span>
                        <span v-if="t.Key === fallback" class="tag">默认</span>
                    </div>
                    <div class="cards">
                        <score-card v-for="(s, i) in SAMPLES" :key="i" :score="s" :index="i + 1"/>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import {ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import ScoreCard from '@/components/maimai/partial/ScoreCard.vue'
import {Score} from '@/components/maimai/utils/best_t'

const route = useRoute()
const themes = ref<{ Key: string, Name: string }[]>([])
const current = ref('')
const fallback = ref('')

const SAMPLES: Score[] = [
    {achievements: 100.7416, ds: 14.9, dxScore: 2844, max_dx: 3114, fc: 'fc', fs: 'sync', level: '14+', level_index: 3, level_label: 'Master', ra: 335, rate: 'sssp', song_id: 11311, title: '躯樹の墓守', type: 'DX'},
    {achievements: 101.0, ds: 13.8, dxScore: 2465, max_dx: 2568, fc: 'app', fs: 'fsp', level: '13+', level_index: 3, level_label: 'Master', ra: 310, rate: 'sssp', song_id: 11807, title: 'ウタヒメナイトストーム', type: 'DX'},
]
const COLLAGE = [11311, 11807, 834, 11394, 773, 227, 11685, 456, 571, 746]

axios.get(context_get, {params: {id: route.query.id, name: 'themes'}}).then(res => {
    themes.value = res.data.Themes
    current.value = res.data.Current
    fallback.value = res.data.Default
})
</script>

<style scoped>
.themes-shell {
    --ink: #2b2147;
    width: 1920px;
    padding: 40px 40px 44px;
    background: #f4f1fa;
    color: var(--ink);
}

.heading {
    display: flex;
    align-items: baseline;
    gap: 28px;
    margin-bottom: 28px;
    font-family: 'Microsoft YaHei', sans-serif;
    font-weight: bold;
}

.heading h1 {
    font-size: 52px;
}

.heading p {
    font-size: 30px;
    opacity: 0.6;
}

.tiles {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 28px;
}

.tile {
    position: relative;
    overflow: hidden;
    padding: 22px 28px 28px;
    border-radius: 28px;
    box-shadow: 0 12px 28px -14px rgba(60, 30, 90, 0.5);
}

.tile-head {
    display: flex;
    align-items: center;
    gap: 14px;
    margin-bottom: 20px;
}

.name,
.tag {
    padding: 4px 22px;
    border-radius: 9999px;
    font: bold 40px/1.3 'Microsoft YaHei', sans-serif;
    background: rgba(255, 255, 255, 0.85);
}

.tag {
    font-size: 26px;
    padding: 3px 16px;
    color: #fff;
    background: var(--ink);
}

.tag.current {
    background: #f93eac;
}

.cards {
    display: flex;
    gap: 20px;
}
</style>

<style scoped lang="postcss" src="@/assets/css/maimai/b50_themes.pcss"/>
