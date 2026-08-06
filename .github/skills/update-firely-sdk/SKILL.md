---
name: update-firely-sdk
description: >-
  Update the Firely .NET SDK (Hl7.Fhir.* packages) across every project in the
  fhir-net-mappinglanguage solution to a specific version, then bump each
  packable project's own NuGet <Version> to a matching version with the
  prerelease sequence reset to beta1 (e.g. SDK 5.13.4 -> package 5.13.4-beta1).
  Use this whenever the user asks to "update Firely SDK", "bump the SDK
  version", or "move to Firely <x.y.z>".
---

# Update Firely SDK to a specific version

This skill upgrades the Firely .NET SDK references used by the
`fhir-net-mappinglanguage` solution and re-stamps the publishable packages so
their version matches the SDK release they were built against.

## Inputs

- **`<targetVersion>`** — the Firely SDK version to move to, in `Major.Minor.Patch`
  form (for example `5.13.4`). The user supplies this. If they did not, ask for it.

## Background: which packages are part of the Firely SDK release train

These packages are versioned together by Firely and **must all be set to
`<targetVersion>`**:

- `Hl7.Fhir.R4`
- `Hl7.Fhir.R4B`
- `Hl7.Fhir.R5`
- `Hl7.Fhir.Specification.R4B`
- `Hl7.Fhir.Specification.R5`
- `Hl7.Fhir.Specification.Data.R4`
- `Hl7.Fhir.Specification.Data.R4B`
- `Hl7.Fhir.Specification.Data.R5`

> Match on the `Hl7.Fhir.*` prefix, but treat the exceptions below as separate
> release trains. If a new `Hl7.Fhir.*` SDK package appears in the future and
> currently shares the old SDK version number, include it too.

### Do NOT change these (they have their own, independent versions)

- `Hl7.Fhir.Validation.Legacy.*` — separate version train (e.g. `5.11.0`).
- `Firely.Fhir.Packages` — separate version train (e.g. `4.9.1`).
- `Microsoft.CodeAnalysis.*`, `MSTest.*`, `Microsoft.NET.Test.Sdk`,
  `coverlet.collector` — unrelated tooling.

### `brianpos.Fhir.*` dependency packages

The `demo-map-server` project references `brianpos.Fhir.*` packages (for example
`brianpos.Fhir.R4B.WebApi.AspNetCore`) that track the SDK number but carry their
own prerelease suffix (e.g. `5.13.2-rc1`). Update the numeric `Major.Minor.Patch`
portion to `<targetVersion>` **but preserve the existing prerelease suffix**
(`-rc1`, etc.). Only change them if a matching prerelease build of that package
is expected to exist; if unsure, call this out to the user rather than guessing.

## Step 1 — Update SDK PackageReference versions in every project

For each `*.csproj` in the solution, set the `Version` attribute of every
release-train package (listed above) to `<targetVersion>`.

Projects that contain these references:

| Project | Firely SDK packages it references |
| --- | --- |
| `Hl7.Fhir.MappingLanguage.R4/Hl7.Fhir.MappingLanguage.R4.csproj` | `Hl7.Fhir.R4` |
| `Hl7.Fhir.MappingLanguage/Hl7.Fhir.MappingLanguage.R4B.csproj` | `Hl7.Fhir.R4B` |
| `Hl7.Fhir.MappingLanguage.R5/Hl7.Fhir.MappingLanguage.R5.csproj` | `Hl7.Fhir.R5` |
| `Test.Hl7.Fhir.MappingLanguage/Test.Hl7.Fhir.MappingLanguage.csproj` | `Hl7.Fhir.R4B`, `Hl7.Fhir.Specification.R4B`, `Hl7.Fhir.Specification.Data.R4`, `.R4B`, `.R5` |
| `Test.Hl7.Fhir.MappingLanguage.R5/Test.Hl7.Fhir.MappingLanguage.R5.csproj` | `Hl7.Fhir.R4B`, `Hl7.Fhir.R5`, `Hl7.Fhir.Specification.R5`, `Hl7.Fhir.Specification.Data.R5` |
| `VersionConversionTester/VersionConversionTester.csproj` | `Hl7.Fhir.R4B`, `Hl7.Fhir.R5`, `Hl7.Fhir.Specification.R4B`, `Hl7.Fhir.Specification.Data.R4`, `.R4B`, `.R5` |
| `demo-map-server/demo-map-server.csproj` | `Hl7.Fhir.Specification.R4B`, `Hl7.Fhir.Specification.Data.R4`, `.R4B`, `.R5` (plus `brianpos.Fhir.*` — see note above) |

Leave `Hl7.Fhir.Validation.Legacy.*` and `Firely.Fhir.Packages` untouched.

## Step 2 — Re-stamp the publishable package versions

Three projects are packable and carry their own `<Version>` element. Set each to
`<targetVersion>-beta1` (the SDK version plus a prerelease suffix with the
sequence number reset to **1**, regardless of the previous beta/alpha/rc number):

| Project | `AssemblyName` | New `<Version>` |
| --- | --- | --- |
| `Hl7.Fhir.MappingLanguage.R4/Hl7.Fhir.MappingLanguage.R4.csproj` | `Hl7.Fhir.R4.MappingLanguage` | `<targetVersion>-beta1` |
| `Hl7.Fhir.MappingLanguage/Hl7.Fhir.MappingLanguage.R4B.csproj` | `Hl7.Fhir.R4B.MappingLanguage` | `<targetVersion>-beta1` |
| `Hl7.Fhir.MappingLanguage.R5/Hl7.Fhir.MappingLanguage.R5.csproj` | `Hl7.Fhir.R5.MappingLanguage` | `<targetVersion>-beta1` |

Example: for `<targetVersion> = 5.13.4`, set `<Version>5.13.4-beta1</Version>`.

> Always reset the suffix to `beta1`. Do not carry over a previous `-beta3`,
> `-alpha2`, or `-rc1` sequence number.

## Step 3 — Validate

1. Restore and build the full solution to confirm the new SDK version resolves:
   ```powershell
   dotnet build
   ```
2. If the requested SDK version does not exist on NuGet, the restore will fail —
   report this back to the user and do not proceed.
3. Optionally run the test projects to confirm runtime compatibility.

## Notes

- Prefer editing each `*.csproj` directly (search for `Version="<oldSdkVersion>"`).
- Keep all other package versions, target frameworks, and project settings unchanged.
- This solution does not use central package management (no `Directory.Packages.props`),
  so versions live inline in each `*.csproj`.
