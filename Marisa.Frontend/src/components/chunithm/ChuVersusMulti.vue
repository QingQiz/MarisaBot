<template>
    <MaiCardShell v-if="data" class="mai-versus-multi chu-multi" :class="{'chu-ultima': levelIndex === 4}" bg-key="DMY" :width="920" :background="backgroundOf(levelIndex)" :accent="accent" :style="{'--accent': accent}">
        <VersusSongMeta :song="song"/>
        <div class="flex items-end gap-5 mt-7">
            <MaiCover game="chunithm" :song-id="song.Id" :size="132" :frame-radius="22" :img-radius="16"/>
            <MaiSongHeading :title="song.Title" :artist="song.Artist" :max="84" :min="1"
                            :artist-size="20" :artist-top="12"/>
        </div>
        <div class="flex items-center gap-4 mt-8 mb-4">
            <span class="section-tag">多人对战</span>
            <span class="difficulty" :style="difficultyStyle(levelIndex)">{{ data.Chart.LevelName }} {{ data.Chart.Level }}<small>{{ data.Chart.Constant.toFixed(1) }}</small></span>
            <div class="flex-1 h-[2px] rounded-full bg-white/20"></div>
            <span v-if="data.Round" class="round-badge">ROUND<b>{{ pad(data.Round) }}</b></span>
        </div>

        <div class="racer-table">
            <div class="racer-columns" aria-hidden="true">
                <span class="column-place">PLACE</span>
                <span class="column-identity">PLAYER</span>
                <span class="column-score">SCORE</span>
                <span class="column-rank">RANK</span>
                <span class="column-rating">RATING</span>
                <span class="column-status">COMPLETION</span>
            </div>
            <div class="racers">
                <div v-for="(player, index) in data.Players" :key="index" class="racer mai-accent-rail"
                     :class="{leader: player.Place === 1, unplayed: player.Place == null}"
                     :style="{'--rail-color': placeColor(player.Place)}" :data-player-index="index">
                    <div class="racer-main">
                        <div class="racer-place" :style="{color: placeColor(player.Place)}">
                            <template v-if="player.Place != null"><b>{{ player.Place }}</b><small>{{ placeSuffix(player.Place) }}</small></template>
                            <b v-else>—</b>
                        </div>
                        <div class="racer-identity" :class="{stamped: player.Stamp}">
                            <span class="racer-name" :title="player.Name">{{ player.Name }}</span>
                            <span v-if="player.Stamp" class="racer-stamp-slot"><span class="loser-stamp">菜</span></span>
                        </div>
                        <template v-if="player.Score?.State === 'played'">
                            <div class="score-line">
                                <b class="achievement">{{ formatScore(player.Score.Score) }}</b>
                                <span v-if="player.Place! > 1" class="gap">−{{ formatScore(best - player.Score.Score!) }}</span>
                            </div>
                            <img v-if="player.Score.Rank" class="rank" :src="rankIcon(player.Score.Rank)" alt="">
                            <b class="rating-value">{{ formatRating(player.Score.Rating) }}</b>
                        </template>
                        <div v-else class="racer-empty">未游玩</div>
                        <div v-if="hasMarks(player.Score)" class="completion-row"><VersusMarks family="common" :combo="player.Score?.FullCombo ?? ''" :chain="player.Score?.FullChain ?? ''" :clear="player.Score?.Clear ?? ''"/></div>
                    </div>
                </div>
            </div>
        </div>

        <VersusStandings v-if="data.Standings.length" :standings="data.Standings" :rounds="data.Round" :accent="accent" unranked-empty/>

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
import MaiCardShell from '@/components/maimai/MaiCardShell.vue'
import MaiCover from '@/components/maimai/MaiCover.vue'
import VersusMarks from '@/components/chunithm/partial/VersusMarks.vue'
import MaiSongHeading from '@/components/maimai/MaiSongHeading.vue'
import VersusSongMeta from '@/components/chunithm/partial/VersusSongMeta.vue'
import VersusStandings from '@/components/versus/VersusStandings.vue'
import {backgroundOf, difficultyColor, difficultyStyle, formatScore, formatRating, hasClear, placeColor, placeSuffix, rankIcon, type ChuChart, type ChuScore, type VersusStanding} from '@/components/chunithm/utils/versus'
interface Player { Name: string; Place: number | null; Score: ChuScore | null; Stamp: boolean }
interface MultiData { Chart: ChuChart; Round: number; Players: Player[]; Standings: VersusStanding[] }
const route = useRoute()
const data = ref<MultiData | null>(null)
axios.get(context_get, {params: {id: route.query.id, name: 'chuVersusMulti'}}).then(res => { data.value = typeof res.data === 'string' ? JSON.parse(res.data) : res.data })
const song = computed(() => data.value!.Chart.Song)
const levelIndex = computed(() => data.value?.Chart.LevelIndex ?? 3)
const accent = computed(() => difficultyColor(levelIndex.value))
const best = computed(() => Math.max(...data.value!.Players.map(p => p.Score?.Score ?? 0)))
function pad(n: number) { return n.toString().padStart(2, '0') }
function hasMarks(score: ChuScore | null) {
    return score?.State === 'played' && (
        ['fullcombo', 'alljustice', 'alljusticecritical'].includes(score.FullCombo)
        || ['fullchain', 'fullchain2'].includes(score.FullChain)
        || hasClear(score.Clear)
    )
}
</script>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/versus.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/versus-multi.pcss"/>
