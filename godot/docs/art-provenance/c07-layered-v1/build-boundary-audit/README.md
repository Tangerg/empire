# Audit source / production build boundary

Fresh production build exposed CS0111: archived standalone GranaryCanopyCandidateProbe and GranaryLeftShedProbe copies under docs were being compiled by the SDK default glob. Earlier successful external Probe.csproj builds did **not** establish production build success. Historical raw probe sources remain intact.

The single production project now excludes docs/**/*.cs, alongside its existing subproject exclusions. Scoped .gdignore files belong only to nine archived C07 probe leaf directories, not all docs: several existing C04/C05/C06/C07 checks load JSON from docs/art-provenance. They must remain accessible. Twelve untracked auto-generated audit script UID sidecars were removed; no tracked UIDs were deleted.

Verified fresh production build0warnings/0errors, external probe build0warnings/0errors, scoped Godot editor import/scan exit0 with no regenerated audit UIDs, and a native FileAccess/JSON probe reading all8existing audit JSON resources referenced by those production checks. Fresh resulting library also passes focused square-box and council original-back/front candidate tests. This is a compile/import boundary repair, not a rules/art change or full-campaign regression claim.

Failure/success build logs and AuditResourceBoundaryProbe.gd are retained in the durable recovery package c07-static-preview/. The original duplicate-source failure is preserved, not treated as an unrelated baseline failure.
