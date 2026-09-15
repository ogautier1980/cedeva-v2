// Table rows carrying a data-href behave like a link to that page, so a list can be browsed by
// clicking anywhere on a row. Clicks landing on something interactive (action buttons, links,
// checkboxes, inline forms) keep their own behaviour, and so does selecting text.
document.addEventListener('click', function (event) {
    var row = event.target.closest('tr[data-href]');
    if (!row) {
        return;
    }

    if (event.target.closest('a, button, input, select, textarea, label, .no-row-link')) {
        return;
    }

    if (window.getSelection && window.getSelection().toString()) {
        return;
    }

    // A row without a destination (e.g. a read-only entry in a mixed list) stays inert.
    var href = row.getAttribute('data-href');
    if (!href) {
        return;
    }

    if (event.ctrlKey || event.metaKey) {
        window.open(href, '_blank');
    } else {
        window.location.href = href;
    }
});
