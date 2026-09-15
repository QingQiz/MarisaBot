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
            <span class="font-rodin text-[18px] tracking-[0.28em] text-white/70">VERSUS</span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
        </div>
        <div class="difficulty-line">
            <span class="difficulty" :style="{color: diffColor}">{{ diffName }}</span>
            <span class="level">{{ data.Level }} · {{ data.Constant.toFixed(1) }}</span>
        </div>
        <div class="players">
            <div v-for="(player, index) in data.Players" :key="player.Nickname" class="player" :class="{winner: data.Winner === player.Nickname}">
                <div class="player-head"><span>{{ player.Nickname }}</span><b v-if="data.Winner === player.Nickname">WIN</b></div>
                <template v-if="player.Played && player.Score">
                    <div class="achievement">{{ player.Score.Achievement.toFixed(4) }}<small>%</small></div>
                    <div class="details">{{ player.Score.Rank }} · Ra {{ player.Score.Rating }} · DX {{ player.Score.DxScore }}</div>
                    <div class="marks">{{ markText(player.Score) }}</div>
                </template>
                <div v-else class="unplayed">未游玩</div>
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
import {DIFF_COLORS, bgKeyOf, themeMainOf} from '@/components/maimai/utils/song_card'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiSongMetaBar from '@/components/maimai/MaiSongMetaBar.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'

interface Score { Achievement: number; Rank: string; Rating: number; DxScore: number; Fc: string; Fs: string }
interface Player { Nickname: string; Played: boolean; Score: Score | null }
interface VersusData { Song: {Id: number; Title: string; Type: string; Artist: string; Genre: string; Bpm: number; From: string; IsNew: boolean}; LevelIndex: number; Level: string; Constant: number; Players: Player[]; Winner: string }
const route = useRoute()
const data = ref<VersusData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'versus'}}).then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })
const bgKey = computed(() => bgKeyOf(data.value?.LevelIndex ?? 3, false))
const accent = computed(() => themeMainOf(data.value?.LevelIndex ?? 3, false))
const diffName = computed(() => ['绿谱', '黄谱', '红谱', '紫谱', '白谱'][data.value?.LevelIndex ?? 3])
const diffColor = computed(() => DIFF_COLORS[data.value?.LevelIndex ?? 3])
function markText(score: Score) { return [score.Fc, score.Fs].filter(Boolean).join(' / ') || '—' }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss">
.section-tag { font-family: 'Microsoft YaHei',sans-serif; font-weight: bold; font-size: 21px; letter-spacing: .1em; border-radius: 9999px; padding: 4px 20px; background: #c64fe4; color: #fff; box-shadow: 0 0 0 2px rgba(255,255,255,.8); white-space: nowrap; }
.difficulty-line { display:flex; align-items:baseline; gap:16px; margin-bottom:12px; }
.difficulty { font-family:'Microsoft YaHei',sans-serif; font-size:27px; font-weight:900; }
.level { font:700 20px 'Torus',sans-serif; color:rgba(255,255,255,.64); }
.players { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
.player { min-height:190px; padding:20px 24px; border:1px solid rgba(255,255,255,.15); border-radius:16px; background:rgba(8,8,16,.34); }
.player.winner { border-color:rgba(255,255,255,.75); box-shadow:inset 5px 0 0 #c64fe4, 0 0 22px rgba(198,79,228,.2); }
.player-head { display:flex; justify-content:space-between; align-items:center; font:700 21px 'Microsoft YaHei',sans-serif; }
.player-head b { font:700 14px 'Torus',sans-serif; letter-spacing:.12em; color:#ffe45c; }
.achievement { margin-top:22px; font:900 39px 'Torus',sans-serif; letter-spacing:.02em; }
.achievement small { margin-left:3px; font-size:17px; opacity:.65; }
.details,.marks { margin-top:7px; font:700 17px 'Torus','Microsoft YaHei',sans-serif; color:rgba(255,255,255,.75); }
.marks { color:rgba(255,255,255,.55); }
.unplayed { margin-top:55px; font:700 22px 'SEGA NewRodin',sans-serif; letter-spacing:.16em; color:rgba(255,255,255,.35); }
</style>
