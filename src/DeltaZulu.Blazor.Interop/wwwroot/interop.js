// ES module loaded on-demand by DeltaZulu.Blazor.Interop services via import().

export function downloadFile(fileName, content, mimeType) {
    const blob = new Blob([content ?? ""], { type: mimeType ?? "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName ?? "download";
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

export function pickFile(accept) {
    return new Promise((resolve, reject) => {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = accept ?? "";
        input.style.display = "none";
        input.addEventListener("change", () => {
            const file = input.files && input.files.length > 0 ? input.files[0] : null;
            input.remove();
            if (!file) { resolve(null); return; }
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result ?? "");
            reader.onerror = () => reject(reader.error ?? new Error("Could not read file."));
            reader.readAsText(file);
        }, { once: true });
        document.body.appendChild(input);
        input.click();
    });
}

export function getBoundingClientRect(element) {
    const r = element.getBoundingClientRect();
    return { width: r.width, height: r.height, top: r.top, left: r.left, bottom: r.bottom, right: r.right };
}
