// Native modal dialogs provide keyboard focus containment and inert background content.
const previousFocus = new WeakMap();
function dialogsIn(node) {
    if (!(node instanceof Element)) return [];
    return [ ...(node.matches('dialog[data-school-modal]') ? [node] : []),
        ...node.querySelectorAll('dialog[data-school-modal]') ];
}
function open(dialog) {
    if (dialog.open || !dialog.isConnected) return;
    previousFocus.set(dialog, document.activeElement);
    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        dialog.click(); // Let the component own closing, including its saving guard.
    });
    dialog.showModal();
}
new MutationObserver(records => {
    for (const record of records) {
        for (const node of record.removedNodes) {
            for (const dialog of dialogsIn(node)) {
                dialog.close();
                const previous = previousFocus.get(dialog);
                if (previous?.isConnected) previous.focus();
            }
        }
        for (const node of record.addedNodes) dialogsIn(node).forEach(open);
    }
}).observe(document.body, { childList: true, subtree: true });
document.querySelectorAll('dialog[data-school-modal]').forEach(open);
