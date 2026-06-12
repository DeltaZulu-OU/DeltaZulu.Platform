window.huntingKqlEditor = window.huntingKqlEditor || {};

window.huntingKqlEditor.insertTextAtCursor = (containerId, text) => {
    const editor = window.huntingMonaco?._editors?.[containerId];
    if (!editor) return false;

    const model = editor.getModel?.();
    if (!model) return false;

    const selection = editor.getSelection?.();
    const position = editor.getPosition?.();
    const range = selection ?? new monaco.Range(
        position?.lineNumber ?? model.getLineCount(),
        position?.column ?? model.getLineMaxColumn(model.getLineCount()),
        position?.lineNumber ?? model.getLineCount(),
        position?.column ?? model.getLineMaxColumn(model.getLineCount()));

    editor.executeEdits('hunting-query-helper', [{
        range,
        text: text ?? '',
        forceMoveMarkers: true
    }]);

    editor.focus();
    return true;
};