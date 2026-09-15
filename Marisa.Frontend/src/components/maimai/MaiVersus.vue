<template>
    <MaiCardShell v-if="data" class="mai-versus" :bg-key="bgKey" :accent="accent">
        <MaiSongMetaBar :from="data.Song.From" :type="data.Song.Type" :song-id="data.Song.Id"
                        :bpm="data.Song.Bpm" :genre="data.Song.Genre" :is-new="data.Song.IsNew"/>
        <div class="flex items-end gap-5 mt-7">
            <MaiCover :song-id="data.Song.Id" :size="132" :frame-radius="22" :img-radius="16"/>
            <MaiSongHeading :title="data.Song.Title" :artist="data.Song.Artist" :max="84" :min="28"
                            :artist-size="20" :artist-top="12"/>
        </div>
        <div class="flex items-center gap-4 mt-8 mb-4">
            <span class="section-tag">玩家对战</span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
        </div>
        <div class="difficulty-line">
            <span class="difficulty" :style="{color: diffColor}">{{ diffName }}</span>
            <span class="level">{{ data.Level }} · {{ data.Constant.toFixed(1) }}</span>
        </div>
        <div class="players">
            <div v-for="player in data.Players" :key="player.Nickname" class="player" :class="{winner: data.Winner === player.Nickname}">
                <div class="player-head"><span>{{ player.Nickname }}</span><b v-if="data.Winner === player.Nickname">WIN</b></div>
                <template v-if="player.Played && player.Score">
                    <div class="score-main">
                        <div class="achievement">{{ player.Score.Achievement.toFixed(4) }}<small>%</small></div>
                        <img :src="rankIcon(player.Score)" class="rank" alt="">
                    </div>
                    <div class="metrics">
                        <div><span>Ra</span><b>{{ player.Score.Rating }}</b></div>
                        <div><span>DX SCORE</span><b>{{ player.Score.DxScore }}<small>/{{ data.MaxDx }}</small></b></div>
                        <div><span>DX%</span><b>{{ dxRate(player.Score) }}%</b></div>
                    </div>
                    <div class="marks">
                        <span v-if="player.Score.Fc"><img :src="fcIcon(player.Score.Fc)" alt=""></span>
                        <span v-if="player.Score.Fs"><img :src="fsIcon(player.Score.Fs)" alt=""></span>
                        <span v-if="starN(player.Score)"><img :src="starIcon(player.Score)" alt=""></span>
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
interface VersusData { Song: {Id: number; Title: string; Type: string; Artist: string; Genre: string; Bpm: number; From: string; IsNew: boolean}; LevelIndex: number; Level: string; Constant: number; MaxDx: number; Players: Player[]; Winner: string }
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
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss">
.section-tag { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; letter-spacing: .1em; border-radius: 9999px; padding: 4px 20px; background: #c64fe4; color: #fff; box-shadow: 0 0 0 2px rgba(255,255,255,.8); white-space: nowrap; }
.difficulty-line { display:flex; align-items:baseline; gap:16px; margin-bottom:12px; }
.difficulty { font-family:'SEGA NewRodin',sans-serif; font-size:25px; font-weight:900; letter-spacing:.03em; }
.level { font:700 21px 'Torus',sans-serif; color:rgba(255,255,255,.72); }
.players { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
.player { min-height:228px; padding:18px 22px 16px; border:1px solid rgba(255,255,255,.15); border-radius:16px; background:linear-gradient(105deg,rgba(8,8,16,.58),rgba(8,8,16,.25)); position:relative; overflow:hidden; }
.player::before { content:''; position:absolute; inset:0 auto 0 0; width:5px; background:rgba(255,255,255,.2); }
.player.winner { border-color:rgba(255,255,255,.78); box-shadow:inset 0 0 0 1px rgba(255,255,255,.13),0 0 22px rgba(198,79,228,.22); }
.player.winner::before { background:#c64fe4; }
.player-head { display:flex; justify-content:space-between; align-items:center; font:700 21px 'Microsoft YaHei',sans-serif; position:relative; }
.player-head b { font:700 14px 'Torus',sans-serif; letter-spacing:.12em; color:#ffe45c; }
.score-main { margin-top:13px; display:flex; align-items:center; justify-content:space-between; gap:8px; }
.achievement { font:900 39px 'Torus',sans-serif; letter-spacing:.02em; line-height:1; }
.achievement small { margin-left:3px; font-size:17px; opacity:.65; }
.rank { height:34px; width:auto; display:block; }
.metrics { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:0; margin-top:15px; padding:9px 0 8px; border-top:1px solid rgba(255,255,255,.14); border-bottom:1px solid rgba(255,255,255,.14); }
.metrics div + div { border-left:1px solid rgba(255,255,255,.12); }
.metrics div { min-width:0; display:flex; flex-direction:column; align-items:center; text-align:center; gap:3px; padding:0 6px; }
.metrics span { font:700 11px 'Torus',sans-serif; letter-spacing:.04em; color:rgba(255,255,255,.5); white-space:nowrap; }
.metrics b { font:800 18px 'Torus',sans-serif; white-space:nowrap; }
.metrics small { margin-left:4px; font-size:11px; color:rgba(255,255,255,.55); }
.marks { margin-top:9px; height:32px; display:flex; align-items:center; gap:8px; }
.marks span { display:flex; align-items:center; height:32px; }
.marks img { display:block; max-height:32px; max-width:76px; }
.unplayed { height:142px; display:flex; align-items:center; justify-content:center; gap:10px; font:700 21px 'SEGA NewRodin',sans-serif; letter-spacing:.14em; color:rgba(255,255,255,.34); }
.unplayed-mark { font:400 35px 'Torus',sans-serif; color:rgba(255,255,255,.22); }
</style>
