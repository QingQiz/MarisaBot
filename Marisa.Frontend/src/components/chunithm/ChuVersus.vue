<template>
    <MaiCardShell v-if="data" class="mai-versus chu-versus chu-theme" :class="{'chu-ultima': data.LevelIndex === 4}" bg-key="DMY" :background="backgroundOf(data.LevelIndex)" :accent="accent" :style="{'--accent': accent, '--winner-glow': accent + '38'}">
        <SongHeader :song="data.Song"/>
        <div class="flex items-center gap-4 mt-8 mb-4"><span class="section-tag">玩家对战</span><div class="flex-1 h-[2px] rounded-full bg-white/20"></div></div>
        <div class="difficulty-line"><span class="difficulty" :style="difficultyStyle(data.LevelIndex)">{{ data.LevelName }} {{ data.Level }}</span></div>
        <div class="players">
            <div v-for="(player, index) in data.Players" :key="index" class="player mai-accent-rail" :class="{winner: data.WinnerIndex === index}">
                <div class="player-head">
                    <span class="player-name">{{ player.Name }}</span>
                    <span class="player-status"><b v-if="data.WinnerIndex === index" class="win-badge">WIN</b><span v-if="data.WinnerIndex === 1 - index && player.Played" class="loser-stamp">菜</span></span>
                </div>
                <template v-if="player.Played && player.Score">
                    <div class="score-main"><div class="achievement">{{ formatScore(player.Score.Score) }}</div><div class="rank-slot"><img :src="rankIcon(player.Score.Rank)" class="rank" alt=""></div></div>
                    <div class="metrics"><div><span>RATING</span><b>{{ formatRating(player.Score.Rating) }}</b></div></div>
                </template>
                <div v-else class="unplayed"><span class="unplayed-mark">—</span>未游玩</div>
                <div class="marks"><VersusMarks :combo="player.Score?.FullCombo ?? ''" :chain="player.Score?.FullChain ?? ''" :clear="player.Score?.Clear ?? ''"/></div>
            </div>
        </div>
        <footer class="mt-7"><span class="footer-text">MARISA BOT · VERSUS</span></footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import SongHeader from '@/components/chunithm/partial/SongHeader.vue'
import VersusMarks from '@/components/chunithm/partial/VersusMarks.vue'
import {backgroundOf, difficultyColor, difficultyStyle} from '@/components/chunithm/utils/song-card'
import {formatRating, formatScore, rankIcon, type ChuScore, type ChuSong} from '@/components/chunithm/utils/versus'
interface VersusData { Song: ChuSong; LevelIndex: number; LevelName: string; ChartIndex: number; Level: string; Constant: number; Players: {Name: string; Played: boolean; Score: ChuScore | null}[]; WinnerIndex: number }
const route = useRoute()
const data = ref<VersusData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'chuVersus'}}).then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })
const accent = computed(() => difficultyColor(data.value?.LevelIndex ?? 3))
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/versus/single.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/versus.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/theme.pcss"/>
