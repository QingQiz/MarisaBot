import {maimai_levelColors} from '@/GlobalVars'

/**
 * MaiSong（歌曲信息卡）与 MaiSongScore（单曲成绩卡）的公用常量与工具。
 * 两卡布局尺寸各自独立，此处只放取值必须一致的部分：难度名色、类别显示、
 * 版本 logo 素材映射、类型章、曲绘路径与难度主题背景。
 */

export const DIFF_NAMES = ['BASIC', 'ADVANCED', 'EXPERT', 'MASTER', 'Re:MASTER']

/** 难度色与 b50 ScoreCard 同源（GlobalVars.maimai_levelColors），改一处全部生效。 */
export const DIFF_COLORS = maimai_levelColors

export const UTAGE = {name: '宴', main: '#f73ee0', deep: '#c41fb4'} as const

export function isUtageId(id: number) {
    return id > 100000
}

/** 卡片主题色：宴谱粉、普通谱按难度 index 取色。 */
export function themeMainOf(idx: number, isUtage: boolean) {
    return isUtage ? UTAGE.main : DIFF_COLORS[idx] ?? '#999'
}

// genre：简中类别名映射成 NewRodin 字库覆盖的日/西文
const GENRE_MAP: Record<string, string> = {
    '流行&动漫':          'POPS&アニメ',
    '舞萌':               'maimai',
    '其他游戏':           'ゲーム&バラエティ',
    '音击&中二节奏':      'オンゲキ&CHUNITHM',
    '东方Project':        '東方Project',
}

export function genreDisplayOf(genre: string | undefined) {
    return GENRE_MAP[genre ?? ''] ?? genre ?? ''
}

// 版本 logo：diving-fish from 字符串 → 游戏内版本标题素材编号。
// DX 时代本传与 PLUS 共用一张（国服 from 已并代）；特例 Splash 用 214
export const VERSION_CODE: Record<string, number> = {
    'maimai': 100,                  'maimai PLUS': 110,
    'maimai GreeN': 120,            'maimai GreeN PLUS': 130,
    'maimai ORANGE': 140,           'maimai ORANGE PLUS': 150,
    'maimai PiNK': 160,             'maimai PiNK PLUS': 170,
    'maimai MURASAKi': 180,         'maimai MURASAKi PLUS': 185,
    'maimai MiLK': 190,             'MiLK PLUS': 195,
    'maimai FiNALE': 199,
    'maimai でらっくす': 200,        'maimai でらっくす Splash': 214,
    'maimai でらっくす UNiVERSE': 220, 'maimai でらっくす FESTiVAL': 230,
    'maimai でらっくす BUDDiES': 240, 'maimai でらっくす PRiSM': 250,
    'maimai でらっくす PRiSM PLUS': 255,
}

// 版本 logo 素材内部左侧透明留白（像素实测 alpha bbox）。
// 各卡按自己的显示倍率补偿，使可见字形左缘与标题左缘对齐。
export const LOGO_BBOX_LEFT: Record<number, number> = {
    100: 22, 110: 21, 120: 20, 130: 20, 140: 20, 150: 14, 160: 21, 170: 21,
    180: 28, 185: 19, 190: 21, 195: 32, 199: 24, 200: 49, 210: 15, 214: 54,
    215: 10, 220: 55, 225: 24, 230: 50, 235: 78, 240: 78, 245: 46, 250: 79, 255: 50,
}

export function versionLogoSrc(from: string | undefined) {
    const code = VERSION_CODE[from ?? '']
    return code
        ? `/assets/maimai/version/Ver${code}.png`
        : '/assets/maimai/version/maimaidx.png'
}

export function typeBadgeSrc(type: string | undefined) {
    return type === 'DX'
        ? '/assets/maimai/pic/mode_dx.png'
        : '/assets/maimai/pic/mode_standard.png'
}

export function coverSrcOf(id: number) {
    return `/assets/maimai/cover/${id}.png`
}

export const COVER_FALLBACK = '/assets/maimai/cover/0.png'

// ── 难度主题暗色背景 ──
const DIFF_KEYS = ['BSC', 'ADV', 'EXP', 'MST', 'MST_Re']
const BG_GRADIENTS: Record<string, [string, string]> = {
    BSC:    ['#123a0a', '#04120a'],
    ADV:    ['#3a2706', '#140d03'],
    EXP:    ['#3d0d14', '#16060a'],
    MST:    ['#2c0b44', '#0e0418'],
    MST_Re: ['#33204f', '#120a20'],
    UTG:    ['#3d0c35', '#150412'],
    DMY:    ['#222633', '#0a0c12'],
}

export function bgKeyOf(topIdx: number, isUtage: boolean) {
    return isUtage ? 'UTG' : DIFF_KEYS[topIdx] ?? 'DMY'
}

/** 卡片根背景：最高难度主题色的深色对角渐变。 */
export function cardBackground(topKey: string) {
    const [c1, c2] = BG_GRADIENTS[topKey] ?? BG_GRADIENTS['DMY']
    return {background: `linear-gradient(168deg, ${c1} 0%, ${c2} 62%, #060309 100%)`}
}
