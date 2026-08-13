window.huntingRenderWizard = window.huntingRenderWizard || {};

window.huntingRenderWizard.initPreview = async (containerId, value) => {
    if (typeof window.ensureMonaco === "function") {
        await window.ensureMonaco();
    }

    if (!window.monaco?.editor) {
        return false;
    }

    const element = document.getElementById(containerId);
    if (!element) {
        return false;
    }

    const existing = window.huntingRenderWizard._editors?.[containerId];
    if (existing) {
        existing.dispose();
    }

    window.huntingRenderWizard._editors = window.huntingRenderWizard._editors || {};
    const editor = window.monaco.editor.create(element, {
        value: value ?? '',
        language: 'kql',
        readOnly: true,
        minimap: { enabled: false },
        lineNumbers: 'off',
        scrollBeyondLastLine: false,
        wordWrap: 'on',
        automaticLayout: true,
        theme: window.huntingMonacoTheme?.activeThemeName ?? 'vs'
    });

    window.huntingRenderWizard._editors[containerId] = editor;
    return true;
};

window.huntingRenderWizard.setPreview = (containerId, value) => {
    const editor = window.huntingRenderWizard._editors?.[containerId];
    if (!editor) return false;
    editor.setValue(value ?? '');
    return true;
};

window.huntingRenderWizard.disposePreview = (containerId) => {
    const editor = window.huntingRenderWizard._editors?.[containerId];
    if (!editor) return;
    editor.dispose();
    delete window.huntingRenderWizard._editors[containerId];
};
