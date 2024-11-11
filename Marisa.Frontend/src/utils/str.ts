export function NullOrWhitespace(str: string) {
    return str == null || str.match(/^\s*$/) !== null;
}