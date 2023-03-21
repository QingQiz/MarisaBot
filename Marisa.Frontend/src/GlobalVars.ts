export const host = "http://localhost:14311"

export const maimai_newRa = host + "/MaiMai/RaNew"
export const osu_pp = host + '/osu/PerformanceCalculator'
export const osu_maniaPpChart = host + '/Osu/ManiaPpChart'
export const osu_userInfo = host + '/osu/GetUserInfo'
export const osu_accRing = host + '/osu/GetAccRing'
export const osu_modIcon = host + '/osu/GetModIcon'
export const osu_beatmapCover = host + '/osu/GetCover'
export const osu_recent = host + '/osu/GetRecent'
export const osu_best = host + '/osu/GetBest'
export const osu_beatmapInfo = host + '/osu/GetBeatmapInfo'

export function osu_accRing_builder(acc: number, modeInt: number) {
    return `${osu_accRing}?acc=${acc}&modeInt=${modeInt}`;
}

export function osu_modIcon_builder(mod: string) {
    return `${osu_modIcon}?mod=${mod}`;
}

export function osu_maniaPpChart_builder(beatmapsetId: number, beatmapChecksum: string, beatmapId: number, mods: string[]) {
    return `${osu_maniaPpChart}?beatmapsetId=${beatmapsetId}&beatmapChecksum=${beatmapChecksum}&beatmapId=${beatmapId}&${mods.map(x => `mods=${x}`).join('&')}`;
}

export function osu_pp_builder(beatmapsetId: number, beatmapChecksum: string, beatmapId: number, modeInt: number, mods: string[], acc: number, maxCombo: number, cMax: number, c300: number, c200: number, c100: number, c50: number, cMiss: number, score: number) {
    return `${osu_pp}?beatmapsetId=${beatmapsetId}&beatmapChecksum=${beatmapChecksum}&beatmapId=${beatmapId}&modeInt=${modeInt}&${mods.map(x => `mods=${x}`).join('&')}&acc=${acc}&maxCombo=${maxCombo}&cMax=${cMax}&c300=${c300}&c200=${c200}&c100=${c100}&c50=${c50}&cMiss=${cMiss}&score=${score}`;
}

export function PpAcc(c_300p: number, c_300: number, c_200: number, c_100: number, c_50: number, c_miss: number) {
    return (320 * c_300p + 300 * c_300 + 200 * c_200 + 100 * c_100 + 50 * c_50)
        / (320 * (c_300p + c_300 + c_200 + c_100 + c_50 + c_miss));
}

export function osu_beatmapCover_builder(beatmapsetId: number, beatmapChecksum: string, beatmapId: number) {
    return `${osu_beatmapCover}?beatmapsetId=${beatmapsetId}&beatmapChecksum=${beatmapChecksum}&beatmapId=${beatmapId}`;
}

export const maimai_levelColors = [
    '#52e72b',
    '#ffa801',
    '#ff5a66',
    '#c64fe4',
    '#dbaaff'
]

export function maimai_alternativeCover(id: number) {
    return [
        `/assets/maimai/cover/${id}.png`,
        `/assets/maimai/cover/${id}.jpg`,
        `/assets/maimai/cover/${(id ?? 0) + 10000}.jpg`,
        `/assets/maimai/cover/${(id ?? 0) + 10000}.png`,
        `/assets/maimai/cover/${(id ?? 0) - 10000}.jpg`,
        `/assets/maimai/cover/${(id ?? 0) - 10000}.png`,
        `/assets/maimai/cover/0.png`,
    ]
}