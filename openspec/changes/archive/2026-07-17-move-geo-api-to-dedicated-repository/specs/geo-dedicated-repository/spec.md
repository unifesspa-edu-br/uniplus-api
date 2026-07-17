## ADDED Requirements

### Requirement: Dedicated Geo repository ownership
The system SHALL treat the dedicated Geo repository as the source of truth for Geo API source code, tests, migrations, OpenAPI contract, CI, Docker image, release notes, and operational documentation.

#### Scenario: Geo source of truth after migration
- **WHEN** a developer needs to change a Geo endpoint, migration, ETL flow, health check, or OpenAPI baseline after cutover
- **THEN** the change MUST be made in the dedicated Geo repository and not in `uniplus-api`

#### Scenario: UniPlus API no longer builds Geo
- **WHEN** the `uniplus-api` solution is restored, built, tested, or published after cleanup
- **THEN** it MUST NOT compile, test, package, or publish `Unifesspa.UniPlus.Geo.*` projects as local projects

### Requirement: Public Geo HTTP contract preservation
The dedicated Geo API SHALL preserve the existing V1 public HTTP contract during repository extraction, including route prefixes, response shapes, OpenAPI document path, health endpoints, cursor pagination, HATEOAS links, error format, authentication behavior, and vendor media types.

#### Scenario: Existing clients call Geo after cutover
- **WHEN** an existing client calls a Geo V1 endpoint through the configured Geo base URL after cutover
- **THEN** the response contract MUST remain compatible with the pre-extraction `contracts/openapi.geo.json` baseline unless a separately approved breaking-change proposal exists

#### Scenario: Runtime OpenAPI remains available
- **WHEN** a client requests `GET /openapi/geo.json` from the dedicated Geo API
- **THEN** the service MUST return a valid OpenAPI 3.x document with Geo metadata and the same V1 contract semantics

### Requirement: Canonical Geo contract publication
The dedicated Geo repository SHALL own the canonical `openapi.geo.json` baseline and SHALL publish the Geo contract as a versioned artifact or documented public URL for consumers.

#### Scenario: Contract drift is introduced
- **WHEN** a code change alters the runtime OpenAPI document in the dedicated Geo repository
- **THEN** CI MUST fail unless the committed Geo baseline is intentionally regenerated and reviewed

#### Scenario: Consumer needs the Geo contract
- **WHEN** Uni+ or another UNIFESSPA service needs the Geo OpenAPI contract after migration
- **THEN** it MUST consume the contract from the dedicated Geo repository, a release artifact, or the runtime endpoint rather than from `uniplus-api`

### Requirement: Independent codebase and namespace isolation
The dedicated Geo repository SHALL build without `ProjectReference` dependencies to projects inside `uniplus-api` and without `PackageReference` dependencies to `Unifesspa.UniPlus.*` packages. Foundations required by Geo MUST be copied or adapted as explicitly owned Geo source under an independent Geo namespace.

#### Scenario: Geo restore runs in a clean checkout
- **WHEN** the dedicated Geo repository is checked out without a sibling `uniplus-api` checkout
- **THEN** `dotnet restore --locked-mode` and `dotnet build` MUST succeed using only the repository contents and external package sources that are not `Unifesspa.UniPlus.*`

#### Scenario: Geo code uses independent namespace
- **WHEN** production source code in the dedicated Geo repository is scanned after extraction
- **THEN** it MUST NOT define new code under `Unifesspa.UniPlus.*` namespaces and MUST NOT reference `Unifesspa.UniPlus.*` packages or projects

#### Scenario: UniPlus API references Geo internals
- **WHEN** `uniplus-api` is scanned after Geo extraction
- **THEN** it MUST NOT contain code dependencies on old `Unifesspa.UniPlus.Geo.*` internals or new dedicated Geo internals such as `Unifesspa.Geo.*`

### Requirement: Independent CI and release
The dedicated Geo repository SHALL provide independent CI and release pipelines for restore, build, tests, formatting, dependency policy, OpenAPI drift, security scanning, Docker image build, and image publication.

#### Scenario: Pull request changes Geo behavior
- **WHEN** a pull request is opened in the dedicated Geo repository
- **THEN** CI MUST run the Geo-specific gates required to prove code quality, contract compatibility, and security posture before merge

#### Scenario: Release tag is pushed
- **WHEN** an approved semver release tag is pushed from the dedicated Geo repository
- **THEN** the release pipeline MUST publish a versioned Geo image to the institutional container registry with immutable release identity

### Requirement: Safe operational cutover
The migration SHALL ensure that only one Geo runtime owns migrations, ETL workers, seed, and reconciliation for a given environment at any time.

#### Scenario: New Geo deployment is promoted
- **WHEN** the dedicated Geo image is promoted to an environment that previously ran the Geo deployable from `uniplus-api`
- **THEN** the old Geo runtime MUST be stopped or have its migration and ETL ownership disabled before the new runtime starts those responsibilities

#### Scenario: Cutover fails
- **WHEN** the dedicated Geo deployment fails readiness, smoke tests, or contract checks during cutover
- **THEN** operators MUST be able to roll back to the last known-good Geo image and database state documented for that environment

### Requirement: Consumer integration remains contract-based
The Uni+ backend and other consumers SHALL integrate with Geo through HTTP/OpenAPI contracts and persisted snapshots where applicable, without foreign keys across databases or mandatory backend runtime calls for existing snapshot validation flows.

#### Scenario: UniPlus persists a city or address reference
- **WHEN** Uni+ persists a Geo-derived city or address reference after extraction
- **THEN** it MUST keep using the approved snapshot/reference model without adding a database foreign key to Geo tables

#### Scenario: External service consumes Geo
- **WHEN** another UNIFESSPA service needs locality, CEP, address, or proximity data
- **THEN** it MUST use the published Geo API contract instead of depending on Uni+ internal code or database tables

### Requirement: Governance and documentation update
The migration SHALL update architectural and operational documentation to state that Geo is an institutional transversal service with repository, release, and contract governance independent from `uniplus-api`.

#### Scenario: Developer reads UniPlus architecture docs
- **WHEN** a developer reads the `uniplus-api` architecture or deploy documentation after cleanup
- **THEN** the documentation MUST identify Geo as an external dedicated service and point to the canonical Geo repository or public contract

#### Scenario: Developer reads Geo repository docs
- **WHEN** a developer reads the dedicated Geo repository documentation
- **THEN** the documentation MUST explain local development, required infrastructure, contract regeneration, release process, and consumer integration rules
