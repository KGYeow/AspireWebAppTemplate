/**
 * Detects whether the operating system / browser prefers a dark color scheme.
 * @returns {boolean} true if the user's OS is set to dark mode.
 */
export function getSystemPrefersDark() {
    try {
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    } catch {
        return false;
    }
}
