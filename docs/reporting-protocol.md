# Reporting HTTP Protocol

Reporting routes are negotiated with `X-LGYM-Reporting-Protocol` after authorization and before API idempotency.

## Negotiation

- Missing header means protocol `1`.
- The only valid header values are the exact strings `1` and `2`.
- Invalid or unsupported values return `400` with code `REPORTING_PROTOCOL_UNSUPPORTED`.
- Protocol `2` is available automatically after successful startup. There is no `ReportingProtocol:EnableV2` switch: schema and historical snapshot migration run before HTTP traffic and hosted jobs. A migration blocker aborts startup instead of serving an incompletely migrated database.
- Successful reporting responses acknowledge the canonical protocol in `X-LGYM-Reporting-Protocol`, use `Cache-Control: private, no-store`, and include `Vary: X-LGYM-Reporting-Protocol`.

## Capabilities

- `GET /api/trainer/reporting/capabilities` requires the existing trainer-access policy.
- `GET /api/trainee/reporting/capabilities` requires authentication.
- Response after successful startup: `{ "supportedProtocols": [1, 2], "photoTypesEnabled": true }`.
- A protocol-2 probe must receive `X-LGYM-Reporting-Protocol: 2` before a client sends protocol-2 writes.

## Compatibility Rules

- Photo-type dictionary routes require protocol `2`, including reads.
- Protocol-2 uploads require `photoTypeId`; `reportPhotoSlotId` is optional when available.
- Protocol-1 template bodies cannot contain schema-2 `moduleConfig`, `requiredPhotoTypeIds`, or protocol-2 upload fields.
- Protocol-1 clients cannot overwrite an existing schema-2 template.
- Protocol-1 reads adapt issued snapshots only when every frozen photo type is an unchanged system-localized legacy view. Custom or renamed photo types return `426` with code `REPORTING_PROTOCOL_UPGRADE_REQUIRED`.
- Reporting protocol is part of both idempotency scope and fingerprint. Missing-header protocol 1 and explicit protocol 1 are canonical equivalents; protocols 1 and 2 are isolated.

Clients must probe protocol 2, validate the acknowledgement header, and fail closed rather than retrying a protocol-2 body as protocol 1.
