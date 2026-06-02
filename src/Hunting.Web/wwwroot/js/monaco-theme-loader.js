/*
 * DeltaZulu / Atom One Light Monaco theme integration.
 *
 * The source theme is stored as /js/monaco-themes/atom-one-light.json.
 * Monaco does not consume VS Code `tokenColors` directly, so this loader maps the
 * generic TextMate scopes used by the uploaded theme into Monaco token rules used
 * by the local KQL Monarch tokenizer.
 */
(function () {
    const themeName = 'atom-one-light';
    const themeUrl = '/js/monaco-themes/atom-one-light.json';
    let registered = false;
    let registerPromise = null;

    const readScopeColor = (source, scopeName, fallback) => {
        const rules = source?.tokenColors ?? [];
        const found = rules.find((rule) => {
            const scope = rule.scope;
            if (Array.isArray(scope)) return scope.includes(scopeName);
            return scope === scopeName;
        });

        return found?.settings?.foreground ?? fallback;
    };

    const readScopeStyle = (source, scopeName, fallback = '') => {
        const rules = source?.tokenColors ?? [];
        const found = rules.find((rule) => {
            const scope = rule.scope;
            if (Array.isArray(scope)) return scope.includes(scopeName);
            return scope === scopeName;
        });

        return found?.settings?.fontStyle ?? fallback;
    };

    const normalizeHex = (value) => {
        if (typeof value !== 'string') return value;
        return value.startsWith('#') ? value.slice(1) : value;
    };

    const toRule = (token, foreground, fontStyle) => ({
        token,
        foreground: normalizeHex(foreground),
        ...(fontStyle ? { fontStyle } : {})
    });

    const createMonacoTheme = (source) => {
        const colors = source?.colors ?? {};

        const foreground = colors['editor.foreground'] ?? '#383A42';
        const editorBackground = colors['editor.background'] ?? '#FAFAFA';
        const lineHighlight = colors['editor.lineHighlightBackground'] ?? '#383A420C';
        const selection = colors['editor.selectionBackground'] ?? '#E5E5E6';
        const cursor = colors['editorCursor.foreground'] ?? '#526FFF';
        const lineNumber = colors['editorLineNumber.foreground'] ?? '#9D9D9F';
        const activeLineNumber = colors['editorLineNumber.activeForeground'] ?? '#383A42';

        const comment = readScopeColor(source, 'comment', '#A0A1A7');
        const keyword = readScopeColor(source, 'keyword', '#A626A4');
        const operator = readScopeColor(source, 'keyword.operator', '#383A42');
        const string = readScopeColor(source, 'string', '#50A14F');
        const number = readScopeColor(source, 'constant.numeric', '#986801');
        const constant = readScopeColor(source, 'constant', '#986801');
        const functionColor = readScopeColor(source, 'entity.name.function', '#4078F2');
        const type = readScopeColor(source, 'entity.name.type', '#C18401');
        const variable = readScopeColor(source, 'variable', '#E45649');
        const commentStyle = readScopeStyle(source, 'comment', 'italic');

        return {
            base: 'vs',
            inherit: true,
            colors: {
                ...colors,
                'editor.background': editorBackground,
                'editor.foreground': foreground,
                'editor.lineHighlightBackground': lineHighlight,
                'editor.selectionBackground': selection,
                'editorCursor.foreground': cursor,
                'editorLineNumber.foreground': lineNumber,
                'editorLineNumber.activeForeground': activeLineNumber,
                'editorSuggestWidget.background': colors['editorSuggestWidget.background'] ?? '#EAEAEB',
                'editorSuggestWidget.border': colors['editorSuggestWidget.border'] ?? '#DBDBDC',
                'editorSuggestWidget.selectedBackground': colors['editorSuggestWidget.selectedBackground'] ?? '#FFFFFF',
                'editorHoverWidget.background': colors['editorHoverWidget.background'] ?? '#EAEAEB',
                'editorHoverWidget.border': colors['editorHoverWidget.border'] ?? '#DBDBDC',
                'focusBorder': colors.focusBorder ?? '#526FFF'
            },
            rules: [
                toRule('', foreground),
                toRule('identifier', foreground),
                toRule('comment', comment, commentStyle),
                toRule('keyword', keyword),
                toRule('operator', operator),
                toRule('delimiter', foreground),
                toRule('number', number),
                toRule('string', string),
                toRule('constant', constant),
                toRule('variable', variable),
                toRule('function', functionColor),
                toRule('type', type),
                toRule('class', type),
                toRule('field', variable),
                toRule('invalid', '#FF1414')
            ]
        };
    };

    const loadThemeSource = async () => {
        const response = await fetch(themeUrl, { cache: 'force-cache' });
        if (!response.ok) {
            throw new Error(`Could not load Monaco theme source: ${themeUrl}`);
        }

        return await response.json();
    };

    const registerTheme = async () => {
        if (registered) return;
        if (registerPromise) return registerPromise;

        registerPromise = (async () => {
            await window.ensureMonaco();
            const source = await loadThemeSource();
            monaco.editor.defineTheme(themeName, createMonacoTheme(source));
            registered = true;
        })();

        return registerPromise;
    };

    const applyTheme = async () => {
        await registerTheme();
        monaco.editor.setTheme(themeName);
    };

    const patchHuntingMonacoInit = () => {
        if (!window.huntingMonaco || window.huntingMonaco.__atomOneLightThemePatched) {
            return Boolean(window.huntingMonaco?.__atomOneLightThemePatched);
        }

        const originalInit = window.huntingMonaco.init;
        window.huntingMonaco.init = async function (...args) {
            await registerTheme();

            const originalCreate = monaco.editor.create;
            monaco.editor.create = function (container, options, ...rest) {
                return originalCreate.call(monaco.editor, container, {
                    ...(options ?? {}),
                    theme: themeName
                }, ...rest);
            };

            try {
                return await originalInit.apply(window.huntingMonaco, args);
            }
            finally {
                monaco.editor.create = originalCreate;
                monaco.editor.setTheme(themeName);
            }
        };

        window.huntingMonaco.__atomOneLightThemePatched = true;
        window.huntingMonaco.applyEditorTheme = applyTheme;
        return true;
    };

    window.huntingMonacoTheme = {
        name: themeName,
        register: registerTheme,
        apply: applyTheme
    };

    if (!patchHuntingMonacoInit()) {
        const patchTimer = window.setInterval(() => {
            if (patchHuntingMonacoInit()) {
                window.clearInterval(patchTimer);
            }
        }, 25);

        window.setTimeout(() => window.clearInterval(patchTimer), 5000);
    }
})();
