export interface Score {
    achievements: number
    ds: number
    dxScore: number
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

/**
 * Vue 端读到的歌曲投影（MaiMaiDraw.ProjectSong 投出，per-chart）。
 * 历史 SongInfo 还含 Levels/Constants/Charters 等全量字段，但 Vue 只读 Id；
 * PR #40 起收窄为最小集 Id/Title/MaxDx（MaxDx = song.Charts[levelIdx].Notes.Sum() * 3）。
 */
export interface SongInfo {
    Id: number
    Title: string
    MaxDx: number
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
    /** "Achievement" / "Fc" / "Fs" / "DxScore" */
    Dim: 'Achievement' | 'Fc' | 'Fs' | 'DxScore'
    /** 比较时的 ordinal level（≥ 这个值算达成） */
    Level: number
    /** 标题里显示的阈值名（"SSS+" / "AP" / "FDX" 等） */
    DisplayName: string
    /** 游戏内成就姓名框贴图文件名（如 "UI_Plate_006125.png"）；查询无对应姓名框时为 null */
    NamePlateImg: string | null
}
