import * as d3 from 'd3';

export const host = "http://localhost:14311/Api"

export const context_get      = host + "/WebContext/Get"
export const maimai_newRa     = host + "/MaiMai/RaNew"
export const osu_pp           = host + '/Osu/PerformanceCalculator'
export const osu_maniaPpChart = host + '/Osu/ManiaPpChart'
export const osu_userInfo     = host + '/Osu/GetUserInfo'
export const osu_accRing      = host + '/Osu/GetAccRing'
export const osu_modIcon      = host + '/Osu/GetModIcon'
export const osu_beatmapCover = host + '/Osu/GetCover'
export const osu_recent       = host + '/Osu/GetRecent'
export const osu_best         = host + '/Osu/GetBest'
export const osu_beatmapInfo  = host + '/Osu/GetBeatmapInfo'
export const osu_getRecommend = host + '/Osu/GetRecommend'
export const osu_getImage     = host + '/Osu/GetImage'

export function osu_accRing_builder(acc: number, modeInt: number) {
    return `${osu_accRing}?acc=${acc}&modeInt=${modeInt}`;
}

export function osu_modIcon_builder(mod: string, withText: boolean = true) {
    if (withText) return `${osu_modIcon}?mod=${mod}`;
    return `${osu_modIcon}?mod=${mod}&withText=false`;
}

export function osu_maniaPpChart_builder(beatmapsetId: number, beatmapChecksum: string, beatmapId: number, mods: string[], totalHits: number) {
    return `${osu_maniaPpChart}?beatmapsetId=${beatmapsetId}&beatmapChecksum=${beatmapChecksum}&beatmapId=${beatmapId}&${mods.map(x => `mods=${x}`).join('&')}&totalHits=${totalHits}`;
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

export function osu_image_builder(uri: string) {
    return `${osu_getImage}?uri=${encodeURIComponent(uri)}`;
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
        `https://www.diving-fish.com/covers/${id}.png`,
        `https://www.diving-fish.com/covers/${id - 10000}.png`,
        `https://www.diving-fish.com/covers/${id + 10000}.png`,
        `/assets/maimai/cover/0.png`,
    ]
}

// see https://github.com/cl8n/osu-web/blob/94e14a47fc2606b1f3ddf45acf86b2677b881aec/resources/assets/lib/utils/beatmap-helper.ts#L23
const difficultyColourSpectrum = d3.scaleLinear<string>()
    .domain([0.1, 1.25, 2, 2.5, 3.3, 4.2, 4.9, 5.8, 6.7, 7.7, 9])
    .clamp(true)
    .range(['#4290FB', '#4FC0FF', '#4FFFD5', '#7CFF4F', '#F6F05C', '#FF8068', '#FF4E6F', '#C645B8', '#6563DE', '#18158E', '#000000'])
    .interpolate(d3.interpolateRgb.gamma(2.2));

export function GetDiffColor(rating: number) {
    if (isNaN(rating)) return '#AAAAAA';
    if (rating < 0.1) return '#AAAAAA';
    if (rating >= 9) return '#000000';
    return difficultyColourSpectrum(rating);
}

export function GetDiffTextColor(sr: number) {
    if (isNaN(sr)) return '#000000';
    return sr >= 7.5 ? '#ffd996' : '#000000';
}
