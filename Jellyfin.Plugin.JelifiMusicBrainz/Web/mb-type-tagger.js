/* Jelifi MusicBrainz – mb-type card tagger
 *
 * Reads mb-type-* Jellyfin tags for every album card in the web UI and adds
 * matching CSS classes so that a custom stylesheet can group / style albums by
 * release type on artist pages (or anywhere else cards are rendered).
 *
 * Example classes applied to a card element:
 *   mb-type-album   mb-type-single   mb-type-ep   mb-type-live
 *   mb-type-compilation   mb-type-soundtrack   mb-type-remix
 *
 * This script is injected into index.html at server startup by HtmlModifier.cs.
 * It is an unofficial mechanism – Jellyfin does not formally support plugins
 * altering the web client HTML.
 */
(function () {
    'use strict';

    // Keyed by Jellyfin item ID; value is the array of mb-type-* class names
    // for that item (may be empty if the item has no mb-type-* tags).
    const tagCache = new Map();

    /**
     * Batch-fetch Tags for item IDs not yet in tagCache, then populate the cache.
     * Uses the standard /Items?Ids=...&Fields=Tags endpoint – no plugin-specific
     * API is required, so the script remains harmless even if the plugin is removed.
     */
    async function populateCache(ids) {
        const missing = ids.filter(id => !tagCache.has(id));
        if (!missing.length) return;

        try {
            const url = ApiClient.getUrl('Items', {
                Ids: missing.join(','),
                Fields: 'Tags',
                Limit: missing.length,
            });
            const result = await ApiClient.getJSON(url);
            for (const item of result.Items ?? []) {
                tagCache.set(item.Id, (item.Tags ?? []).filter(t => t.startsWith('mb-type-')));
            }
        } catch (_) {
            // Network or auth errors – not critical, cards just stay unclassified
        }

        // Mark IDs that returned no tags so we don't re-fetch them on the next run
        for (const id of missing) {
            if (!tagCache.has(id)) tagCache.set(id, []);
        }
    }

    /**
     * Collect all [data-id] elements currently in the DOM, fetch their tags,
     * then apply / refresh the mb-type-* CSS classes on each card.
     */
    async function tagCards() {
        const cards = [...document.querySelectorAll('[data-id]')];
        if (!cards.length) return;

        const ids = [...new Set(cards.map(c => c.dataset.id).filter(Boolean))];
        await populateCache(ids);

        for (const card of cards) {
            const types = tagCache.get(card.dataset.id);
            if (!types?.length) continue;

            // Remove stale mb-type-* classes left over from a previous run or
            // a metadata refresh that changed the type.
            for (const cls of [...card.classList]) {
                if (cls.startsWith('mb-type-')) card.classList.remove(cls);
            }
            card.classList.add(...types);
        }
    }

    function debounce(fn, ms) {
        let timer;
        return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
    }

    // 400 ms quiet period prevents flooding on rapid DOM mutations (e.g. virtual
    // scroll, image lazy-loads).
    const debouncedTagCards = debounce(tagCards, 400);

    /**
     * Kick off once ApiClient is available and the user is logged in.
     * The SPA bootstraps asynchronously after our <script> runs, so we poll.
     */
    function init() {
        if (typeof ApiClient === 'undefined' || !ApiClient.isLoggedIn?.()) {
            setTimeout(init, 500);
            return;
        }

        // Watch for cards added by SPA navigation or lazy rendering
        new MutationObserver(debouncedTagCards)
            .observe(document.body, { childList: true, subtree: true });

        // Re-tag on hash-based page transitions (Jellyfin's SPA router)
        window.addEventListener('hashchange', debouncedTagCards);

        tagCards();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        // Script injected after DOMContentLoaded – defer slightly so the SPA
        // module graph has a chance to initialise ApiClient.
        setTimeout(init, 0);
    }
})();
