window.huntingDashboardTransfer = (() => {
    function downloadJson(fileName, content) {
        const blob = new Blob([content || ""], { type: "application/json;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");

        anchor.href = url;
        anchor.download = fileName || "dashboard.json";
        anchor.style.display = "none";

        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();

        window.setTimeout(() => URL.revokeObjectURL(url), 0);
    }

    function pickJson() {
        return new Promise((resolve, reject) => {
            const input = document.createElement("input");
            input.type = "file";
            input.accept = "application/json,.json";
            input.style.display = "none";

            input.addEventListener("change", () => {
                const file = input.files && input.files.length > 0
                    ? input.files[0]
                    : null;

                input.remove();

                if (!file) {
                    resolve(null);
                    return;
                }

                const reader = new FileReader();
                reader.onload = () => resolve(reader.result || "");
                reader.onerror = () => reject(reader.error || new Error("Could not read dashboard JSON."));
                reader.readAsText(file);
            }, { once: true });

            document.body.appendChild(input);
            input.click();
        });
    }

    return {
        downloadJson,
        pickJson
    };
})();