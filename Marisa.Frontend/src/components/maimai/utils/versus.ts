/** 多人对战两张卡（单曲排行 / 谱面矩阵）与累计榜的公用类型和取值。 */

export interface VersusScore {
    State: 'played' | 'unplayed'
    Achievement: number | null
    Rank: string | null
    Rating: number | null
    DxScore: number | null
    Fc: string
    Fs: string
}

export interface VersusSong { Id: number; Title: string; Type: string; Artist: string; Genre: string; Bpm: number; From: string; IsNew: boolean }

export interface VersusCell { Score: VersusScore; Place: number | null; Points: number; Matchups: number }

export interface VersusChart { Song: VersusSong; LevelIndex: number; Level: string; Constant: number; MaxDx: number; Cells: VersusCell[] }


const PIC = '/assets/maimai/pic'

export function rankIcon(rank: string | null) {
    return `${PIC}/rank_${(rank ?? 'd').toLowerCase().replaceAll('+', 'p')}.png`
}

export function markIcon(name: string) {
    return `${PIC}/icon_${name}.png`
}

export function starIcon(star: number) {
    return `${PIC}/music_icon_dxstar_${star}.png`
}

export {placeColor, placeSuffix, formatPoints} from "@/components/versus/utils"
export type {VersusStanding} from "@/components/versus/utils"
