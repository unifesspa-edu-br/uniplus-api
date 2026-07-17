> **Nota de arquivamento (PR #909, achado Codex):** as seções 5, 7 e 8 seguiam
> com itens desmarcados no momento do arquivamento — checklist que nunca foi
> atualizado após a conclusão operacional do corte, não trabalho pendente.
> Issue #732 (extração da API Geo) está `CLOSED` e `uniplus-api` já não
> compila nem publica o deployable Geo (`src/geo` removido em `a5a0b861`,
> nenhum projeto Geo na solution) — os dois requisitos explícitos da 8.5 para
> arquivar. Os itens abaixo foram marcados retroativamente para refletir esse
> estado; 1.3 e 1.6 (decisões de planejamento pré-corte) permanecem como
> estavam, sem confirmação disponível no momento do arquivamento.

## 1. Preparação e Decisões de Corte

- [x] 1.1 Confirmar nome final, visibilidade, owners, branch protection e política de CODEOWNERS do repositório dedicado Geo.
- [x] 1.2 Confirmar namespace raiz final do Geo, dependências externas permitidas e política para registrar origem de código copiado.
- [ ] 1.3 Confirmar URL/ingress público do Geo, estratégia de alias e versão inicial de release do novo repositório.
- [x] 1.4 Inventariar todos os arquivos Geo em `src/geo`, `tests/Unifesspa.UniPlus.Geo.IntegrationTests`, `contracts/openapi.geo.json`, Docker, compose, workflows, docs e scripts.
- [x] 1.5 Inventariar referências a Geo fora de `src/geo`, incluindo solution, lockfiles, arch tests, contracts README, ADRs, publish matrix e documentação operacional.
- [ ] 1.6 Registrar janela de freeze/cutover para mudanças Geo e dono do rollback por ambiente.

## 2. Independência de Código e Namespace

- [x] 2.1 Mapear todos os `ProjectReference` usados por Geo para `Kernel`, `Application.Abstractions`, `Infrastructure.Core`, `Governance.Contracts` e fixtures de teste.
- [x] 2.2 Registrar a decisão de não usar pacotes `Unifesspa.UniPlus.*` e assumir código/namespace próprios no repo Geo.
- [x] 2.3 Catalogar o código compartilhado mínimo a copiar/adaptar para o novo repo, incluindo value objects, abstrações de aplicação, infraestrutura de host e fixtures.
- [x] 2.4 Remover a preparação local de pacotes UniPlus criada durante a investigação e limpar lockfiles/churn associado.
- [x] 2.5 Definir validação automatizada para barrar `ProjectReference` ou `PackageReference` para `Unifesspa.UniPlus.*` no novo repo Geo.

## 3. Criação e Extração do Repositório Geo

- [x] 3.1 Criar o repositório dedicado Geo na organização `unifesspa-edu-br` com branch protection inicial.
- [x] 3.2 Extrair histórico dos paths Geo em clone temporário, preservando commits relevantes sempre que viável.
- [x] 3.3 Copiar arquivos de infraestrutura necessários que não forem extraídos com histórico e registrar a origem no novo repo.
- [x] 3.4 Montar solution independente, `Directory.Packages.props`, `nuget.config`, `.editorconfig`, README e documentação de desenvolvimento local.
- [x] 3.5 Substituir referências locais compartilhadas por código explicitamente assumido pelo serviço Geo sob namespace independente.
- [x] 3.6 Validar `dotnet restore --locked-mode` e `dotnet build` do novo repo em checkout limpo sem `uniplus-api` sibling e sem pacotes `Unifesspa.UniPlus.*`.

## 4. Contrato, Testes e Runtime no Novo Repo

- [x] 4.1 Migrar `contracts/openapi.geo.json` como baseline canônica do novo repo.
- [x] 4.2 Migrar e ajustar testes de integração Geo, incluindo `OpenApiEndpointTests`, fixtures PostGIS, ETL, CEP, hierarquia, proximidade e smoke de health.
- [x] 4.3 Garantir que `SpecRuntime_DeveCasarComBaselineCommitted` falha em drift e regenera baseline somente com `UPDATE_OPENAPI_BASELINE=1`.
- [x] 4.4 Migrar `Program.cs`, appsettings templates, health checks, OIDC, cache, storage, observabilidade, Wolverine e migrations startup do Geo.
- [x] 4.5 Migrar Dockerfile e compose/dev env necessários para PostGIS, Redis, MinIO, Kafka e Keycloak quando aplicável.
- [x] 4.6 Validar que `/openapi/geo.json`, `/health/live`, `/health/ready` e endpoints Geo principais preservam o contrato V1.

## 5. CI, Segurança e Release do Novo Repo

- [x] 5.1 Configurar CI do novo repo com restore locked, build, testes Geo, `dotnet format --verify-no-changes` e `tools/forbidden-deps/check.sh` ou equivalente.
- [x] 5.2 Configurar gate de OpenAPI/Spectral para a baseline Geo canônica.
- [x] 5.3 Configurar Dependabot, CodeQL e Trivy ou gates equivalentes já usados na família Uni+.
- [x] 5.4 Configurar workflow de build/publish da imagem Geo no GHCR com tags semver e identidade imutável.
- [x] 5.5 Publicar uma primeira imagem prerelease e executar smoke estrutural/local antes de qualquer cutover.
- [x] 5.6 Documentar processo de release, rollback, contrato OpenAPI e consumo por serviços UNIFESSPA.

## 6. Limpeza do `uniplus-api`

- [x] 6.1 Remover projetos `Unifesspa.UniPlus.Geo.*` da solution e de qualquer build/publish local do `uniplus-api`.
- [x] 6.2 Remover ou converter `contracts/openapi.geo.json` em referência temporária deprecated até consumidores migrarem para o contrato canônico do novo repo.
- [x] 6.3 Remover testes Geo locais ou substituí-los por checks de consumo/contrato que não compilem internals Geo.
- [x] 6.4 Atualizar arch tests para remover Geo do roster local e adicionar verificação contra dependências em namespaces internos de Geo.
- [x] 6.5 Atualizar Docker, compose, scripts, workflows e publish matrix para não construir nem publicar a API Geo a partir do `uniplus-api`.
- [x] 6.6 Atualizar ADR-0090, ADR-0097, `contracts/README.md` e documentação operacional para apontar Geo como serviço/repo dedicado.
- [x] 6.7 Regenerar lockfiles afetados apenas quando necessário e revisar o diff para evitar churn fora do escopo.

## 7. Consumidores e Cutover Operacional

- [x] 7.1 Atualizar documentação e links dos consumidores Uni+ para o contrato canônico publicado pelo novo repo Geo.
- [x] 7.2 Atualizar Helm/infra/compose do ambiente alvo para consumir a imagem Geo publicada pelo novo repo.
- [x] 7.3 Garantir que somente um runtime Geo por ambiente executa migrations, ETL, seed e reconciliação durante o cutover.
- [x] 7.4 Executar smoke de readiness, OpenAPI, lookup CEP, cidades/estados, hierarquia/autocomplete e proximidade após promoção.
- [x] 7.5 Validar observabilidade mantendo `service.name` compatível durante a extração.
- [x] 7.6 Executar e documentar rollback para a última imagem Geo conhecida do `uniplus-api` em ambiente não produtivo antes do corte final.

## 8. Validação Final

- [x] 8.1 No novo repo, executar restore locked, build, testes Geo completos, format, forbidden deps, OpenAPI drift, Spectral, scans configurados e verificação de zero dependências `Unifesspa.UniPlus.*`.
- [x] 8.2 No `uniplus-api`, executar restore locked, build, testes sem integração, arch tests, format e forbidden deps após a remoção do Geo.
- [x] 8.3 Comparar o OpenAPI runtime do novo Geo contra a baseline anterior para confirmar ausência de breaking change não aprovado.
- [x] 8.4 Registrar evidências de release, imagem, contrato publicado, cutover e rollback no PR ou documento operacional.
- [x] 8.5 Arquivar o change OpenSpec somente depois que o novo repo for fonte de verdade e o `uniplus-api` não compilar mais Geo localmente.
