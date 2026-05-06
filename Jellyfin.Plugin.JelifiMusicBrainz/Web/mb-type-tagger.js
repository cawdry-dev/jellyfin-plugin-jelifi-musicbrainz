/* Jelifi MusicBrainz – mb-type card tagger
 *
 * On artist detail pages, finds the visible itemsContainer, fetches the
 * first mb-type-* tag for each album card, applies it as a CSS class, and
 * marks the container with mb-sections-active so CSS rules can group albums
 * into sections by release type.
 *
 * Injected into index.html at server startup by HtmlModifier.cs.
 */
(function () {
    'use strict';

    const LOG = '[JelifiMB]';
    console.log(LOG, 'mb-type-tagger.js loaded');

    // item ID → first mb-type-* string (e.g. "mb-type-album"), or "" if none.
    // Storing a single string keeps the API simple and puts each album in
    // exactly one section.
    const tagCache = new Map();

    // ------------------------------------------------------------------
    // Returns the visible itemsContainer on an artist detail page, or null.
    // Requirements:
    //   • .detailPageContent must exist in DOM (artist/detail page)
    //   • container must have offsetWidth > 0 || offsetHeight > 0 (visible)
    // ------------------------------------------------------------------
    function findContainer() {
        const detailPage = document.querySelector('.detailPageContent');
        if (!detailPage) return null;

        for (const el of detailPage.querySelectorAll('.itemsContainer')) {
            if (el.offsetWidth > 0 || el.offsetHeight > 0) {
                return el;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------
    // Fetch the first mb-type-* tag for any IDs not yet in tagCache.
    // Does NOT mark failed fetches as empty – they stay absent so a later
    // run can retry.
    // ------------------------------------------------------------------
    async function populateCache(ids) {
        const missing = ids.filter(id => !tagCache.has(id));
        if (!missing.length) return;

        console.log(LOG, 'populateCache: fetching', missing.length, 'IDs');

        let url;
        try {
            url = ApiClient.getUrl('Items', {
                Ids: missing.join(','),
                Fields: 'Tags',
                Limit: missing.length,
            });
        } catch (e) {
            console.error(LOG, 'ApiClient.getUrl failed:', e);
            return;
        }

        let result;
        try {
            result = await ApiClient.getJSON(url);
        } catch (e) {
            console.error(LOG, 'ApiClient.getJSON failed:', e);
            return;
        }

        console.log(LOG, 'populateCache: response Items count =', result?.Items?.length ?? 0);

        for (const item of result.Items ?? []) {
            const first = (item.Tags ?? []).find(t => t.startsWith('mb-type-')) ?? '';
            tagCache.set(item.Id, first);
            if (first) console.log(LOG, 'cached', item.Id, '->', first);
        }

        // Mark items that had no mb-type-* tag so we don't re-fetch them.
        for (const id of missing) {
            if (!tagCache.has(id)) tagCache.set(id, '');
        }
    }

    // ------------------------------------------------------------------
    // Main work: find the visible container, populate cache, apply classes.
    // ------------------------------------------------------------------
    async function tagCards() {
        const container = findContainer();
        if (!container) {
            console.log(LOG, 'tagCards: no visible itemsContainer in .detailPageContent – skipping');
            return;
        }

        const cards = [...container.querySelectorAll('[data-id]')];
        console.log(LOG, 'tagCards: found', cards.length, 'cards in visible container');
        if (!cards.length) return;

        const ids = [...new Set(cards.map(c => c.dataset.id).filter(Boolean))];
        await populateCache(ids);

        let applied = 0;
        for (const card of cards) {
            const type = tagCache.get(card.dataset.id);
            if (!type) continue;

            // Remove any stale mb-type-* class before adding the fresh one.
            for (const cls of [...card.classList]) {
                if (cls.startsWith('mb-type-')) card.classList.remove(cls);
            }
            card.classList.add(type);
            applied++;
        }

        if (applied) {
            console.log(LOG, 'tagCards: applied classes to', applied, 'cards; activating container');
            container.classList.add('mb-sections-active');
        }
    }

    function debounce(fn, ms) {
        let timer;
        return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
    }

    const debouncedTagCards = debounce(tagCards, 400);

    // ------------------------------------------------------------------
    // ApiClient readiness check (handles both isLoggedIn and accessToken APIs).
    // ------------------------------------------------------------------
    function clientReady() {
        if (typeof ApiClient === 'undefined') return false;
        if (typeof ApiClient.isLoggedIn === 'function') return ApiClient.isLoggedIn();
        if (typeof ApiClient.accessToken === 'function') return !!ApiClient.accessToken();
        return true;
    }

    let pollCount = 0;
    function init() {
        if (!clientReady()) {
            pollCount++;
            if (pollCount <= 5 || pollCount % 10 === 0) {
                console.log(LOG, 'init: waiting for ApiClient (attempt', pollCount + ')');
            }
            setTimeout(init, 500);
            return;
        }

        console.log(LOG, 'init: ApiClient ready, setting up observers');

        new MutationObserver(debouncedTagCards)
            .observe(document.body, { childList: true, subtree: true });

        window.addEventListener('hashchange', () => {
            console.log(LOG, 'hashchange ->', window.location.hash);
            debouncedTagCards();
        });

        tagCards();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        setTimeout(init, 0);
    }
})();
