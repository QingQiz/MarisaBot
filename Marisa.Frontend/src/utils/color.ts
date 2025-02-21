export function MakeRgba(rgb: { r: number, g: number, b: number }, a ?: number) {
    if (a == undefined) {
        return `rgb(${rgb.r},${rgb.g},${rgb.b})`
    }
    return `rgba(${rgb.r},${rgb.g},${rgb.b},${a})`
}

/**
 * 根据给定的 RGB 颜色值来确定与之对比的文本颜色（黑色或白色）
 * @param rgb
 * @constructor
 */
export function GetContrastingTextColor(rgb: { r: number, g: number, b: number }) {
    let {r, g, b} = rgb;
    if (r * 0.299 + g * 0.587 + b * 0.114 > 186) {
        return '#000000'
    } else {
        return '#ffffff';
    }
}
