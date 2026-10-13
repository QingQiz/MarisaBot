<template>
    <MaiCardShell v-if="data" class="mai-versus-multi" :bg-key="bgKey" :accent="accent" :style="{'--accent': accent}">
        <MaiSongMetaBar :from="song.From" :type="song.Type" :song-id="song.Id"
                        :bpm="song.Bpm" :genre="song.Genre" :is-new="song.IsNew"/>
        <div class="flex items-end gap-5 mt-7">
            <MaiCover :song-id="song.Id" :size="132" :frame-radius="22" :img-radius="16"/>
            <MaiSongHeading :title="song.Title" :artist="song.Artist" :max="84" :min="1"
                            :artist-size="20" :artist-top="12"/>
        </div>
        <div class="flex items-center gap-4 mt-8 mb-4">
            <span class="section-tag">多人对战</span>
            <span class="difficulty" :style="{color: diffColor}">{{ diffName }} {{ data.Chart.Level }}<small>{{ data.Chart.Constant.toFixed(1) }}</small></span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
            <span v-if="data.Round" class="round-badge">ROUND<b>{{ pad(data.Round) }}</b></span>
        </div>

        <div class="racers">
            <div v-for="(player, index) in data.Players" :key="index" class="racer mai-accent-rail"
                 :class="{leader: player.Place === 1, unplayed: player.Place == null}"
                 :style="{'--rail-color': placeColor(player.Place)}">
                <div class="place" :style="{color: placeColor(player.Place)}">
                    <template v-if="player.Place != null"><b>{{ player.Place }}</b><small>{{ placeSuffix(player.Place) }}</small></template>
                    <b v-else>—</b>
                </div>
                <div class="who" :class="{stamped: player.Stamp}">
                    <span class="name">{{ player.Name }}</span>
                    <span v-if="player.Score.State === 'played'" class="marks">
                        <img v-if="player.Score.Fc" :src="markIcon(player.Score.Fc)" alt="">
                        <img v-if="player.Score.Fs" :src="markIcon(player.Score.Fs)" alt="">
                        <img v-if="stars(player.Score)" :src="starIcon(stars(player.Score))" alt="">
                    </span>
                    <span v-if="player.Stamp" class="stamp-slot"><span class="loser-stamp">菜</span></span>
                </div>
                <template v-if="player.Score.State === 'played'">
                    <div class="achv">
                        <div class="achievement">{{ player.Score.Achievement!.toFixed(4) }}<small>%</small></div>
                        <div v-if="player.Place! > 1" class="gap">−{{ (best - player.Score.Achievement!).toFixed(4) }}%</div>
                    </div>
                    <img class="rank" :src="rankIcon(player.Score.Rank)" alt="">
                    <div class="dx">
                        <span>DX SCORE</span>
                        <b>{{ player.Score.DxScore }}<small>/{{ data.Chart.MaxDx }}</small></b>
                        <span class="ra">Ra <b>{{ player.Score.Rating }}</b></span>
                    </div>
                </template>
                <div v-else class="no-play">No Play Record</div>
            </div>
        </div>

        <MaiVersusStandings v-if="data.Standings.length" :standings="data.Standings" :rounds="data.Round" :accent="accent"/>

        <footer class="mt-7 flex items-end justify-between">
            <span class="footer-text">MARISA BOT · VERSUS</span>
            <span v-if="data.Round" class="next-hint">发送「开始」进行下一轮</span>
        </footer>
    </MaiCardShell>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue'
import axios from 'axios'
import {useRoute} from 'vue-router'
import {context_get} from '@/GlobalVars'
import {dxScoreStar} from '@/components/maimai/utils/ordinal'
import {DIFF_COLORS, DIFF_NAMES, bgKeyOf, themeMainOf} from '@/components/maimai/utils/song_card'
import {markIcon, placeColor, placeSuffix, rankIcon, starIcon, type VersusChart, type VersusScore, type VersusStanding} from '@/components/maimai/utils/versus'
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiSongMetaBar from '@/components/maimai/MaiSongMetaBar.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'
import MaiVersusStandings from '@/components/versus/VersusStandings.vue'

interface Player { Name: string; Place: number | null; Score: VersusScore; Stamp: boolean }
interface MultiData { Chart: VersusChart; Round: number; Players: Player[]; Standings: VersusStanding[] }

const route = useRoute()
const data = ref<MultiData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'versusMulti'}}).then(res => {
    data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
})
const song = computed(() => data.value!.Chart.Song)
const levelIndex = computed(() => data.value?.Chart.LevelIndex ?? 3)
const bgKey = computed(() => bgKeyOf(levelIndex.value, false))
const accent = computed(() => themeMainOf(levelIndex.value, false))
const diffName = computed(() => DIFF_NAMES[levelIndex.value])
const diffColor = computed(() => DIFF_COLORS[levelIndex.value])
const best = computed(() => Math.max(...data.value!.Players.map(p => p.Score.Achievement ?? 0)))
function stars(score: VersusScore) { return dxScoreStar(score.DxScore ?? 0, data.value?.Chart.MaxDx ?? 0) }
function pad(n: number) { return n.toString().padStart(2, '0') }
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/versus/multi.pcss"/>
