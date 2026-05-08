---
status: "proposed"
date: "2026-05-08"
decision-makers:
  - "Tech Lead"
consulted:
  - "DevOps"
informed:
  - "Equipe `uniplus-api`"
---

# ADR-0050: GitHub Container Registry e estratégia de tagging das imagens da `uniplus-api`

## Contexto e enunciado do problema

A Fase 5 do plano de deploy (cluster standalone) está bloqueada porque as imagens dos módulos `uniplus-api-selecao` e `uniplus-api-ingresso` ainda não são publicadas em um registry acessível pelo cluster. Os Dockerfiles existem (`docker/Dockerfile.selecao`, `docker/Dockerfile.ingresso`), o pipeline de build/test já roda em PR ([ADR-0017](0017-kubernetes-com-helm-para-orquestracao.md), Feature [#7](https://github.com/unifesspa-edu-br/uniplus-api/issues/7)) e os Helm charts (Feature [#10](https://github.com/unifesspa-edu-br/uniplus-api/issues/10)) já assumem que as imagens estão disponíveis em runtime — mas não há workflow de publish, não há convenção de naming nem política de tagging.

Decisões adjacentes precisam ser ancoradas: o scan Trivy ([#24](https://github.com/unifesspa-edu-br/uniplus-api/issues/24)) precisa de um alvo concreto, o Helm chart precisa saber qual tag consumir por ambiente, e o scaffold da Portal API ([#336](https://github.com/unifesspa-edu-br/uniplus-api/issues/336)) entrará na mesma esteira assim que estiver pronto.

A organização já valida o GHCR como registry institucional via imagem composta `ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.x` (Keycloak 26.5.7 + JAR `cpf-matcher`), publicada e consumida em produção pelo realm Uni+. Reaproveitar GHCR mantém a esteira homogênea e elimina credenciais externas adicionais.

## Drivers da decisão

- Destravar Fase 5 (deploy das apps no cluster standalone) com mínimo trabalho novo
- Não reabrir discussão sobre registry institucional já validado (GHCR)
- Compatibilidade com ArgoCD/Helm: tags imutáveis por SHA + tag mutável `main` para promoção
- Pré-requisito do scan Trivy ([#24](https://github.com/unifesspa-edu-br/uniplus-api/issues/24)) e do Helm chart ([#10](https://github.com/unifesspa-edu-br/uniplus-api/issues/10))
- Coexistência com módulos futuros (Portal API após [#336](https://github.com/unifesspa-edu-br/uniplus-api/issues/336)) sem precisar reescrever pipeline
- Permissões de CI escopadas: sem PAT pessoal, login via `GITHUB_TOKEN`
- Custo: GHCR é gratuito para repositórios públicos da organização

## Opções consideradas

- **GHCR (`ghcr.io/unifesspa-edu-br/uniplus-api-<modulo>`)** — registry institucional já validado
- **Docker Hub (`unifesspa/uniplus-api-<modulo>`)** — registry público externo
- **Registry interno Unifesspa** — registry on-prem hospedado em DC institucional

## Resultado da decisão

**Escolhida:** "GHCR (`ghcr.io/unifesspa-edu-br/uniplus-api-<modulo>`)", porque é o único registry já em produção na organização (imagem composta do Keycloak), tem custo zero para repositórios públicos, autentica via `GITHUB_TOKEN` sem credencial extra e elimina latência de decisão para destravar a Fase 5.

A convenção de naming dedicada por módulo `uniplus-api-<modulo>` evita o anti-padrão de uma única imagem multipropósito e permite Helm chart per-app sem ramificação.

**Trigger de publish:** o workflow só dispara em `push` de tag `v*` (ex.: `v0.1.0`). Push em `main` **não** publica imagem. Disciplina de release explícito — sem rolling tag mutável, sem ambiguidade sobre "qual é o estado atual de DEV". Devs que precisam de imagem de DEV constroem localmente via `docker compose` com build context. ArgoCD e Helm em qualquer ambiente sempre pinam um semver ou um sha — nunca um canal mutável.

**Tags publicadas para cada release `v<X>.<Y>.<Z>`:**

- `sha-<7-curto>` — identidade imutável do commit (sempre publicada)
- `v<X>.<Y>.<Z>` — release exata (imutável)
- `v<X>.<Y>` — pin de minor (rola com patches subsequentes)
- `v<X>` — pin de major (rola com minors+patches subsequentes)

Sem `latest` — convenção é dispensável quando `v<X>` já fornece soft-pinning automático e ArgoCD pina sempre `v<X>.<Y>.<Z>` (ou `sha-*`) explicitamente. Sem `main` — nenhum consumidor produtivo deve apontar para um canal de branch que muda em cada merge.

Multi-arch fica restrito a `linux/amd64` neste momento; `linux/arm64` é deferido até existir cluster ARM ou demanda concreta de Apple Silicon em CI. SBOM e attestation (cosign keyless via OIDC do GitHub) ficam parqueados como follow-up — registrados em backlog mas fora do escopo de unblock.

## Consequências

### Positivas

- Fase 5 destrava com 1 workflow novo (`publish-images.yml`) consumindo GHCR já validado
- ArgoCD pode pinar `sha-*` sem risco de mutação; promoção entre ambientes troca apenas a tag
- Trivy ([#24](https://github.com/unifesspa-edu-br/uniplus-api/issues/24)) ganha alvo concreto e pode rodar como gate no mesmo workflow
- Adicionar `uniplus-api-portal` após [#336](https://github.com/unifesspa-edu-br/uniplus-api/issues/336) requer apenas append na matriz do workflow
- Sem credenciais externas: `GITHUB_TOKEN` com escopo `packages: write` resolve auth

### Negativas

- Acoplamento ao GitHub: migração para outro registry no futuro exige mirror prévio
- Sem multi-arch limita rodar imagens em workstations Apple Silicon (devs usam compose local com .NET SDK, mitigando)
- Retention granular (cleanup de tags antigas) exige Action externa (`actions/delete-package-versions`) — não é nativo do GHCR

### Neutras

- Visibilidade pública das imagens espelha a do repositório (`uniplus-api` é público); imagens privadas exigiriam mudança de visibility tanto do repo quanto do package
- SBOM/attestation cosign deferidos não impedem publish, mas precisarão ser adicionados antes do gate de produção quando HML/PROD existirem

## Confirmação

Verificável por:

- Workflow `.github/workflows/publish-images.yml` presente, sem trigger em `push: branches`, apenas em `push: tags v*`
- `gh api /orgs/unifesspa-edu-br/packages?package_type=container --jq '.[].name'` lista `uniplus-api-{selecao,ingresso,portal}` após o primeiro `git tag v* && git push --tags`
- `docker pull ghcr.io/unifesspa-edu-br/uniplus-api-selecao:v0.1.0` funciona sem auth (visibilidade pública)
- `docker pull ghcr.io/unifesspa-edu-br/uniplus-api-selecao:main` falha com 404 — `main` **não** é tag publicada
- Tag de release `v0.1.0` produz simultaneamente `:v0.1.0`, `:v0.1`, `:v0` e `:sha-<7>`
- Helm chart consome tag por `Values.image.tag` (semver explícito), sem hardcode e sem `latest`

## Prós e contras das opções

### GHCR

- Bom, porque já está em produção na organização (imagem composta `uniplus-keycloak`), elimina latência de decisão
- Bom, porque autentica via `GITHUB_TOKEN` sem PAT nem secret externo
- Bom, porque tem custo zero para repositórios públicos
- Ruim, porque acopla o registry ao provedor de SCM (lock-in moderado)
- Ruim, porque cleanup de tags antigas exige Action externa, não é nativo

### Docker Hub

- Bom, porque é o registry mais conhecido publicamente
- Ruim, porque exige conta institucional separada (`unifesspa` ou similar) com credenciais administradas fora do GitHub
- Ruim, porque rate limit anônimo de pull (100/6h) afeta cluster e pipelines
- Ruim, porque duplica o trabalho de auditoria de segurança já feita no GHCR

### Registry interno Unifesspa

- Bom, porque mantém todas as imagens dentro da rede institucional
- Ruim, porque não existe ainda — exigiria projeto de provisionamento, backup, hardening, monitoração antes de destravar Fase 5
- Ruim, porque cluster standalone ainda não tem conectividade configurada para registry on-prem
- Ruim, porque adiciona dependência de DevOps Unifesspa em cada deploy do CI

## Mais informações

- Story de implementação: a ser criada como sub-issue da Feature [#7](https://github.com/unifesspa-edu-br/uniplus-api/issues/7) (CI/CD pipelines)
- Trivy gate: [#24](https://github.com/unifesspa-edu-br/uniplus-api/issues/24)
- Portal API scaffold: [#336](https://github.com/unifesspa-edu-br/uniplus-api/issues/336) — quando fechada, append `uniplus-api-portal` na matriz
- Helm charts base: Feature [#10](https://github.com/unifesspa-edu-br/uniplus-api/issues/10)
- ADR pareada no consumidor frontend: `uniplus-web/docs/adrs/0020-registry-ghcr-e-tagging.md`
- Decisão de orquestração: [ADR-0017](0017-kubernetes-com-helm-para-orquestracao.md)
- Origem: pré-requisito da Fase 5 do plano de deploy do cluster standalone (snapshot 2026-05-07)
