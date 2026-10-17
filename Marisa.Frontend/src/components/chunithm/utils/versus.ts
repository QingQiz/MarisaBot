import type {ChuSongHeaderData} from '@/components/chunithm/utils/song-card'
import type {ChuScore} from '@/components/chunithm/utils/score'
import type {VersusStanding} from '@/components/versus/utils'

export interface ChuSong extends ChuSongHeaderData { Bpm: string }
export interface ChuCell { Score: ChuScore; Place: number | null; Points: number; Matchups: number }
export interface ChuChart { Song: ChuSong; LevelIndex: number; LevelName: string; ChartIndex: number; Level: string; Constant: number; Cells: ChuCell[] }
export type {VersusStanding}
export {placeColor, placeSuffix, formatPoints} from '@/components/versus/utils'
