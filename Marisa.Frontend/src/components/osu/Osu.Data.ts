export {
    BeatmapInfo,
    UserInfo,
    ScoreSimple,
    Score,
    RecommendData,
    ManiaRecommendData,
    OsuRecommendData
}

interface BeatmapInfo extends ScoreBeatmapInfo {
    beatmapset: BeatmapInfoBeatmapset;
    failtimes:  FailTimes;
    max_combo:  number;
}

interface ScoreBeatmapInfo {
    beatmapset_id:     number;
    difficulty_rating: number;
    id:                number;
    mode:              string;
    status:            string;
    total_length:      number;
    user_id:           number;
    version:           string;
    accuracy:          number;
    ar:                number;
    bpm:               number;
    convert:           boolean;
    count_circles:     number;
    count_sliders:     number;
    count_spinners:    number;
    cs:                number;
    deleted_at:        null;
    drain:             number;
    hit_length:        number;
    is_scoreable:      boolean;
    last_updated:      Date;
    mode_int:          number;
    passcount:         number;
    playcount:         number;
    ranked:            number;
    url:               string;
    checksum:          string;
}

interface BeatmapInfoBeatmapset extends ScoreBeatmapset {
    bpm:                 number;
    can_be_hyped:        boolean;
    deleted_at:          null;
    discussion_enabled:  boolean;
    discussion_locked:   boolean;
    is_scoreable:        boolean;
    last_updated:        Date;
    legacy_thread_url:   string;
    nominations_summary: NominationsSummary;
    ranked:              number;
    ranked_date:         Date;
    storyboard:          boolean;
    submitted_date:      Date;
    tags:                string;
    availability:        Availability;
    ratings:             number[];
}

interface Availability {
    download_disabled: boolean;
    more_information:  null;
}

interface BeatmapsetCovers {
    cover:          string;
    "cover@2x":     string;
    card:           string;
    "card@2x":      string;
    list:           string;
    "list@2x":      string;
    slimcover:      string;
    "slimcover@2x": string;
}

interface NominationsSummary {
    current:  number;
    required: number;
}

interface FailTimes {
    fail: number[];
    exit: number[];
}

interface ScoreSimple {
    accuracy:                number;
    best_id:                 number;
    created_at:              Date;
    id:                      number;
    max_combo:               number;
    mode:                    string;
    mode_int:                number;
    mods:                    string[];
    passed:                  boolean;
    perfect:                 boolean;
    pp:                      number;
    rank:                    string;
    replay:                  boolean;
    score:                   number;
    statistics:              ScoreStatistics;
    type:                    string;
    user_id:                 number;
    current_user_attributes: CurrentUserAttributes;
}

interface Score extends ScoreSimple {
    beatmap:                 ScoreBeatmapInfo;
    beatmapset:              ScoreBeatmapset;
    user:                    User;
    weight:                  Weight;
}

interface ScoreBeatmapset {
    artist:          string;
    artist_unicode:  string;
    covers:          BeatmapsetCovers;
    creator:         string;
    favourite_count: number;
    hype:            null;
    id:              number;
    nsfw:            boolean;
    offset:          number;
    play_count:      number;
    preview_url:     string;
    source:          string;
    spotlight:       boolean;
    status:          string;
    title:           string;
    title_unicode:   string;
    track_id:        null;
    user_id:         number;
    video:           boolean;
}

interface CurrentUserAttributes {
    pin: null;
}

interface ScoreStatistics {
    count_100:  number;
    count_300:  number;
    count_50:   number;
    count_geki: number;
    count_katu: number;
    count_miss: number;
}

interface User {
    avatar_url:      string;
    country_code:    string;
    default_group:   string;
    id:              number;
    is_active:       boolean;
    is_bot:          boolean;
    is_deleted:      boolean;
    is_online:       boolean;
    is_supporter:    boolean;
    last_visit:      Date;
    pm_friends_only: boolean;
    profile_colour:  null;
    username:        string;
}

interface Weight {
    percentage: number;
    pp:         number;
}

interface UserInfo {
    avatar_url:                           string;
    country_code:                         string;
    default_group:                        string;
    id:                                   number;
    is_active:                            boolean;
    is_bot:                               boolean;
    is_deleted:                           boolean;
    is_online:                            boolean;
    is_supporter:                         boolean;
    last_visit:                           Date;
    pm_friends_only:                      boolean;
    username:                             string;
    cover_url:                            string;
    has_supported:                        boolean;
    join_date:                            Date;
    kudosu:                               Kudosu;
    max_blocks:                           number;
    max_friends:                          number;
    playmode:                             string;
    playstyle:                            string[];
    post_count:                           number;
    profile_order:                        string[];
    country:                              Country;
    cover:                                UserProfileCover;
    account_history:                      any[];
    badges:                               any[];
    beatmap_playcounts_count:             number;
    comments_count:                       number;
    favourite_beatmapset_count:           number;
    follower_count:                       number;
    graveyard_beatmapset_count:           number;
    groups:                               any[];
    guest_beatmapset_count:               number;
    loved_beatmapset_count:               number;
    mapping_follower_count:               number;
    monthly_playcounts:                   MonthlyPlayCount[];
    page:                                 Page;
    pending_beatmapset_count:             number;
    previous_usernames:                   string[];
    ranked_beatmapset_count:              number;
    replays_watched_counts:               any[];
    scores_best_count:                    number;
    scores_first_count:                   number;
    scores_pinned_count:                  number;
    scores_recent_count:                  number;
    statistics:                           UserStatistics;
    support_level:                        number;
    user_achievements:                    UserAchievement[];
    rank_history:                         RankHistory;
    ranked_and_approved_beatmapset_count: number;
    unranked_beatmapset_count:            number;
}

interface Country {
    code: string;
    name: string;
}

interface UserProfileCover {
    custom_url: string;
    url:        string;
}

interface Kudosu {
    total:     number;
    available: number;
}

interface MonthlyPlayCount {
    start_date: Date;
    count:      number;
}

interface Page {
    html: string;
    raw:  string;
}

interface RankHistory {
    mode: string;
    data: number[];
}

interface UserStatistics {
    level:                     Level;
    global_rank:               number;
    pp:                        number;
    ranked_score:              number;
    hit_accuracy:              number;
    play_count:                number;
    play_time:                 number;
    total_score:               number;
    total_hits:                number;
    maximum_combo:             number;
    replays_watched_by_others: number;
    is_ranked:                 boolean;
    grade_counts:              GradeCounts;
    country_rank:              number;
    rank:                      Rank;
    variants:                  Variant[];
}

interface GradeCounts {
    ss:  number;
    ssh: number;
    s:   number;
    sh:  number;
    a:   number;
}

interface Level {
    current:  number;
    progress: number;
}

interface Rank {
    country: number;
}

interface Variant {
    mode:         string;
    variant:      string;
    country_rank: number;
    global_rank:  number;
    pp:           number;
}

interface UserAchievement {
    achieved_at:    Date;
    achievement_id: number;
}

interface RecommendData {
    id:                  string;
    mapName:             string;
    mapLink:             string;
    mapCoverUrl:         string;
    mod:                 string[];
    currentMod:          string[] | null;
    difficulty:          number;
    currentPP:           number | null;
    predictPP:           number;
    accurate:            boolean;
    newRecordPercent:    number;
    ppIncrement:         number;
    passPercent:         number;
    ppIncrementExpect:   number;
}

interface ManiaRecommendData extends RecommendData {
    keyCount:            number;
    currentAccuracy:     number | null;
    predictAccuracy:     number;
    currentSpeed:        number | null;
    currentAccuracyLink: null | string;
}

interface OsuRecommendData extends RecommendData {
    currentScoreLink:  null | string;
    currentScore:      number | null;
}
