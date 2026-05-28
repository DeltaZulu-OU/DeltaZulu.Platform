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

    const keywords = [
        'where', 'project', 'take', 'limit', 'extend', 'summarize',
        'by', 'join', 'on', 'kind', 'lookup', 'sort', 'order', 'asc', 'desc',
        'count', 'distinct', 'render', 'fork', 'union', 'mv-expand'
    ];

    const renderKinds = [
        'table', 'timechart', 'linechart', 'barchart', 'columnchart', 'piechart', 'areachart', 'scatterchart'
    ];
    const operators = [
        '==', '!=', '=~', '!~', '>', '<', '>=', '<=', 'contains', 'contains_cs',
        '!contains', 'startswith', 'endswith', 'has', 'has_cs', 'and', 'or'
    ];

    monaco.languages.setMonarchTokensProvider('kql', {
        keywords,
        operators,
        tokenizer: {
            root: [
                [/[a-zA-Z_]\w*/, {
                    cases: {
                        '@keywords': 'keyword',
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
        triggerCharacters: ['|', ' ', '.'],
        provideCompletionItems: (model, position) => {
            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn
            };

            const keywordSuggestions = keywords.map((keyword) => ({
                label: keyword,
                kind: monaco.languages.CompletionItemKind.Keyword,
                insertText: keyword,
                range
            }));

            const operatorSuggestions = operators.map((operator) => ({
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

            const tableSuggestions = (window.huntingMonaco?._schema ?? []).map((table) => ({
                label: table.name,
                kind: monaco.languages.CompletionItemKind.Class,
                insertText: table.name,
                detail: 'Table',
                range
            }));

            const columnSuggestions = (window.huntingMonaco?._schema ?? [])
                .flatMap((table) => (table.columns ?? []).map((column) => ({
                    label: column,
                    kind: monaco.languages.CompletionItemKind.Field,
                    insertText: column,
                    detail: `Column · ${table.name}`,
                    range
                })));

            const renderKindSuggestions = renderKinds.map((kind) => ({
                label: `render ${kind}`,
                kind: monaco.languages.CompletionItemKind.EnumMember,
                insertText: `render ${kind}`,
                detail: 'Render kind',
                range
            }));

            const renderSnippetSuggestions = [
                {
                    label: 'render linechart',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: 'render linechart xcolumn=${1:Timestamp} ycolumns=${2:Count} title="${3:Line chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'render barchart',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: 'render barchart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Bar chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'render piechart',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: 'render piechart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Pie chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'render areachart',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: 'render areachart xcolumn=${1:Timestamp} ycolumns=${2:Count} title="${3:Area chart}"',
                    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                    range
                },
                {
                    label: 'render scatterchart',
                    kind: monaco.languages.CompletionItemKind.Snippet,
                    insertText: 'render scatterchart xcolumn=${1:Category} ycolumns=${2:Count} title="${3:Scatter chart}"',
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

window.huntingMonaco = {
    _editors: {},
    _schema: [],
    registerKqlLanguage,
    isReady: () => typeof window.monaco !== 'undefined',
    setSchema: (schema) => {
        window.huntingMonaco._schema = Array.isArray(schema) ? schema : [];
    },
    init: async (dotNetRef, containerId, initialValue) => {
        await window.ensureMonaco();
        const container = document.getElementById(containerId);
        if (!container) return;

        if (window.huntingMonaco._editors[containerId]) {
            window.huntingMonaco._editors[containerId].dispose();
            delete window.huntingMonaco._editors[containerId];
        }

        const editor = monaco.editor.create(container, {
            language: 'kql',
            theme: 'vs-dark',
            value: initialValue ?? '',
            automaticLayout: true,
            minimap: { enabled: false },
            scrollBeyondLastLine: false,
            fontFamily: "'JetBrains Mono', 'Cascadia Code', 'Fira Code', monospace",
            fontSize: 13,
            lineNumbers: 'on',
            quickSuggestions: true,
            suggestOnTriggerCharacters: true
        });

        editor.addCommand(
            monaco.KeyMod.Shift | monaco.KeyCode.Enter,
            () => dotNetRef.invokeMethodAsync('RunFromEditor')
        );

        window.huntingMonaco._editors[containerId] = editor;
    },
    getValue: (containerId) => window.huntingMonaco._editors[containerId]?.getValue() ?? '',
    setValue: (containerId, value) => window.huntingMonaco._editors[containerId]?.setValue(value ?? ''),
    dispose: (containerId) => {
        const editor = window.huntingMonaco._editors[containerId];
        if (!editor) return;
        editor.dispose();
        delete window.huntingMonaco._editors[containerId];
    }
};

window.registerKqlLanguage = registerKqlLanguage;
