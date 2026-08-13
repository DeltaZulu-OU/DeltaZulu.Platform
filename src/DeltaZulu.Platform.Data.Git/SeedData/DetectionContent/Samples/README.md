# Sample detection content seed manifest

This folder contains sample detection content adapted from the uploaded `Sentinel-Queries-main` extract.
The original queries were filtered for seed suitability and then rewritten against DeltaZulu golden
analytical views rather than copied as Microsoft Sentinel table-specific content.

Selection rules:

1. Prefer simple KQL pipelines using `where`, `project`, `summarize`, `sort`, and `take` style operators.
2. Exclude queries requiring joins, unions, `mv-*`, `parse`, `externaldata`, watchlists, or complex dynamic JSON parsing.
3. Prefer endpoint, Windows security, DNS, authentication, process, and network examples that map naturally to current DeltaZulu analytical contracts.
4. Keep metadata complete enough for Governance and future Operations projection: severity, confidence, risk score, schedule, lookback, materialization, required tables, MITRE hints, and entity mappings.

These detections are seed/demo content. They are intentionally not production-ready rules.
