export function renderDoc(text: string): string {
    return text
        .replace(/`([^`]+)`/g, '<span class="text-cyan-400">$1</span>')
        .replace(/\*\*([^*]+)\*\*/g, '<span class="font-bold text-white">$1</span>')
}