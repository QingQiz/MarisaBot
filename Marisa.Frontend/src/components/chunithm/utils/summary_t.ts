export interface Score {
    cid: number;
    ds: number;
    fc: string;
    level: string;
    level_index: number;
    level_label: string;
    mid: number;
    ra: number;
    score: number;
    title: string;
    Rank: string;
}

export interface GroupSongInfo {
    /**
     * 定数
     */
    Item1: number;
    /**
     * Level Index
     */
    Item2: number;
    Item3: SongInfo;
}

export interface SongInfo {
    Genre: string;
    DiffNames: string[];
    MaxCombo: number[];
    Bpm: string;
    Id: number;
    Title: string;
    Artist: string;
    Constants: number[];
    Levels: string[];
    Charters: string[];
    Version: string;
}
