export interface VersusStanding { Name: string; Wins: number; Matchups: number; Rounds: number; Left: boolean; Rate: number | null }

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
