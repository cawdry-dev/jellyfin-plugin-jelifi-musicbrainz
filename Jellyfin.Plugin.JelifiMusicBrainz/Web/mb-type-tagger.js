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

    const LOG = '[JelifiMB]';
    console.log(LOG, 'mb-type-tagger.js loaded');

    // Keyed by Jellyfin item ID; value is the array of mb-type-* class names.
    const tagCache = new Map();

    // -----------------------------------------------------------------
    // Returns true once ApiClient exists and has an access token.
    // Handles both ApiClient.isLoggedIn() (older) and ApiClient.accessToken()
    // (newer jellyfin-apiclient builds where isLoggedIn was removed).
    // -----------------------------------------------------------------
    function clientReady() {
        if (typeof ApiClient === 'undefined') return false;
        if (typeof ApiClient.isLoggedIn === 'function') return ApiClient.isLoggedIn();
        if (typeof ApiClient.accessToken === 'function') return !!ApiClient.accessToken();
        // Last resort: if the object exists assume it's ready
        return true;
    }

    // -----------------------------------------------------------------
    // Batch-fetch Tags for item IDs not yet in tagCache.
    // Does NOT silently cache failed IDs – they stay absent from the map
    // so a later run can retry them.
    // -----------------------------------------------------------------
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

        console.log(LOG, 'populateCache: GET', url);

        let result;
        try {
            result = await ApiClient.getJSON(url);
        } catch (e) {
            console.error(LOG, 'ApiClient.getJSON failed:', e);
            return;
        }

        console.log(LOG, 'populateCache: response Items count =', result?.Items?.length ?? 0);

        for (const item of result.Items ?? []) {
            const mbTags = (item.Tags ?? []).filter(t => t.startsWith('mb-type-'));
            tagCache.set(item.Id, mbTags);
            if (mbTags.length) {
                console.log(LOG, 'cached', item.Id, '->', mbTags);
            }
        }

        // Only mark truly-no-tag items as empty (don't mark fetch-failed ones)
        for (const id of missing) {
            if (!tagCache.has(id)) tagCache.set(id, []);
        }
    }

    // -----------------------------------------------------------------
    // Find all [data-id] elements, populate the cache, apply classes.
    // -----------------------------------------------------------------
    async function tagCards() {
        const cards = [...document.querySelectorAll('[data-id]')];
        console.log(LOG, 'tagCards: found', cards.length, '[data-id] elements');

        if (!cards.length) return;

        const ids = [...new Set(cards.map(c => c.dataset.id).filter(Boolean))];
        await populateCache(ids);

        let applied = 0;
        for (const card of cards) {
            const types = tagCache.get(card.dataset.id);
            if (!types?.length) continue;

            for (const cls of [...card.classList]) {
                if (cls.startsWith('mb-type-')) card.classList.remove(cls);
            }
            card.classList.add(...types);
            applied++;
        }

        if (applied) {
            console.log(LOG, 'tagCards: applied mb-type-* classes to', applied, 'cards');
        }
    }

    function debounce(fn, ms) {
        let timer;
        return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
    }

    const debouncedTagCards = debounce(tagCards, 400);

    // -----------------------------------------------------------------
    // Poll until ApiClient is ready, then wire up observers.
    // -----------------------------------------------------------------
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
