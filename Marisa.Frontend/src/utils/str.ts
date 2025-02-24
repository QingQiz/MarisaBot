export function NullOrWhitespace(str: string) {
    return str == null || str.match(/^\s*$/) !== null;
}

export function ToFixedNoRound(num: number, digits: number): string {
    const parts = num.toString().split(".");
    if (parts.length === 1 || digits === 0) {
        return parts[0] + (digits > 0 ? "." + "0".repeat(digits) : "");
    }

    const integerPart = parts[0];
    const decimalPart = parts[1].substring(0, digits).padEnd(digits, '0');

    return integerPart + '.' + decimalPart;
}