// Monaco editor integration placeholder.
// For MVP, the plain <textarea> is used.
// When monaco-kusto is ready, replace the textarea with a Monaco instance:
//
// window.huntingMonaco = {
//     init: function(dotNetRef, containerId) {
//         require.config({ paths: { 'vs': '/_content/monaco-editor/min/vs' } });
//         require(['vs/editor/editor.main'], function() {
//             const editor = monaco.editor.create(
//                 document.getElementById(containerId), {
//                     language: 'kusto',
//                     theme: 'vs-dark',
//                     value: '',
//                     automaticLayout: true,
//                     minimap: { enabled: false },
//                     scrollBeyondLastLine: false,
//                     fontFamily: "'JetBrains Mono', 'Cascadia Code', 'Fira Code', monospace",
//                     fontSize: 13,
//                     lineNumbers: 'on',
//                 });
//
//             editor.addCommand(
//                 monaco.KeyMod.Shift | monaco.KeyCode.Enter,
//                 () => dotNetRef.invokeMethodAsync('RunFromEditor'));
//
//             window._huntingEditor = editor;
//         });
//     },
//     getValue: function() { return window._huntingEditor?.getValue() ?? ''; },
//     setValue: function(v) { window._huntingEditor?.setValue(v); }
// };
