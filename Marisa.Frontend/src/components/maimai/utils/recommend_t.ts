export interface RecommendationDifficulty {
    Kind: 'fitted_ds' | 'band_pct'
    Value: number
    Personalized: boolean
    Rank: number | null
    Of: number | null
}

export interface RecommendationReplacement {
    SongId: number
    Title: string
    LevelIndex: number
    Achievement: number
    Rating: number
}

export interface RecommendationItem {
    Step: number
    Bucket: 'old' | 'new'
    Action: 'upgrade' | 'entry'
    SongId: number
    Title: string
    Type: string
    IsNew: boolean
    LevelIndex: number
    Level: string
    Constant: number
    CurrentAchievement: number | null
    BaselineRating: number
    TargetAchievement: number
    TargetRating: number
    Gain: number
    Difficulty: RecommendationDifficulty | null
    Replaced: RecommendationReplacement | null
}

export interface RecommendationCardData {
    Mode: 'quick' | 'plan'
    Nickname: string
    CurrentRating: number
    TargetRating: number | null
    ProjectedRating: number | null
    Items: RecommendationItem[]
}
