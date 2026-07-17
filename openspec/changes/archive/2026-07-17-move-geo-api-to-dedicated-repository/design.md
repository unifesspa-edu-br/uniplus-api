## Context

O Geo já está separado conceitualmente dentro do `uniplus-api`: possui `Program.cs` próprio, banco `GeoDb`/PostGIS, migrations, ETL, health checks, OpenAPI em `/openapi/geo.json`, baseline `contracts/openapi.geo.json` e suíte `tests/Unifesspa.UniPlus.Geo.IntegrationTests`. A ADR-0090 define Geo como bounded context transversal de localidades; a ADR-0097 mantém Geo fora do host UniPlus como deployable autônomo.

O limite que ainda não está separado é o repositório. O código Geo continua dependendo por `ProjectReference` de fundações locais (`Kernel`, `Application.Abstractions`, `Infrastructure.Core`, `Governance.Contracts`) e divide CI, solution, lockfiles, fitness tests, contratos e documentação com o Uni+. Isso torna a API transversal dependente do ciclo de mudança do `uniplus-api`, embora seu contrato possa servir outros sistemas da UNIFESSPA.

A extração deve criar um projeto 100% independente do UniPlus: o novo repositório terá namespaces e código próprios, não dependerá de pacotes `Unifesspa.UniPlus.*` e poderá duplicar/adaptar código necessário para preservar autonomia operacional.

Consumidores esperados:

- Uni+ web e módulos Uni+ que compõem cidade/endereço no cliente e persistem snapshot.
- Outros serviços UNIFESSPA que precisem de localidades, CEP, logradouro, bairro, distrito, proximidade ou georreferência.
- Operação/DevOps, que precisa publicar, monitorar e reverter Geo sem acoplar a release do Uni+.

Restrições:

- A API HTTP V1 deve permanecer compatível.
- O backend Uni+ não deve passar a depender de chamadas runtime obrigatórias ao Geo para validar snapshots.
- Não deve haver FK cross-banco nem dependência de código contra `Geo.Domain`, `Geo.Application`, `Geo.Infrastructure` ou `Geo.API` a partir do `uniplus-api`.
- O cutover não pode deixar dois processos concorrentes executando migrations, ETL ou reconciliação do Geo sobre o mesmo banco.

## Goals / Non-Goals

**Goals:**

- Criar um repositório dedicado para a API Geo como fonte de verdade de código, contrato, testes, CI, imagem e documentação operacional.
- Preservar o contrato público V1 da API Geo durante a extração.
- Transformar o Geo em uma codebase própria, removendo `ProjectReference` e `PackageReference` para `Unifesspa.UniPlus.*` e copiando/adaptando o código mínimo necessário sob namespace do Geo.
- Dar ao Geo release, versionamento e imagem próprios, compatíveis com GHCR e com os gates atuais de build/test/OpenAPI.
- Limpar o `uniplus-api` para que ele deixe de compilar, testar e publicar Geo, mantendo somente contratos de consumo/documentação necessários.

**Non-Goals:**

- Redesenhar endpoints, payloads, paginação, HATEOAS, vendor media types ou semântica de lookup/proximidade.
- Migrar Portal ou os módulos internos do Uni+ para outros repositórios.
- Trocar IdP/OIDC, modelo de autorização, PostGIS, Wolverine, Redis, MinIO ou observabilidade além do necessário para bootstrap do novo repo.
- Introduzir chamada backend obrigatória do Uni+ para o Geo como substituto da composição no cliente.
- Renomear publicamente o produto/serviço de observabilidade na mesma entrega, salvo se a operação decidir fazer isso no cutover.

## Decisions

### 1. Repositório dedicado como fonte de verdade

O Geo deve sair de `uniplus-api` para um repositório dedicado na organização `unifesspa-edu-br` (nome operacional sugerido: `unifesspa-geo-api`). Esse repo passa a conter solution, `src/geo`, testes, contrato OpenAPI, migrations, Dockerfile, workflows, documentação operacional e ADRs específicas do serviço.

Alternativas consideradas:

- Manter no monorepo com CODEOWNERS mais forte: reduz mudança inicial, mas mantém release, CI e ownership acoplados ao Uni+.
- Criar submódulo Git: preserva separação parcial, mas aumenta atrito de checkout, CI e revisão.
- Copiar sem preservar histórico: mais rápido, mas perde rastreabilidade de ADRs, bugs e decisões recentes.

Decisão: extrair por clone temporário com preservação de histórico de paths Geo sempre que viável, usando cópia direta apenas para arquivos de infraestrutura sem histórico relevante ou muito acoplados.

### 2. Contrato HTTP V1 preservado

O endpoint `/openapi/geo.json`, os prefixos `/api/*`, responses `application/problem+json`, cursores, `_links`, health checks e semântica atual dos endpoints devem continuar válidos. A mudança é de ownership e entrega, não de API funcional.

Alternativas consideradas:

- Versionar imediatamente como `/api/v2`: rejeitado porque não há mudança funcional que justifique quebra.
- Mudar prefixo para refletir marca institucional: rejeitado para esta entrega; pode ser evolução posterior com redirects e versão nova.

### 3. Código próprio e namespace independente

O novo repo não deve depender por `ProjectReference` nem por `PackageReference` de projetos ou pacotes `Unifesspa.UniPlus.*`. As fundações usadas pelo Geo devem ser incorporadas explicitamente ao novo repo como código próprio, reduzidas ao necessário e renomeadas para um namespace raiz independente do Geo.

Namespace raiz operacional sugerido: `Unifesspa.Geo`. A decisão final pode ajustar o nome, mas o princípio é estável: código novo do repo dedicado não deve viver em `Unifesspa.UniPlus.*`.

Escopo mínimo a resolver antes da remoção do código local:

- `Unifesspa.UniPlus.Kernel`
- `Unifesspa.UniPlus.Application.Abstractions`
- `Unifesspa.UniPlus.Infrastructure.Core`
- `Unifesspa.UniPlus.Governance.Contracts`
- fixtures/helpers de teste necessários para Geo

Alternativas consideradas:

- Duplicar todo o `src/shared`: simples no primeiro commit, mas traz código demais e aumenta superfície de manutenção.
- Manter `uniplus-api` como submodule: reduz duplicação, mas mantém o novo repo operacionalmente preso ao antigo.
- Criar um repo/plataforma compartilhada antes da extração: arquiteturalmente limpo, mas pode bloquear a entrega.
- Publicar pacotes internos `Unifesspa.UniPlus.*`: reduz duplicação, mas mantém o Geo semanticamente preso ao UniPlus, contrariando a natureza transversal da API.

Decisão: copiar/adaptar somente o código mínimo necessário e assumi-lo no novo repositório. A duplicação é aceita para garantir independência. Dependências externas NuGet continuam permitidas, desde que não sejam pacotes `Unifesspa.UniPlus.*`.

### 4. Contrato publicado, não arquivo local em outro repo

`contracts/openapi.geo.json` deve migrar como baseline primária do novo repo. Consumidores devem apontar para o endpoint runtime, artefato de release ou documentação pública gerada a partir do novo repo, não para um arquivo dentro de `uniplus-api`.

O `uniplus-api` pode manter uma referência temporária durante a migração, mas essa referência deve ser marcada como espelho/deprecated e removida quando consumidores e docs forem atualizados.

### 5. Cutover operacional em duas fases

A extração deve permitir uma fase de paralelismo de código sem paralelismo de runtime:

1. Novo repo compila, testa e publica imagem sem receber tráfego produtivo.
2. Infra aponta o serviço Geo para a nova imagem/repo mantendo banco e URLs compatíveis.
3. O deployable Geo antigo do `uniplus-api` é removido somente depois do primeiro release verde e do rollback documentado.

Somente um processo Geo deve executar migrations, ETL, seed ou reconciliação sobre o banco `GeoDb` em cada ambiente.

### 6. Observabilidade e service name

O `service.name` atual (`uniplus-geo`) deve ser preservado durante a extração para manter dashboards, alertas e traces comparáveis. Uma eventual troca para `unifesspa-geo` deve ser tratada em mudança posterior, com migração explícita de painéis e consultas.

## Risks / Trade-offs

- Perda de compatibilidade OpenAPI -> manter `SpecRuntime_DeveCasarComBaselineCommitted`, Spectral e comparação do contrato antes/depois da extração.
- Drift de código copiado do UniPlus -> reduzir a cópia ao mínimo, registrar origem/proveniência no novo repo e tratar evoluções futuras como código próprio do Geo, não como sincronização automática com o UniPlus.
- Dois deployables Geo aplicando migrations/ETL no mesmo banco -> usar janela de cutover com um único owner ativo por ambiente e rollback para a imagem anterior.
- Histórico Git incompleto -> fazer extração em clone temporário, validar histórico dos paths principais e registrar arquivos copiados sem histórico.
- Consumidores apontando para `contracts/openapi.geo.json` no `uniplus-api` -> publicar novo artefato canônico e atualizar docs/links antes de remover o arquivo local.
- Escopo expandir para plataforma compartilhada completa -> limitar a primeira entrega ao código mínimo necessário para compilar Geo de forma independente; evoluções de plataforma entram como follow-up.
- Nome institucional ainda não decidido -> usar nome operacional no plano e confirmar antes da criação do repo público.

## Migration Plan

1. Preparar inventário de arquivos Geo: projetos, testes, contracts, Docker/compose, workflows, docs/ADRs e referências em solution/fitness tests.
2. Confirmar nome/visibilidade do novo repo, namespace raiz independente e dependências externas permitidas.
3. Criar o novo repo e extrair histórico dos paths Geo em clone temporário.
4. Montar solution independente no novo repo, substituindo `ProjectReference` compartilhado por código próprio/copied assumido pelo Geo.
5. Portar CI: restore locked, build, testes Geo, `dotnet format --verify-no-changes`, forbidden deps, SpecRuntime, Spectral, Trivy/CodeQL quando aplicável.
6. Portar Dockerfile, compose/dev env, health checks, appsettings templates e documentação operacional.
7. Publicar primeira imagem GHCR e validar smoke local/integrado contra `GeoDb`/PostGIS, Redis, MinIO, Kafka/OIDC conforme dependência ativa.
8. Atualizar consumidores, documentação e links de contrato para o novo repo/artefato.
9. Remover Geo do `uniplus-api`: solution, projetos, testes, baseline primária, Docker/publish matrix, arch tests e docs que afirmem ownership local.
10. Executar gates do `uniplus-api` após a remoção e registrar ADR/follow-up de topologia.
11. Fazer cutover por ambiente com rollback documentado para a última imagem Geo gerada pelo `uniplus-api`.

## Resolved Decisions

- Nome final do repositório: `unifesspa-geo-api`.
- Visibilidade inicial: público, alinhado ao `uniplus-api`.
- Branch protection inicial em `main`: revisão obrigatória com 1 aprovação, stale reviews descartadas, aprovação do último push exigida, histórico linear, conversas resolvidas, force push e deleção bloqueados.
- CODEOWNERS: não exigir revisão de CODEOWNERS enquanto não houver time visível/confirmado para ownership no novo repo.
- Namespace raiz final do novo repo: `Unifesspa.Geo`.
- Dependências externas permitidas: pacotes NuGet públicos/externos necessários ao runtime e aos testes, desde que não sejam `Unifesspa.UniPlus.*`.
- Política de código copiado: código trazido do `uniplus-api` passa a ser código próprio do Geo sob namespace `Unifesspa.Geo.*`; a origem deve ficar registrada em `docs/extraction-provenance.md` e nas notas de implementação.

## Open Questions

- A URL pública/ingress atual do Geo será mantida integralmente ou haverá alias institucional?
- O primeiro release do novo repo começa em `v1.0.0` por contrato V1 ou herda o versionamento atual do Uni+?
