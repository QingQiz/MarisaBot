/**
 * SDGB 解包的 ma2 谱面存在两代命令词表：FESTiVAL 前的旧谱使用无前缀命令
 * （TAP/HLD/STR/…），新谱与宴谱使用带修饰前缀的命令（NMTAP/BRTAP/EXHLD/…）。
 * vendored 的 Ma2Parser 只认带前缀词表，本函数把旧词表逐行改写为等价的新词表，
 * 行内其余字段两代格式完全一致，不做改动。
 */

const SLIDE_SHAPES = [
    "SI_", "SCR", "SCL", "SXR", "SXL", "SUL", "SUR",
    "SV_", "SVP", "SF_", "SWF", "SSL", "SSR", "SLL", "SLR",
] as const;

const BARE_COMMAND_MAP: Record<string, string> = {
    // 按键音符：普通 / 绝赞 / EX / 绝赞EX
    TAP: "NMTAP", BRK: "BRTAP", XTP: "EXTAP", BXX: "BXTAP",
    HLD: "NMHLD", BHO: "BRHLD", XHO: "EXHLD", BXH: "BXHLD",
    // 星星头（滑条起点）
    STR: "NMSTR", BST: "BRSTR", XST: "EXSTR", XBS: "BXSTR",
    // 触摸
    TTP: "NMTTP", THO: "NMTHO",
};

for (const shape of SLIDE_SHAPES) {
    BARE_COMMAND_MAP[shape] = `NM${shape}`;
}

export function normalizeMa2Commands(ma2Text: string): string {
    return ma2Text
        .split(/\r?\n/)
        .map(line => {
            const tabIndex = line.indexOf("\t");
            if (tabIndex <= 0) return line;

            const mapped = BARE_COMMAND_MAP[line.slice(0, tabIndex)];
            return mapped ? mapped + line.slice(tabIndex) : line;
        })
        .join("\n");
}
