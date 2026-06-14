# Sentinel query sample selection for DeltaZulu seed detection content

## Scope

The uploaded `Sentinel-Queries-main.zip` archive contained 462 `.kql` files. I filtered them for sample-seed suitability rather than importing them wholesale. The aim is not Sentinel compatibility. The aim is a small set of understandable detection-content examples that exercise DeltaZulu Governance today and can later project into executable Operations detections.

## Filtering result

| Step | Result |
|---|---:|
| KQL files inspected | 462 |
| Simple pipeline candidates found | 83 |
| Seed detections created | 10 |

## Selection criteria

Queries were preferred when the original sample already had enough comments to infer purpose and data source, used a short pipeline, and mapped naturally to DeltaZulu golden analytical views such as `ProcessEvent`, `NetworkSession`, `Authentication`, `UserManagement`, and `Dns`. Queries were rejected when they depended on complex Sentinel-specific constructs such as joins, unions, watchlists, `externaldata`, `mv-expand`, `parse`, advanced dynamic JSON parsing, `make_set_if`, or source-specific schema fields that would distort the seed content.

## Created samples

| Slug | Required table | Severity | Confidence | Original source |
|---|---|---:|---:|---|
| `powershell-execution-policy-change` | `ProcessEvent` | Medium | High | `Defender for Endpoint/Device-PowerShellExecutionModeChanged.kql` |
| `powershell-public-network-connection` | `NetworkSession` | Medium | Medium | `Defender for Endpoint/Device-PowershellConnectingtoInternet.kql` |
| `public-ssh-egress` | `NetworkSession` | Low | Medium | `Defender for Endpoint/Device-PublicPort22Allowed.kql` |
| `inbound-ldap-ldaps-exposure` | `NetworkSession` | Medium | Medium | `Defender for Endpoint/Device-SummarizeLDAPandLDAPStraffic.kql` |
| `rdp-interactive-logon` | `Authentication` | Low | High | `Active Directory/SecurityEvent-SummarizeRDPActivity.kql` |
| `password-not-required-flag-set` | `UserManagement` | High | High | `Active Directory/SecurityEvent-AccountSetPasswordNotRequired.kql` |
| `kerberos-preauth-disabled` | `UserManagement` | High | Medium | `Active Directory/SecurityEvent-AccountPreAuthChanges.kql` |
| `new-low-port-listener` | `NetworkSession` | Medium | Medium | `Defender for Endpoint/Device-InterestingPortsOpened.kql` |
| `suspicious-command-line-download` | `ProcessEvent` | Medium | Medium | `Derived from Defender endpoint command-line hunting patterns in sample extract` |
| `dns-high-query-volume-per-name` | `Dns` | Low | Medium | `Derived from DNS anomaly patterns in sample extract` |

## Design notes

The generated KQL is intentionally rewritten rather than copied. Microsoft Sentinel table names such as `DeviceNetworkEvents`, `DeviceEvents`, and `SecurityEvent` were converted into DeltaZulu golden analytical contracts. This keeps the content aligned with the platform rule that analysts and detections should depend on governed analytical surfaces, not raw source tables.

The YAML metadata is deliberately fuller than the current minimum. Each rule includes severity, confidence, risk score, source provenance, schedule, lookback, materialization mode, required tables, tactics, techniques, entity mappings, false-positive notes, and validation notes. That makes the sample useful for Governance now and prevents a second migration when executable detection projection and alert materialization mature.

## Deferred imports

The remaining candidate queries should not be imported until the translator and schema contracts support the required constructs. The largest blockers are joins, dynamic-field parsing, `make_set`/`make_set_if`, conditional aggregate functions, watchlists, and source-specific nested JSON fields. Those are good future test cases, but poor seed content today.
