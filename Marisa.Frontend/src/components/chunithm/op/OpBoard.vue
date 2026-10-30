<script setup lang="ts">
import {computed} from "vue";
import MaiCardShell from "@/components/maimai/MaiCardShell.vue";
import OverPower from "@/components/chunithm/partial/OverPower.vue";
import {GroupSongInfo, Score} from "@/components/chunithm/utils/summary_t";
import {bgKeyOf, themeMainOf, tallCardBackground} from "@/components/maimai/utils/song_card";

export interface OpRow {
    label: string;
    group: GroupSongInfo[];
    scores: Score[];
    /** 版本页用版本 logo 取代文字标签 */
    logo?: string;
}

const props = defineProps<{
    /** 全部对象（ALL 那一行统计的就是这些） */
    charts: GroupSongInfo[];
    scores: Score[];
    rows: OpRow[];
}>();

// 主题取全部对象里出现最多的难度，与 chusum / maisum 同一套
const themeIndex = computed(() => {
    const count = [0, 0, 0, 0, 0];
    for (const c of props.charts) count[c.Item2] = (count[c.Item2] ?? 0) + 1;
    return count.reduce((best, n, i) => n >= count[best] ? i : best, 0);
});

const bgKey      = computed(() => bgKeyOf(themeIndex.value, false));
const accent     = computed(() => themeMainOf(themeIndex.value, false));
const background = computed(() => tallCardBackground(bgKey.value, accent.value));

// 老 chuop 页第一行就是 ALL，这里保持同样的内容顺序
const allRows = computed<OpRow[]>(() => [
    {label: "ALL", group: props.charts, scores: props.scores},
    ...props.rows,
]);

function RowPlayed(row: OpRow) {
    return row.scores.filter(Boolean).length;
}
</script>

<template>
    <div class="chu-op">
        <MaiCardShell :bg-key="bgKey" :accent="accent" :background="background" :width="1480"
                      :class="{'chu-ultima': themeIndex === 4}">
            <div class="eyebrow">MARISA BOT · OVER POWER</div>

            <div class="flex flex-col gap-6 mt-6">
                <section v-for="row in allRows" :key="row.label">
                    <div class="op-row">
                        <span class="g-capsule" :class="{complete: RowPlayed(row) === row.group.length}">
                            <span class="g-tag">
                                <img v-if="row.logo" :src="row.logo" class="g-logo" :alt="row.label">
                                <template v-else>{{ row.label }}</template>
                            </span>
                            <span class="g-count"><b>{{ RowPlayed(row) }}</b> / {{ row.group.length }}</span>
                        </span>
                        <div class="op-stat">
                            <OverPower :group="row.group" :scores="row.scores" layout="bars"
                                       numbers percent detail show-difficulty/>
                        </div>
                    </div>
                </section>
            </div>

            <footer class="mt-9 flex justify-end">
                <span class="footer-text">MARISA BOT · OVER POWER</span>
            </footer>
        </MaiCardShell>
    </div>
</template>

<style scoped lang="postcss" src="@/assets/css/maimai/song_card.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/theme.pcss"/>
<style scoped lang="postcss" src="@/assets/css/chunithm/summary.pcss"/>

<style scoped lang="postcss">
/* 左侧胶囊 / 中间数字 / 右侧统计条：各自宽度固定，行与行之间对齐 */
.op-row {
    display: flex;
    align-items: center;
    gap: 18px;
}

.op-row > .g-capsule {
    flex: 0 0 260px;
    justify-content: flex-start;
}

.op-row > .op-stat {
    flex: 1 1 auto;
    min-width: 0;
}

/* 版本 logo：统一按横向宽度缩放。原图宽高比从 1.35 到 3.98 都有，按高度定宽高会让新版（近方形）
   显得特别小；宽度定死之后各行 logo 左右一样宽，胶囊宽度也就一致了 */
.g-logo {
    display: block;
    width: 96px;
    height: auto;
    margin: 0 auto;
}

/* 类别页的标签是 2~4 个汉字，30px 时宽度差 20px，缩到 24px 正好都落在胶囊的 136px 最小宽度里 */
.chu-op.genre-tags .g-tag {
    font-size: 24px;
}
</style>
