export interface Score {
    achievements: number
    ds: number
    fc: string
    fs: string
    level: string
    level_index: number
    level_label: string
    ra: number
    rate: string
    id: number
    title: string
    type: string
}

export interface SongInfo {
    Id: number
    Title: string
    Levels: string[]
    Constants: number[]
    Charters: string[]
    Type: string
    Version: string
    Bpm: number
}

export interface GroupedSong {
    Key: string
    x: {
        /** 定数 */
        Item1: number
        /** Level Index 0~4(白/紫/红/黄/绿) */
        Item2: number
        Item3: SongInfo
    }[]
}

/** plate 模式下 server 传过来的查询信息。sum 模式时为 null。 */
export interface PlateInfo {
    /** "Achievement" / "Fc" / "Fs" */
    Dim: 'Achievement' | 'Fc' | 'Fs'
    /** 比较时的 ordinal level（≥ 这个值算达成） */
    Level: number
    /** 标题里显示的阈值名（"SSS+" / "AP" / "FDX" 等） */
    DisplayName: string
}
