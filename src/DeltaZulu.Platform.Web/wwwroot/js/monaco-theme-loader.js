/*
 * DeltaZulu / Atom One Light Monaco theme integration.
 *
 * The source theme is stored next to this script under:
 *   ./monaco-themes/atom-one-light.json
 *
 * When loaded as a Blazor static web asset, the script usually lives under:
 *   /_content/DeltaZulu.Platform.Web/js/monaco-theme-loader.js
 *
 * Therefore the theme URL must be resolved relative to the current script,
 * not from the application root.
 */
(function () {
    'use strict';

    const customThemeName = 'atom-one-light';
    const fallbackThemeName = 'vs';
    const fallbackThemeUrl = '_content/DeltaZulu.Platform.Web/js/monaco-themes/atom-one-light.json';

    let activeThemeName = customThemeName;
    let registered = false;
    let registerPromise = null;
    let patchTimer = null;

    const isDebugEnabled = () =>
        Boolean(window.deltaZuluDebug || window.huntingMonacoDebug);

    const debug = (...args) => {
        if (isDebugEnabled()) {
            console.debug('[DeltaZulu Monaco Theme]', ...args);
        }
    };

    const warn = (...args) => {
        if (isDebugEnabled()) {
            console.warn('[DeltaZulu Monaco Theme]', ...args);
        }
    };

    const error = (...args) => {
        if (isDebugEnabled()) {
            console.error('[DeltaZulu Monaco Theme]', ...args);
        }
    };

    const resolveThemeUrl = () => {
        try {
            const scriptUrl = document.currentScript?.src;

            if (scriptUrl) {
                return new URL('./monaco-themes/atom-one-light.json', scriptUrl).toString();
            }

            return fallbackThemeUrl;
        }
        catch (exception) {
            warn('Could not resolve theme URL from current script. Falling back to static web asset path.', exception);
            return fallbackThemeUrl;
        }
    };

    const themeUrl = resolveThemeUrl();

    const getMonaco = () => {
        if (!window.monaco?.editor) {
            throw new Error('Monaco editor API is not available.');
        }

        return window.monaco;
    };

    const ensureMonacoAvailable = async () => {
        if (typeof window.ensureMonaco !== 'function') {
            throw new Error('window.ensureMonaco is not available.');
        }

        await window.ensureMonaco();
        return getMonaco();
    };

    const readScopeColor = (source, scopeName, fallback) => {
        const rules = Array.isArray(source?.tokenColors)
            ? source.tokenColors
            : [];

        const found = rules.find((rule) => {
            const scope = rule?.scope;

            if (Array.isArray(scope)) {
                return scope.includes(scopeName);
            }

            return scope === scopeName;
        });

        return found?.settings?.foreground ?? fallback;
    };

    const readScopeStyle = (source, scopeName, fallback = '') => {
        const rules = Array.isArray(source?.tokenColors)
            ? source.tokenColors
            : [];

        const found = rules.find((rule) => {
            const scope = rule?.scope;

            if (Array.isArray(scope)) {
                return scope.includes(scopeName);
            }

            return scope === scopeName;
        });

        return found?.settings?.fontStyle ?? fallback;
    };

    const normalizeHex = (value) => {
        if (typeof value !== 'string') {
            return value;
        }

        return value.startsWith('#')
            ? value.slice(1)
            : value;
    };

    const toRule = (token, foreground, fontStyle) => ({
        token,
        foreground: normalizeHex(foreground),
        ...(fontStyle ? { fontStyle } : {})
    });

    const createMonacoTheme = (source) => {
        const colors = source?.colors && typeof source.colors === 'object'
            ? source.colors
            : {};

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
            base: fallbackThemeName,
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
        debug('Loading Monaco theme source.', { themeUrl });

        let response;

        try {
            response = await fetch(themeUrl, {
                cache: 'force-cache',
                headers: {
                    'Accept': 'application/json'
                }
            });
        }
        catch (exception) {
            throw new Error(`Could not fetch Monaco theme source: ${themeUrl}`, {
                cause: exception
            });
        }

        if (!response.ok) {
            throw new Error(
                `Could not load Monaco theme source: ${themeUrl}. HTTP ${response.status} ${response.statusText}`.trim()
            );
        }

        try {
            const source = await response.json();

            if (!source || typeof source !== 'object') {
                throw new Error('Theme source was not a JSON object.');
            }

            return source;
        }
        catch (exception) {
            throw new Error(`Could not parse Monaco theme source JSON: ${themeUrl}`, {
                cause: exception
            });
        }
    };

    const registerTheme = async () => {
        if (registered) {
            return;
        }

        if (registerPromise) {
            return registerPromise;
        }

        registerPromise = (async () => {
            const monacoInstance = await ensureMonacoAvailable();

            try {
                const source = await loadThemeSource();
                monacoInstance.editor.defineTheme(customThemeName, createMonacoTheme(source));
                activeThemeName = customThemeName;
                debug('Registered custom Monaco theme.', { themeName: activeThemeName, themeUrl });
            }
            catch (exception) {
                activeThemeName = fallbackThemeName;
                warn('Custom Monaco theme could not be loaded. Falling back to built-in theme.', exception);
            }

            registered = true;
        })();

        try {
            return await registerPromise;
        }
        catch (exception) {
            registerPromise = null;
            registered = false;
            error('Monaco theme registration failed before fallback could be applied.', exception);
            throw exception;
        }
    };

    const applyTheme = async () => {
        await registerTheme();

        try {
            getMonaco().editor.setTheme(activeThemeName);
            debug('Applied Monaco theme.', { themeName: activeThemeName });
        }
        catch (exception) {
            warn('Could not apply Monaco theme.', exception);
        }
    };

    const patchHuntingMonacoInit = () => {
        if (!window.huntingMonaco) {
            return false;
        }

        if (window.huntingMonaco.__atomOneLightThemePatched) {
            return true;
        }

        if (typeof window.huntingMonaco.init !== 'function') {
            warn('window.huntingMonaco exists, but init is not a function.');
            return false;
        }

        const originalInit = window.huntingMonaco.init;

        window.huntingMonaco.init = async function (...args) {
            await registerTheme();

            const monacoInstance = getMonaco();
            const originalCreate = monacoInstance.editor.create;

            monacoInstance.editor.create = function (container, options, ...rest) {
                return originalCreate.call(monacoInstance.editor, container, {
                    ...(options ?? {}),
                    theme: activeThemeName
                }, ...rest);
            };

            try {
                debug('Initializing KQL Monaco editor with theme wrapper.', { themeName: activeThemeName });
                return await originalInit.apply(window.huntingMonaco, args);
            }
            finally {
                monacoInstance.editor.create = originalCreate;

                try {
                    monacoInstance.editor.setTheme(activeThemeName);
                }
                catch (exception) {
                    warn('Could not re-apply Monaco theme after editor initialization.', exception);
                }
            }
        };

        window.huntingMonaco.__atomOneLightThemePatched = true;
        window.huntingMonaco.applyEditorTheme = applyTheme;

        debug('Patched huntingMonaco.init for theme application.');
        return true;
    };

    window.huntingMonacoTheme = {
        get name() {
            return activeThemeName;
        },
        get sourceUrl() {
            return themeUrl;
        },
        register: registerTheme,
        apply: applyTheme
    };

    if (!patchHuntingMonacoInit()) {
        patchTimer = window.setInterval(() => {
            if (patchHuntingMonacoInit()) {
                window.clearInterval(patchTimer);
                patchTimer = null;
            }
        }, 25);

        window.setTimeout(() => {
            if (patchTimer) {
                window.clearInterval(patchTimer);
                patchTimer = null;
                warn('Timed out while waiting to patch huntingMonaco.init.');
            }
        }, 5000);
    }
})();
