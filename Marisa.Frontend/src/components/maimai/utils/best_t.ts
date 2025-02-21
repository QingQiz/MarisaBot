export interface MaiMaiRating {
    additional_rating: number;
    charts: Charts;
    nickname: string;
    plate: string;
    rating: number;
    user_data: null;
    user_id: null;
    username: string;
}

export interface Charts {
    dx: Score[];
    sd: Score[];
}

export interface Score {
    achievements: number;
    ds: number;
    dxScore: number;
    fc: string;
    fs: string;
    level: string;
    level_index: number;
    level_label: string;
    ra: number;
    rate: string;
    song_id: number;
    title: string;
    type: string;
}

