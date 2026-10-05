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

export interface VersusStanding { Name: string; Wins: number; Matchups: number; Rounds: number; Left: boolean; Rate: number | null }

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

/** 前三名的奖牌色，其余名次用中性色。 */
const PLACE_COLORS = ['#ffd75e', '#dce4f0', '#f2a46c']

export function placeColor(place: number | null) {
    return place != null && place <= 3 ? PLACE_COLORS[place - 1] : 'rgba(255,255,255,.5)'
}

export function placeSuffix(place: number) {
    if (place % 100 >= 11 && place % 100 <= 13) return 'TH'
    return ['TH', 'ST', 'ND', 'RD'][place % 10] ?? 'TH'
}

export function formatPoints(points: number) {
    return Number.isInteger(points) ? points.toString() : points.toFixed(1)
}
