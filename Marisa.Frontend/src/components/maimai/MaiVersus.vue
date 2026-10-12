<template>
    <MaiCardShell v-if="data" class="mai-versus" :bg-key="bgKey" :accent="accent"
                  :style="{'--accent': accent, '--winner-glow': accent + '38'}">
        <MaiSongMetaBar :from="data.Song.From" :type="data.Song.Type" :song-id="data.Song.Id"
                        :bpm="data.Song.Bpm" :genre="data.Song.Genre" :is-new="data.Song.IsNew"/>
        <div class="flex items-end gap-5 mt-7">
            <MaiCover :song-id="data.Song.Id" :size="132" :frame-radius="22" :img-radius="16"/>
            <MaiSongHeading :title="data.Song.Title" :artist="data.Song.Artist" :max="84" :min="1"
                            :artist-size="20" :artist-top="12"/>
        </div>
        <div class="flex items-center gap-4 mt-8 mb-4">
            <span class="section-tag">玩家对战</span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
        </div>
        <div class="difficulty-line">
            <span class="difficulty" :style="{color: diffColor}">{{ diffName }} {{ data.Level }}</span>
        </div>
        <div class="players">
            <div v-for="(player, index) in data.Players" :key="index" class="player mai-accent-rail" :class="{winner: isWinner(index)}">
                <div class="player-head">
                    <span class="player-name">{{ player.Nickname }}</span>
                    <span class="player-status">
                        <b v-if="isWinner(index)" class="win-badge">WIN</b>
                        <span v-if="isLoser(index)" class="loser-stamp">菜</span>
                    </span>
                </div>
                <template v-if="player.Played && player.Score">
                    <div class="score-main">
                        <div class="achievement">{{ player.Score.Achievement.toFixed(4) }}<small>%</small></div>
                        <div class="rank-slot"><img :src="rankIcon(player.Score)" class="rank" alt=""></div>
                    </div>
                    <div class="metrics">
                        <div><span>Ra</span><b>{{ player.Score.Rating }}</b></div>
                        <div><span>DX SCORE</span><b>{{ player.Score.DxScore }}<small>/{{ data.MaxDx }}</small></b></div>
                        <div><span>DX%</span><b>{{ dxRate(player.Score) }}%</b></div>
                    </div>
                    <div class="marks">
                        <img v-if="player.Score.Fc" :src="fcIcon(player.Score.Fc)" alt="">
                        <img v-if="player.Score.Fs" :src="fsIcon(player.Score.Fs)" alt="">
                        <img v-if="starN(player.Score)" :src="starIcon(player.Score)" alt="">
                    </div>
                </template>
                <div v-else class="unplayed"><span class="unplayed-mark">—</span>未游玩</div>
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
import {dxScoreStar} from '@/components/maimai/utils/ordinal'
import {DIFF_NAMES, DIFF_COLORS, bgKeyOf, themeMainOf} from '@/components/maimai/utils/song_card'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiSongMetaBar from '@/components/maimai/MaiSongMetaBar.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'

interface Score { Achievement: number; Rank: string; Rating: number; DxScore: number; Fc: string; Fs: string }
interface Player { Nickname: string; Played: boolean; Score: Score | null }
interface VersusData { Song: {Id: number; Title: string; Type: string; Artist: string; Genre: string; Bpm: number; From: string; IsNew: boolean}; LevelIndex: number; Level: string; Constant: number; MaxDx: number; Players: Player[]; WinnerIndex: number }
const route = useRoute()
const data = ref<VersusData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'versus'}}).then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })
const bgKey = computed(() => bgKeyOf(data.value?.LevelIndex ?? 3, false))
const accent = computed(() => themeMainOf(data.value?.LevelIndex ?? 3, false))
const diffName = computed(() => DIFF_NAMES[data.value?.LevelIndex ?? 3])
const diffColor = computed(() => DIFF_COLORS[data.value?.LevelIndex ?? 3])
const PIC = '/assets/maimai/pic'
function rankIcon(score: Score) { return `${PIC}/rank_${score.Rank.toLowerCase().replaceAll('+', 'p')}.png` }
function fcIcon(name: string) { return `${PIC}/icon_${name}.png` }
function fsIcon(name: string) { return `${PIC}/icon_${name}.png` }
function starN(score: Score) { return dxScoreStar(score.DxScore, data.value?.MaxDx ?? 0) }
function starIcon(score: Score) { return `${PIC}/music_icon_dxstar_${starN(score)}.png` }
function dxRate(score: Score) { return data.value?.MaxDx ? (score.DxScore / data.value.MaxDx * 100).toFixed(1) : '0.0' }
function isWinner(index: number) { return data.value?.WinnerIndex === index }
// 未游玩的一方只算输，不盖“菜”章
function isLoser(index: number) { return data.value?.WinnerIndex === 1 - index && data.value.Players[index].Played }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/versus/single.pcss"/>
