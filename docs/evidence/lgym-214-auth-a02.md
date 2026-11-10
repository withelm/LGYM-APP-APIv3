# LGYM-214 AUTH-A02 Validation

Reviewable copy of `.omo/evidence/lgym-55/AUTH-A02/validation.md`.

- Source SHA: `42223961ab683bf9854ac2029341ba1f729dd7e0`.
- Release build: exit `0`, 0 warnings and 0 errors.
- Reporting unit suite: exit `0`, 235 passed.
- Unit full suite: exit `0`, 2156 passed.
- Testing-host Reporting/authorization HTTP slice (`ReportingEngineTests` and `AuthorizationCapabilitiesContractTests`): exit `0`, 37 passed.
- Topology and project-reference guards: exit `0`, 31 passed.

The executable matrix covers U, T1, T2, T3, Admin, Admin+Trainer; submitted/pending/expired/cancelled states; active, historical, and absent facts; malformed source/ownership facts; source-isolated signing; cancelled direct/history denial; typed capability-command parity; returning T1 source isolation; and issued-template edit/delete archive retention.

Testing-host observation: after unlink, T1's `GET /api/trainer/reporting/photos/history?traineeId={traineeId}` returned `200 OK` with only T1's authorized source photo. Unrelated trainer, ordinary user, and anonymous requests returned `404`, `403`, and `401` respectively without signed-capability disclosure.

`ReportingEngineTests.SubmittedArchive_AfterSourceTemplateEditAndDelete_PreservesIssuedTemplateAndPhotoTypeSnapshot` observed `201` issue, `200` submit, `200` update/delete, and `200` trainee archive. The archive retained the original template and `Issued` provenance; its stored snapshot retained the issued photo-type metadata. `ReportingAuthorizationCoordinatorTests.ReturningT1_AfterT1ToT2ToT1_DeniesSubmittedT2SourceRow` passed with T1's row allowed and T2's row denied.

`ReportingEngineTests.SubmissionCollections_ExcludeSoftDeletedSubmissionAndRequestRows` observed `200` trainee and trainer collections containing only the live submitted row. The explicit query filters retain deleted-template snapshots while excluding both soft-deleted submissions and soft-deleted requests.
