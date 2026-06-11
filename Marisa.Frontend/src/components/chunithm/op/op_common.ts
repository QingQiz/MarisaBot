import {ref, computed} from "vue";
import {GroupSongInfo, Score} from "../utils/summary_t";
import {calcOverPower} from "../utils/overpower";
import axios from "axios";
import {context_get} from "@/GlobalVars";
import {useRoute} from "vue-router";

export function useOpData() {
    const route = useRoute()
    const id    = ref(route.query.id)

    const data_fetched = ref(false)

    const songs  = ref([] as GroupSongInfo[])
    const scores = ref({} as { [key: string]: Score })

    axios.all([
        axios.get(context_get, {params: {id: id.value, name: 'OverPowerSongs'}}),
        axios.get(context_get, {params: {id: id.value, name: 'OverPowerScores'}}),
    ]).then(data => {
        songs.value  = data[0].data
        scores.value = data[1].data
    }).finally(() => {
        data_fetched.value = true
    })

    function GetScore(id: number, level: number) {
        return scores.value[`(${id}, ${level})`]
    }

    const maxConstMap = computed(() => {
        const map = new Map<number, number>();
        for (const s of songs.value) {
            const id = s.Item3.Id;
            if (!map.has(id) || map.get(id)! < s.Item1) {
                map.set(id, s.Item1);
            }
        }
        return map;
    })

    function filterBestOP(entries: GroupSongInfo[]): { group: GroupSongInfo[], scores: Score[] } {
        const map = new Map<number, { song: GroupSongInfo, score: Score | null, op: number }>();

        for (const e of entries) {
            const score = GetScore(e.Item3.Id, e.Item2);
            const key = e.Item3.Id;

            if (!score) {
                if (!map.has(key)) {
                    map.set(key, { song: e, score: null, op: 0 });
                } else if (!map.get(key)!.score && e.Item1 > map.get(key)!.song.Item1) {
                    map.set(key, { song: e, score: null, op: 0 });
                }
                continue;
            }

            const op = calcOverPower(score);
            if (!map.has(key) || (map.get(key)!.op || -1) < op) {
                map.set(key, { song: e, score, op });
            }
        }

        const group: GroupSongInfo[] = [], scs: Score[] = [];
        for (const [, v] of map) { group.push(v.song); scs.push(v.score as Score); }
        return { group, scores: scs };
    }

    function buildGroups(entries: GroupSongInfo[], keyFn: (s: GroupSongInfo) => string): { label: string, group: GroupSongInfo[], scores: Score[] }[] {
        const groups = new Map<string, GroupSongInfo[]>();
        for (const e of entries) {
            const k = keyFn(e);
            if (!groups.has(k)) groups.set(k, []);
            groups.get(k)!.push(e);
        }
        const result: { label: string, group: GroupSongInfo[], scores: Score[] }[] = [];
        for (const [label, g] of groups) {
            const f = filterBestOP(g);
            if (f.group.length > 0) {
                result.push({ label, group: f.group, scores: f.scores });
            }
        }
        return result;
    }

    return { data_fetched, songs, scores, GetScore, maxConstMap, filterBestOP, buildGroups };
}
