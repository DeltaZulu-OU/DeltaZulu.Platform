window.ensureMonaco = () => {
    if (window.monaco) return Promise.resolve();

    return new Promise((resolve, reject) => {
        const ensureRequire = (onReady) => {
            if (window.require) {
                onReady();
                return;
            }

            const script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js';
            script.onload = onReady;
            script.onerror = () => reject(new Error('Could not load require.js from CDN.'));
            document.head.appendChild(script);
        };

        const monacoBaseUrl = 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.2/min';

        ensureRequire(() => {
            window.MonacoEnvironment = {
                getWorkerUrl: () => `data:text/javascript;charset=utf-8,${encodeURIComponent(`
                    self.MonacoEnvironment = { baseUrl: '${monacoBaseUrl}' };
                    importScripts('${monacoBaseUrl}/vs/base/worker/workerMain.js');
                `)}`
            };

            window.require.config({ paths: { vs: `${monacoBaseUrl}/vs` } });
            window.require(['vs/editor/editor.main'], () => resolve(), reject);
        });
    });
};

const registerKqlLanguage = async () => {
    await window.ensureMonaco();
    if (monaco.languages.getLanguages().some((m) => m.id === 'kql')) return;

    monaco.languages.register({ id: 'kql' });

    const language = window.huntingMonaco?._schema?.language ?? {};
    const defaultKeywords = [
        'where', 'project', 'take', 'limit', 'extend', 'summarize',
        'by', 'join', 'on', 'kind', 'lookup', 'sort', 'order', 'asc', 'desc',
        'count', 'distinct', 'top', 'print', 'sample', 'sample-distinct', 'render'
    ];
    const defaultOperators = [
        '==', '!=', '=~', '!~', '>', '<', '>=', '<=',
        'between', '!between', 'in', '!in', 'and', 'or', 'not',
        'contains', '!contains', 'contains_cs', '!contains_cs',
        'startswith', '!startswith', 'startswith_cs', '!startswith_cs',
        'endswith', '!endswith', 'endswith_cs', '!endswith_cs',
        'has', '!has', 'has_cs', '!has_cs',
        'hasprefix', '!hasprefix', 'hasprefix_cs', '!hasprefix_cs',
        'hassuffix', '!hassuffix', 'hassuffix_cs', '!hassuffix_cs'
    ];
    const defaultRenderKinds = ['table', 'timechart', 'linechart', 'barchart', 'columnchart', 'piechart', 'areachart', 'scatterchart'];
    const keywords = language.keywords ?? [];
    const operators = language.operators ?? [];
    const renderKinds = language.renderKinds ?? [];
    const effectiveKeywords = keywords.length ? keywords : defaultKeywords;
    const effectiveOperators = operators.length ? operators : defaultOperators;
    const effectiveRenderKinds = renderKinds.length ? renderKinds : defaultRenderKinds;
    const hyphenatedKeywords = effectiveKeywords.filter((keyword) => keyword.includes('-'));
    const hyphenatedKeywordPattern = hyphenatedKeywords.length > 0
        ? new RegExp(`(?:${hyphenatedKeywords.map((keyword) => keyword.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')})(?![\\w-])`)
        : /(?!)/;

    monaco.languages.setMonarchTokensProvider('kql', {
        keywords: effectiveKeywords,
        operators: effectiveOperators,
        tokenizer: {
            root: [
                [hyphenatedKeywordPattern, 'keyword'],
                [/[a-zA-Z_]\w*/, {
                    cases: {
                        '@keywords': 'keyword',
                        '@operators': 'operator',
                        '@default': 'identifier'
                    }
                }],
                [/[()\[\]{}]/, 'delimiter'],
                [/"([^"\\]|\\.)*"/, 'string'],
                [/'([^'\\]|\\.)*'/, 'string'],
                [/\/\/.*$/, 'comment'],
                [/\b\d+(\.\d+)?\b/, 'number'],
                [/[|=><!~]+/, 'operator']
            ]
        }
    });

    monaco.languages.setLanguageConfiguration('kql', {
        comments: { lineComment: '//' },
        brackets: [['(', ')'], ['[', ']'], ['{', '}']],
        autoClosingPairs: [
            { open: '[', close: ']' },
            { open: '{', close: '}' },
            { open: '(', close: ')' },
            { open: '"', close: '"' },
            { open: '\'', close: '\'' }
        ]
    });

    monaco.languages.registerCompletionItemProvider('kql', {
        triggerCharacters: ['|', ' ', '.', '-'],
        provideCompletionItems: (model, position) => {
            const word = model.getWordUntilPosition(position);
            const linePrefix = model.getLineContent(position.lineNumber).slice(0, position.column - 1);
            const completionToken = linePrefix.match(/!?[A-Za-z_][\w-]*$/)?.[0] ?? '';
            const afterRender = /\|\s*render\s+[A-Za-z0-9_-]*$/i.test(linePrefix);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: completionToken.length > 0
                    ? position.column - completionToken.length
                    : word.startColumn,
                endColumn: word.endColumn
            };

            const keywordSuggestions = effectiveKeywords.map((keyword) => ({
                label: keyword,
                kind: monaco.languages.CompletionItemKind.Keyword,
                insertText: keyword,
                range
            }));

            const operatorSuggestions = effectiveOperators.map((operator) => ({
                label: operator,
                kind: monaco.languages.CompletionItemKind.Operator,
                insertText: operator,
                range
            }));

            const snippetSuggestions = [
                {
                    label: 'where contains',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: 'where ${1:Column} contains "${2:value}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'summarize count by',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: 'summarize count() by ${1:Column}',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                }
            ];

            const tableSuggestions = (window.huntingMonaco?._schema?.tables ?? []).map((table) => ({
                label: table.name,
                kind: monaco.languages.CompletionItemKind.Class,
                insertText: table.name,
                detail: table.description ? `Table · ${table.description}` : 'Table',
                range
            }));

            const columnSuggestions = (window.huntingMonaco?._schema?.tables ?? [])
                .flatMap((table) => (table.columns ?? []).map((column) => ({
                    label: column.name,
                    kind: monaco.languages.CompletionItemKind.Field,
                    insertText: column.name,
                    detail: `Column · ${table.name} · ${column.type}${column.nullable ? ' · nullable' : ''}${column.dynamic ? ' · dynamic' : ''}`,
                    documentation: column.description,
                    range
                })));

            const renderKindSuggestions = effectiveRenderKinds.map((kind) => ({
                label: kind,
                kind: monaco.languages.CompletionItemKind.EnumMember,
                insertText: afterRender ? kind : `render ${kind}`,
                detail: 'Render kind',
                range
            }));

            const renderSnippetSuggestions = [
                {
                    label: 'linechart template',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: afterRender
                        ? 'linechart xcolumn=${1:Timestamp} ycolumns=${2:Count} title="${3:Line chart}"'
                        : 'render linechart xcolumn=${1:Timestamp} ycolumns=${2:Count} title="${3:Line chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'barchart template',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: afterRender
                        ? 'barchart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Bar chart}"'
                        : 'render barchart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Bar chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'piechart template',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: afterRender
                        ? 'piechart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Pie chart}"'
                        : 'render piechart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Pie chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'areachart template',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: afterRender
                        ? 'areachart xcolumn=${1:Timestamp} ycolumns=${2:Count} title="${3:Area chart}"'
                        : 'render areachart xcolumn=${1:Timestamp} ycolumns=${2:Count} title="${3:Area chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'scatterchart template',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: afterRender
                        ? 'scatterchart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Scatter chart}"'
                        : 'render scatterchart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Scatter chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                }
            ];

            return {
                suggestions: [
                    ...keywordSuggestions,
                    ...operatorSuggestions,
                    ...snippetSuggestions,
                    ...renderSnippetSuggestions,
                    ...tableSuggestions,
                    ...columnSuggestions,
                    ...renderKindSuggestions
                ]
            };
        }
    });
};

const markerSeverity = (severity) => {
    const normalized = String(severity ?? '').toLowerCase();
    if (normalized === 'warning') return monaco.MarkerSeverity.Warning;
    if (normalized === 'info' || normalized === 'information') return monaco.MarkerSeverity.Info;
    return monaco.MarkerSeverity.Error;
};

const diagnosticValue = (diagnostic, camelName, pascalName) => diagnostic?.[camelName] ?? diagnostic?.[pascalName];

const diagnosticToMarker = (model, diagnostic) => {
    const modelLength = model.getValueLength();
    const rawStart = diagnosticValue(diagnostic, 'textStart', 'TextStart');
    const rawLength = diagnosticValue(diagnostic, 'textLength', 'TextLength');
    const startOffset = Math.min(Math.max(Number.isInteger(rawStart) ? rawStart : 0, 0), modelLength);
    const length = Math.max(Number.isInteger(rawLength) ? rawLength : 1, 1);
    const endOffset = Math.min(Math.max(startOffset + length, startOffset + 1), modelLength);
    const start = model.getPositionAt(startOffset);
    const end = model.getPositionAt(endOffset > startOffset ? endOffset : startOffset);

    return {
        severity: markerSeverity(diagnosticValue(diagnostic, 'severity', 'Severity')),
        message: diagnosticValue(diagnostic, 'message', 'Message') ?? 'Invalid KQL.',
        startLineNumber: start.lineNumber,
        startColumn: start.column,
        endLineNumber: end.lineNumber,
        endColumn: endOffset > startOffset ? end.column : start.column + 1
    };
};

const scheduleValidation = (editor, dotNetRef) => {
    let validationTimer = null;
    let validationVersion = 0;

    const validate = () => {
        clearTimeout(validationTimer);
        const version = ++validationVersion;
        validationTimer = setTimeout(async () => {
            const model = editor.getModel();
            if (!model) return;

            const queryText = model.getValue();
            if (!queryText.trim()) {
                monaco.editor.setModelMarkers(model, 'kql-validation', []);
                return;
            }

            try {
                const diagnostics = await dotNetRef.invokeMethodAsync('ValidateFromEditor', queryText);
                if (version !== validationVersion) return;

                const markers = Array.isArray(diagnostics)
                    ? diagnostics.map((diagnostic) => diagnosticToMarker(model, diagnostic))
                    : [];
                monaco.editor.setModelMarkers(model, 'kql-validation', markers);
            } catch {
                if (version === validationVersion) {
                    monaco.editor.setModelMarkers(model, 'kql-validation', []);
                }
            }
        }, 400);
    };

    editor.onDidChangeModelContent(validate);
    validate();
};

window.huntingMonaco = {
    _editors: {},
    _schema: { tables: [], language: { keywords: [], operators: [], renderKinds: [] } },
    registerKqlLanguage,
    isReady: () => typeof window.monaco !== 'undefined',
    setSchema: (schema) => {
        window.huntingMonaco._schema = Array.isArray(schema?.tables) && schema?.language
            ? schema
            : { tables: [], language: { keywords: [], operators: [], renderKinds: [] } };
    },
    init: async (dotNetRef, containerId, initialValue) => {
        await window.ensureMonaco();
        const container = document.getElementById(containerId);
        if (!container) return;

        let preservedValue = '';
        if (window.huntingMonaco._editors[containerId]) {
            preservedValue = window.huntingMonaco._editors[containerId].getValue?.() ?? '';
            window.huntingMonaco._editors[containerId].dispose();
            delete window.huntingMonaco._editors[containerId];
        }

        const editor = monaco.editor.create(container, {
            language: 'kql',
            theme: 'vs-dark',
            value: preservedValue || initialValue || '',
            automaticLayout: true,
            minimap: { enabled: false },
            scrollBeyondLastLine: false,
            fontFamily: "'JetBrains Mono', 'Cascadia Code', 'Fira Code', monospace",
            fontSize: 13,
            lineNumbers: 'on',
            quickSuggestions: true,
            suggestOnTriggerCharacters: true,
            contextmenu: false
        });

        editor.onKeyDown((event) => {
            if (event.shiftKey && event.keyCode === monaco.KeyCode.Enter) {
                event.preventDefault();
                event.stopPropagation();

                dotNetRef.invokeMethodAsync('RunFromEditor');
            }
        });

        scheduleValidation(editor, dotNetRef);

        window.huntingMonaco._editors[containerId] = editor;

        editor.onContextMenu((e) => {
            e.event.preventDefault();
        });
    },
    getValue: (containerId) => window.huntingMonaco._editors[containerId]?.getValue() ?? '',
    setValue: (containerId, value) => window.huntingMonaco._editors[containerId]?.setValue(value ?? ''),
    layout: (containerId) => {
        const editor = window.huntingMonaco._editors[containerId];
        if (!editor) return;
        editor.layout();
    },
    dispose: (containerId) => {
        const editor = window.huntingMonaco._editors[containerId];
        if (!editor) return;
        const model = editor.getModel();
        if (model) {
            monaco.editor.setModelMarkers(model, 'kql-validation', []);
        }
        editor.dispose();
        delete window.huntingMonaco._editors[containerId];
    }
};

window.registerKqlLanguage = registerKqlLanguage;
