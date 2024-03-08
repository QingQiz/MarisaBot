<template>
    <template v-if="data_fetched">
        <div class="root flex flex-col gap-[35px]">
            <div class="info-container gap-12">
                <div class="title-container gap-8">
                    <!-- title -->
                    <div class="title">
                        {{ beatmap.BeatmapInfo.Metadata.title_unicode }}
                    </div>
                    <!-- artist -->
                    <div class="artist">
                        {{ beatmap.BeatmapInfo.Metadata.Artist }}
                    </div>
                    <div class="flex text-4xl gap-2">
                        <div class="osu-star-rating"
                            :style="`background-color: ${GetDiffColor(info.difficulty_rating)}; color: ${GetDiffTextColor(info.difficulty_rating)}`">
                            {{ info.difficulty_rating.toFixed(2) }}
                        </div>
                        <!-- diff name -->
                        <div class="text-[#65ccfe]">
                            {{ info.version }}
                        </div>
                        <!-- mapper -->
                        <div>
                            mapped by <span class="font-bold text-[#65ccfe]">{{ info.beatmapset.creator }}</span>
                        </div>
                        <div class="px-3 py-0.5 bg-black rounded-lg w-fit bg-opacity-30 no-shadow">
                            #{{ info.id }}
                        </div>
                    </div>
                </div>
                <beatmap-info :beatmap="info" class="osu-preview-beatmap-info" />
            </div>
            <div class="stage-container z-10">
                <div v-for="stage in Math.floor((max_time + time_per_stage - 1) / time_per_stage)">
                    <div class="text-center text-white font-osu-web">
                        {{ (stage * time_per_stage - time_per_stage) / 1000 }}s - {{ (stage * time_per_stage) / 1000 }}s
                    </div>
                    <div class="stage-outter">
                        <div class="stage">
                            <template v-for="point in filter_measures_by_stage(stage)">
                                <div class="measure-line"
                                    :style="`left: 0; bottom: calc(${point}% - var(--note-height))`">
                                </div>
                            </template>
                            <template v-for="point in filter_beats_by_stage(stage)">
                                <div class="beat-line" :style="`left: 0; bottom: calc(${point}% - var(--note-height))`">
                                </div>
                            </template>
                            <template v-for="obj in filter_obj_by_stage(stage)">
                                <div v-if="obj.StartTime > (stage - 1) * time_per_stage" class="note"
                                    :class="`lane-${get_lane(obj.X)}`"
                                    :style="`left: ${get_left(obj)}%; top: ${get_top(obj, stage - 1)}%`"></div>
                                <template v-if="obj.EndTime != null">
                                    <div class="note ln-body"
                                        :class="`lane-${get_lane(obj.X)} ${is_ln_end(obj, stage) ? 'ln-end' : ''}`"
                                        :style="`left: ${get_left(obj)}%; top: ${get_ln_top(obj, stage - 1)}%; height: calc(${get_ln_height(obj, stage - 1)}% + var(--note-height))`">
                                    </div>
                                </template>
                            </template>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </template>
</template>

<style>
.osu-preview-beatmap-info {
    color: white !important;
    font-size: 4rem !important;
}

.osu-preview-beatmap-info img {
    width: 80px !important;
    height: 80px !important;
}
</style>

<style scoped>
.osu-star-rating {
    font-size: 2rem !important;
}

.root {
    --stage-outter-width: var(var(--stage-width));
    --stage-outter-height: calc(var(--stage-height) + var(--note-height));

    --stage-width: calc(var(--note-width) * v-bind(key_count));
    --stage-height: v-bind(stage_height + 'px');

    --note-height: 10px;
    --note-width: 20px;

    --stage-color: rgb(31 41 55);

    --note-color-1: #ffffff;
    --note-color-2: #5e9ecc;
    --note-color-3: #e5c349;

    --gap: 35px;

    @apply flex bg-gray-700;
    gap: var(--gap);
}

.info-container {
    @apply relative text-white font-osu-web flex flex-col p-5 justify-around;
    @apply bg-center bg-cover;
    background-image: linear-gradient(rgba(0, 0, 0, 0.3), rgba(0, 0, 0, 0.3)), v-bind(cover_url());
}

.title-container {
    text-orientation: mixed;

    @apply flex flex-col justify-start items-start;
}

.title-container .title {
    @apply text-9xl;
}

.title-container .artist {
    @apply text-5xl mt-5 text-gray-200;
}

.stage-container {
    @apply flex flex-row items-center justify-center px-[15px];
    gap: var(--gap);
}

.stage-outter {
    width: var(--stage-outter-width);
    height: var(--stage-outter-height);
    background-color: var(--stage-color);

    @apply flex place-content-center border-black border-x-[2px];
}

.stage {
    width: var(--stage-width);
    height: var(--stage-height);

    @apply relative;
}

.measure-line {
    width: 100%;
    height: 3px;

    @apply absolute bg-green-400;
}

.beat-line {
    width: 100%;
    height: 1px;

    @apply absolute bg-red-800;
}

.note {
    width: var(--note-width);
    height: var(--note-height);

    @apply absolute;
}

.note:not(.ln-body)::after {
    content: ' ';
    background-color: var(--note-color-1);

    @apply absolute bottom-0 top-0 left-[1px] right-[1px] rounded-sm;
}

.note.ln-body::after {
    content: ' ';
    background-color: var(--note-color-1);

    @apply absolute top-0 bottom-0 left-[4px] right-[4px];
}

.note.ln-body.ln-end::after {
    @apply rounded-t-lg;
}
</style>

<script setup lang="ts">
import { context_get, osu_beatmapCover_builder, osu_beatmapInfo, GetDiffColor, GetDiffTextColor } from '@/GlobalVars'
import axios from 'axios'
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import BeatmapInfo from './partial/BeatmapInfo.vue';
import { BeatmapInfo as BeatmapInfoT } from './Osu.Data';

const route = useRoute()

let id = ref(route.query.id)
const data_fetched = ref(false)
const error_message = ref('')

let beatmap = ref({} as ManiaBeatmap)
let info = ref({} as BeatmapInfoT)
let max_time = ref(0)
let key_count = ref(0)
let measures = ref([] as number[])
let beats = ref([] as number[])


let time_per_stage = 10000
let stage_height = 4000

let cover_url = () => {
    if (data_fetched.value == false) return ''
    // return `url(${osu_image_builder(info.value.beatmapset.covers["list@2x"])})`
    return `url(${osu_beatmapCover_builder(beatmap.value.BeatmapInfo.BeatmapSet.OnlineID, null, beatmap.value.BeatmapInfo.OnlineID)})`
}

function get_left(x: Item) {
    return get_lane(x.X) / key_count.value * 100
}

/**
 * @param stage start index is 0
 */
function is_ln_end(x: Item, stage: number) {
    let max_time = time_per_stage * stage
    if (x.EndTime! > max_time) return false
    return true
}

/**
 * @param stage start index is 0
 */
function get_ln_top(x: Item, stage: number) {
    let min_time = time_per_stage * stage
    let max_time = min_time + time_per_stage

    if (x.EndTime! > max_time) return 0
    return (time_per_stage - (x.EndTime! - min_time)) / time_per_stage * 100
}

/**
 * @param stage start index is 0
 */
function get_ln_height(x: Item, stage: number) {
    let min_time = time_per_stage * stage

    let start = x.StartTime - min_time
    let end = x.EndTime! - min_time

    start = Math.max(start, 0)
    end = Math.min(end, time_per_stage)

    return (end - start) / time_per_stage * 100
}

function get_top(x: Item, stage: number) {
    return (time_per_stage - (x.StartTime - time_per_stage * stage)) / time_per_stage * 100
}

function get_lane(x: number) {
    return Math.floor(x / (512 / key_count.value))
}

/**
 * @param stage start index is 1
 */
function filter_obj_by_stage(stage: number) {
    let max = stage * time_per_stage
    let min = max - time_per_stage

    return beatmap.value.HitObjects.$items.filter(v => {
        if (v.StartTime > max) return false
        if (v.EndTime != null && v.EndTime < min) return false
        if (v.EndTime == null && v.StartTime < min) return false
        return true
    })
}

/**
 * @param stage start index is 1
 */
function filter_measures_by_stage(stage: number) {
    let max = stage * time_per_stage
    let min = max - time_per_stage

    return measures.value.filter(v => v >= min && v <= max).map(v => (v - min) / time_per_stage * 100)
}

function filter_beats_by_stage(stage: number) {
    let max = stage * time_per_stage
    let min = max - time_per_stage

    return beats.value.filter(v => v >= min && v <= max).map(v => (v - min) / time_per_stage * 100)
}

function generate_timing_points() {
    let measures = [] as number[]
    let beats = [] as number[]

    let beat_length = 0;
    let numerator = 4;

    for (let i = 0; i < beatmap.value.ControlPointInfo.Groups.length; i++) {
        let max = i == beatmap.value.ControlPointInfo.Groups.length - 1
            ? max_time.value
            : beatmap.value.ControlPointInfo.Groups[i + 1].Time


        for (let j of beatmap.value.ControlPointInfo.Groups[i].ControlPoints) {
            if (j.BeatLength != null) {
                beat_length = j.BeatLength
            }
            if (j.TimeSignature != null) {
                numerator = j.TimeSignature.Numerator
            }
        }

        let time_measure = beatmap.value.ControlPointInfo.Groups[i].Time
        while (time_measure < max) {
            measures.push(time_measure)
            time_measure += beat_length * numerator;
        }

        let time_beat = beatmap.value.ControlPointInfo.Groups[i].Time
        while (time_beat < max) {
            for (let j = 1; j < numerator; j++) {
                beats.push(time_beat + beat_length * j)
            }
            time_beat += beat_length * numerator;
        }
    }
    return [measures, beats]
}


axios.get(context_get, { params: { id: id.value, name: "beatmap" } })
    .then(data => {
        let d = data.data as ManiaBeatmap;

        // sort ControlPointInfo.Groups by Time
        d.ControlPointInfo.Groups.sort((a, b) => a.Time - b.Time)

        // sort HitObjects.$items by StartTime, then by EndTime if not null
        d.HitObjects.$items.sort((a, b) => {
            if (a.StartTime == b.StartTime) {
                if (a.EndTime == null) {
                    return -1
                } else if (b.EndTime == null) {
                    return 1
                } else {
                    return a.EndTime - b.EndTime
                }
            } else {
                return a.StartTime - b.StartTime
            }
        })

        beatmap.value = d

        max_time.value = Math.max(...[
            ...d.HitObjects.$items.map(v => v.EndTime ?? v.StartTime),
            ...d.ControlPointInfo.Groups.map(v => v.Time)
        ])

        key_count.value = d.BeatmapInfo.Difficulty.CircleSize

        let [a, b] = generate_timing_points()
        measures.value = a;
        beats.value = b;

        let stage_count = Math.floor((max_time.value + time_per_stage - 1) / time_per_stage)
        while (stage_count < 10) {
            time_per_stage /= 2
            stage_count = Math.floor((max_time.value + time_per_stage - 1) / time_per_stage)
            stage_height /= 2
        }
    })
    .then(_ => axios.get(osu_beatmapInfo, { params: { beatmapId: beatmap.value.BeatmapInfo.OnlineID } }))
    .then(data => { info.value = data.data })
    .catch(err => {
        error_message.value = JSON.stringify(err, null, 4)
    })
    .finally(() => data_fetched.value = true)

function note_color(x: number) {
    switch (key_count.value) {
        case 1:
        case 2:
            return "var(--note-color-1)"
        case 3:
            if (x == 1) return "var(--note-color-2)"
            return "var(--note-color-1)"
        case 4:
            if (x == 1 || x == 2) return "var(--note-color-2)"
            return "var(--note-color-1)"
        case 5:
            if (x == 1 || x == 3) return "var(--note-color-2)"
            if (x == 2) return "var(--note-color-3)"
            return "var(--note-color-1)"
        case 6:
            if (x == 1 || x == 4) return "var(--note-color-2)"
            return "var(--note-color-1)"
        case 7:
            if (x == 1 || x == 5) return "var(--note-color-2)"
            if (x == 3) return "var(--note-color-3)"
            return "var(--note-color-1)"
        case 8:
            if (x == 1 || x == 6) return "var(--note-color-2)"
            if (x == 3 || x == 4) return "var(--note-color-3)"
            return "var(--note-color-1)"
        case 9:
            if (x == 1 || x == 7) return "var(--note-color-2)"
            if (x == 3 || x == 5) return "var(--note-color-2)"
            if (x == 4) return "var(--note-color-3)"
            return "var(--note-color-1)"
        case 10:
            if (x == 1 || x == 8) return "var(--note-color-2)"
            if (x == 3 || x == 6) return "var(--note-color-2)"
            if (x == 4 || x == 5) return "var(--note-color-3)"
            return "var(--note-color-1)"
        default:
            return "var(--note-color-1)"
    }
}


</script>

<style scoped>
.note.lane-0::after {
    background-color: v-bind(note_color(0));
}

.note.lane-1::after {
    background-color: v-bind(note_color(1));
}

.note.lane-2::after {
    background-color: v-bind(note_color(2));
}

.note.lane-3::after {
    background-color: v-bind(note_color(3));
}

.note.lane-4::after {
    background-color: v-bind(note_color(4));
}

.note.lane-5::after {
    background-color: v-bind(note_color(5));
}

.note.lane-6::after {
    background-color: v-bind(note_color(6));
}

.note.lane-7::after {
    background-color: v-bind(note_color(7));
}

.note.lane-8::after {
    background-color: v-bind(note_color(8));
}

.note.lane-9::after {
    background-color: v-bind(note_color(9));

}
</style>


<script lang="ts">
interface ManiaBeatmap {
    Difficulty: Difficulty;
    BeatmapInfo: BeatmapInfo;
    ControlPointInfo: ControlPointInfo;
    HitObjects: HitObjects;
}

interface BeatmapInfo {
    BeatmapVersion: number;
    DifficultyName: string;
    Ruleset: Ruleset;
    Difficulty: Difficulty;
    Metadata: Metadata;
    UserSettings: UserSettings;
    BeatmapSet: BeatmapSet;
    File: null;
    Status: number;
    StatusInt: number;
    OnlineID: number;
    Length: number;
    BPM: number;
    Hash: string;
    StarRating: number;
    MD4Hash: string;
    OnlineMD4Hash: string;
    LastLocalUpdate: null;
    LastOnlineUpdate: null;
    MatchesOnlineVersion: boolean;
    AudioLeadIn: number;
    StackLeniency: number;
    SpecialStyle: boolean;
    LetterboxInBreaks: boolean;
    WidescreenStoryboard: boolean;
    EpilepsyWarning: boolean;
    SamplesMatchPlaybackRate: boolean;
    LastPlayed: null;
    DistanceSpacing: number;
    BeatDivisor: number;
    GridSize: number;
    TimelineZoom: number;
    Countdown: number;
    CountdownOffset: number;
    BaseDifficulty: Difficulty;
    Path: null;
    OnlineInfo: null;
    MaxCombo: null;
    Bookmarks: any[];
}

interface Difficulty {
    DrainRate: number;
    CircleSize: number;
    OverallDifficulty: number;
    ApproachRate: number;
    SliderMultiplier: number;
    SliderTickRate: number;
    Parent: null;
}

interface BeatmapSet {
    OnlineID: number;
    DateAdded: string;
    DateSubmitted: null;
    DateRanked: null;
    Beatmaps: any[];
    Files: any[];
    Status: number;
    StatusInt: number;
    DeletePending: boolean;
    Hash: string;
    Protected: boolean;
    MaxStarDifficulty: number;
    MaxLength: number;
    MaxBPM: number;
    AllBeatmapsUpToDate: boolean;
}

interface Metadata {
    Title: string;
    title_unicode: string;
    Artist: string;
    artist_unicode: string;
    Author: Author;
    Source: string;
    tags: string;
    PreviewTime: number;
    AudioFile: string;
    BackgroundFile: string;
}

interface Author {
    OnlineID: number;
    Username: string;
    CountryCode: string;
    CountryString: string;
    IsBot: boolean;
    Parent: null;
}

interface Ruleset {
    ShortName: string;
    OnlineID: number;
    Name: string;
    InstantiationInfo: string;
    LastAppliedDifficultyVersion: number;
    Available: boolean;
}

interface UserSettings {
    Offset: number;
    Parent: null;
}

interface ControlPointInfo {
    SamplePoints: SamplePoint[];
    DifficultyPoints: any[];
    Groups: Group[];
    TimingPoints: TimingPoint[];
    EffectPoints: EffectPoint[];
}

interface EffectPoint {
    OmitFirstBarLineBindable: boolean;
    ScrollSpeedBindable: number;
    KiaiModeBindable: boolean;
    ScrollSpeed: number;
    OmitFirstBarLine: boolean;
    KiaiMode: boolean;
}

interface Group {
    Time: number;
    ControlPoints: ControlPoint[];
}

interface ControlPoint {
    TimeSignatureBindable?: TimeSignature;
    BeatLengthBindable?: number;
    TimeSignature?: TimeSignature;
    BeatLength?: number;
    BPM?: number;
    CustomSampleBank?: number;
    SampleBankBindable?: string;
    SampleVolumeBindable?: number;
    SampleBank?: string;
    SampleVolume?: number;
    OmitFirstBarLineBindable?: boolean;
    ScrollSpeedBindable?: number;
    KiaiModeBindable?: boolean;
    ScrollSpeed?: number;
    OmitFirstBarLine?: boolean;
    KiaiMode?: boolean;
}

interface TimeSignature {
    Numerator: number;
}

interface SamplePoint {
    CustomSampleBank: number;
    SampleBankBindable: string;
    SampleVolumeBindable: number;
    SampleBank: string;
    SampleVolume: number;
}

interface TimingPoint {
    TimeSignatureBindable: TimeSignature;
    BeatLengthBindable: number;
    TimeSignature: TimeSignature;
    BeatLength: number;
    BPM: number;
}

interface HitObjects {
    $lookup_table: string[];
    $items: Item[];
}

interface Item {
    $type: number;
    StartTimeBindable: number;
    SamplesBindable: Sample[];
    SampleControlPoint: SamplePoint;
    DifficultyControlPoint: DifficultyControlPoint;
    X: number;
    StartTime: number;
    Samples: Sample[];
    AuxiliarySamples: any[];
    Duration?: number;
    EndTime?: number;
}

interface DifficultyControlPoint {
    SliderVelocityBindable: number;
    SliderVelocity: number;
}

interface Sample {
    CustomSampleBank: number;
    IsLayered: boolean;
    Name: string;
    Bank: null;
    Suffix: null;
    Volume: number;
}
</script>