import type {ChuSongHeaderData} from '@/components/chunithm/utils/song-card'
import type {VersusStanding} from '@/components/versus/utils'

export interface ChuScore {
    State: 'played' | 'unplayed'
    Score: number | null
    Rank: string | null
    Rating: number | null
    FullCombo: string
    FullChain: string
    Clear: string | null
}
export interface ChuSong extends ChuSongHeaderData { Bpm: string }
export interface ChuCell { Score: ChuScore; Place: number | null; Points: number; Matchups: number }
export interface ChuChart { Song: ChuSong; LevelIndex: number; LevelName: string; ChartIndex: number; Level: string; Constant: number; Cells: ChuCell[] }
export type {VersusStanding}
export {placeColor, placeSuffix, formatPoints} from '@/components/versus/utils'
export {rankIcon} from '@/components/chunithm/utils/score'

const scoreFormat = new Intl.NumberFormat('en-US')
export function formatScore(score: number | null) { return score == null ? '—' : scoreFormat.format(score) }
export function formatRating(rating: number | null) { return rating == null ? '—' : rating.toFixed(2) }
export function hasClear(clear?: string | null) { return ['clear', 'hard', 'brave', 'absolute', 'catastrophy', 'failed'].includes(clear ?? '') }
