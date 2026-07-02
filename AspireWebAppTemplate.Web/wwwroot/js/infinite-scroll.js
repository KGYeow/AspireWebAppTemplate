// Infinite scroll using Intersection Observer.
// Watches a sentinel element and invokes a .NET method when it becomes visible.

let observer = null;

export function initialize(sentinelElement, dotNetRef) {
    if (observer) {
        observer.disconnect();
    }

    observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                dotNetRef.invokeMethodAsync('OnScrolledToBottom');
            }
        });
    }, { threshold: 0.1 });

    observer.observe(sentinelElement);
}

export function dispose() {
    if (observer) {
        observer.disconnect();
        observer = null;
    }
}
