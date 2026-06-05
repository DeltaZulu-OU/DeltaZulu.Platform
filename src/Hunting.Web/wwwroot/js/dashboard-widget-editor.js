window.huntingDashboardWidgetEditor = (() => {
    const editors = new Map();

    function getMonaco() {
        if (window.monaco && window.monaco.editor) {
            return window.monaco;
        }

        return null;
    }

    function initialize(elementId, value) {
        const monaco = getMonaco();
        const element = document.getElementById(elementId);

        if (!monaco || !element) {
            return false;
        }

        dispose(elementId);

        const editor = monaco.editor.create(element, {
            value: value || "",
            language: "kql",
            theme: "deltazulu-dark",
            automaticLayout: true,
            minimap: { enabled: false },
            scrollBeyondLastLine: false,
            wordWrap: "on",
            fontSize: 13,
            lineNumbers: "on",
            renderLineHighlight: "line",
            tabSize: 4
        });

        editors.set(elementId, editor);

        window.setTimeout(() => editor.layout(), 0);
        window.setTimeout(() => editor.focus(), 50);

        return true;
    }

    function getValue(elementId) {
        const editor = editors.get(elementId);
        return editor ? editor.getValue() : "";
    }

    function setValue(elementId, value) {
        const editor = editors.get(elementId);
        if (editor) {
            editor.setValue(value || "");
        }
    }

    function layout(elementId) {
        const editor = editors.get(elementId);
        if (editor) {
            editor.layout();
        }
    }

    function dispose(elementId) {
        const editor = editors.get(elementId);
        if (editor) {
            editor.dispose();
            editors.delete(elementId);
        }
    }

    return {
        initialize,
        getValue,
        setValue,
        layout,
        dispose
    };
})();
