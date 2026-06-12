# DeltaZulu Platform product identity

DeltaZulu Platform is the product name for this repository and the user-facing application shell.
The platform may contain DZNS-authored content or be deployed for internal DeltaZulu workflows, but
those are content or deployment contexts rather than alternate product identities.

## Binding UI rules

- Use **DeltaZulu Platform** for the application name, shell title, page hero copy, module cards, and
  product documentation that describes the runnable application.
- Use **Analytics**, **Detection Content Governance**, and **Operations** as the three module names.
- Use **DZNS** only when referring to company/source identity, authored packages, or marketing/company
  surfaces. Do not use DZNS as the application name inside the product shell.
- Use **internal DeltaZulu platform** only when documenting a deployment context; do not expose it as a
  competing brand in navigation, calls to action, or page titles.
- Product UI headings use IBM Plex Sans. Newsreader is reserved for marketing/company display
  surfaces and must not be attached to global `h1` product styles.
- Orange is an action/CTA color. Structural navigation, emphasis, cards, and table chrome use ink,
  paper, slate, borders, and status colors rather than orange-as-primary styling.
- Structural product surfaces are sharp. Action controls use pill treatment, and explicitly opted-in
  inputs or code affordances may use the tiny input radius.

## Module language

The platform shell should present the product as a full-cycle security analytics loop:

1. **Analytics** asks questions, explores approved schemas, preserves analytical artifacts, and builds
   reusable logic.
2. **Detection Content Governance** turns detection changes into reviewed, validated, accepted content.
3. **Operations** executes accepted detections, records runs, creates alerts/entities, correlates
   candidates, and feeds triage outcomes back into tuning.
