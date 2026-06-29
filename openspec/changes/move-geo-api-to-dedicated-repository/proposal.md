## Why

A API Geo já é uma API transversal de localidades, endereçamento e georreferência, consumida por composição por diferentes módulos e potencialmente por outros serviços da UNIFESSPA. Mantê-la dentro do repositório `uniplus-api` mistura ciclos de release, governança de contrato e ownership de uma capacidade institucional que não pertence somente ao Uni+.

## What Changes

- Mover o código da API Geo (`src/geo`), seus contratos OpenAPI, testes, migrations, infraestrutura de build/deploy e documentação operacional para um repositório dedicado.
- Preservar o contrato HTTP público da API Geo em V1, incluindo prefixos, vendor media types, paginação por cursor, HATEOAS, health checks e `/openapi/geo.json`.
- Definir a API Geo como serviço institucional transversal, com ownership, CI, versionamento e release independentes do monólito modular Uni+.
- Substituir acoplamentos de código locais por código próprio do Geo, copiado/adaptado quando necessário, sem dependência de pacotes UniPlus.
- Atualizar o `uniplus-api` para deixar de compilar, testar e publicar o deployable Geo, mantendo apenas referências de consumo e documentação de integração.
- **BREAKING operacional**: contribuições, pipelines, paths de código, Docker/build context e baselines de contrato do Geo deixam de viver em `uniplus-api` e passam a ser governados pelo novo repositório dedicado.

## Capabilities

### New Capabilities

- `geo-dedicated-repository`: cobre a extração da API Geo para um repositório dedicado, preservando contrato público, governança de release, código e namespaces próprios, migração de CI/deploy e integração segura com consumidores Uni+ e demais serviços UNIFESSPA.

### Modified Capabilities

- Nenhuma capability OpenSpec existente. Não há specs em `openspec/specs/` nesta árvore; a mudança introduz uma capability nova para formalizar a extração.

## Impact

- Código: `src/geo/**`, `tests/Unifesspa.UniPlus.Geo.IntegrationTests/**`, referências Geo em fitness tests, solution, lockfiles, Dockerfiles, compose, scripts e documentação.
- Contratos: `contracts/openapi.geo.json` deve migrar como baseline primária do novo repositório; consumidores devem usar URL/artefato publicado em vez de depender do arquivo local do `uniplus-api`.
- Infraestrutura: pipelines de build, test, Spectral/OpenAPI drift, imagem Docker, banco `GeoDb`/PostGIS, Redis, MinIO, Kafka/Wolverine quando aplicável, observabilidade e health checks precisam existir no novo repositório.
- Independência de código: trechos hoje usados de Kernel, Application.Abstractions, Infrastructure.Core, Governance.Contracts e fixtures devem ser copiados/adaptados para namespaces próprios do Geo, sem `ProjectReference` ou `PackageReference` para `Unifesspa.UniPlus.*`.
- Governança: ADRs e documentação pública devem registrar que Geo é serviço transversal institucional e que o Uni+ consome seus dados por contrato, sem chamada backend obrigatória nem FK cross-banco.
