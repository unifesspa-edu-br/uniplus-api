# CI — Publicação de imagens Docker

Documento operacional sobre o workflow `.github/workflows/publish-images.yml`. As decisões binding (registry, naming, tagging, retention) vivem em [ADR-0050](adrs/0050-registry-ghcr-e-tagging.md) — este guia explica **como o workflow opera**, **como publicar** uma nova versão e **como consumir** as imagens publicadas.

## Imagens publicadas

| Módulo | Imagem |
|---|---|
| Seleção | `ghcr.io/unifesspa-edu-br/uniplus-api-selecao` |
| Ingresso | `ghcr.io/unifesspa-edu-br/uniplus-api-ingresso` |
| Portal | `ghcr.io/unifesspa-edu-br/uniplus-api-portal` |

Visibilidade: pública (espelha o repositório).

## Trigger único — push de tag `v*`

O workflow **só dispara** em `push` de tag que case com `v*` (ex.: `v0.1.0`, `v1.2.3`). **Push em `main` não publica imagem.** Disciplina de release explícito — sem rolling tag mutável, sem ambiguidade sobre "qual é o estado atual de DEV".

| Evento | Tags geradas |
|---|---|
| `push` em tag `v<X>.<Y>.<Z>` | `sha-<7>`, `v<X>.<Y>.<Z>`, `v<X>.<Y>`, `v<X>` |

Sem `latest`, sem `main` — `v<X>` já fornece soft-pinning automático que rola com patches/minors do mesmo major. ArgoCD em ambiente promovido pina `v<X>.<Y>.<Z>` ou `sha-<7>` explicitamente.

## Como publicar uma nova versão

Pré-requisito: o commit alvo já está em `main` (mergeado via PR).

```bash
# 1. Atualiza local
git checkout main && git pull origin main

# 2. Cria a tag anotada (signed se chave configurada)
git tag -a v0.1.0 -m "Release v0.1.0 — primeiro publish dos 3 módulos"

# 3. Publica a tag
git push origin v0.1.0
```

O workflow inicia automaticamente. Em ~5–10 minutos, as 3 imagens estão em GHCR com 4 tags cada (`sha-<7>`, `v0.1.0`, `v0.1`, `v0`).

**Reverter:** tags são imutáveis no GHCR uma vez publicadas. Para revogar uma release, publique uma versão imediatamente posterior (`v0.1.1`) com o conteúdo correto. Não fazer `git push --force` em tag publicada.

## Como devs/ambientes locais usam

Para DEV local não há imagem publicada — devs constroem via `docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up`, que faz build com contexto local e roda em rede com Postgres/Redis/Kafka/Keycloak.

## Plataformas

Apenas `linux/amd64`. ARM (`linux/arm64`) está deferido até existir cluster ARM ou demanda concreta de Apple Silicon — ver [ADR-0050](adrs/0050-registry-ghcr-e-tagging.md#não-objetivos).

## Cache

Build cache via `type=gha` (GitHub Actions cache backend) com `scope=<module>` por matriz. Cada módulo tem cache isolado — alterar `Directory.Packages.props` invalida os 3 caches; alterar só `src/portal/` invalida apenas o cache do portal.

## Smoke test (gate antes do push)

O workflow constrói cada imagem em duas fases para evitar publicar tags imutáveis de uma imagem que falha no smoke:

1. **Build local** com `push: false` + `load: true` — imagem fica no daemon Docker do runner como `smoke/uniplus-api-<module>:ci`.
2. **Validação estrutural** (sem rodar o app):
   - `docker create` — manifest + layers íntegros
   - `docker inspect` confirma `Config.User` ≠ vazio e ≠ `root`
   - `docker inspect` confirma `Config.Entrypoint` referencia uma DLL `Unifesspa.UniPlus.*`
   - `dotnet --info` no runtime da imagem (override do entrypoint) — confirma que o runtime .NET 10 está presente e funcional
3. **Push para GHCR** com as tags semver — só roda se a validação passou. Reusa o cache GHA do build local, então o segundo build é praticamente instantâneo.

Se a validação falhar, **nenhuma tag chega ao GHCR**.

### Por que não rodamos `/health/live` em runtime aqui

A primeira versão do gate tentou `docker run` + curl em `/health/live` e falhou: `Wolverine` valida `ConnectionStrings:*Db` ao construir o host (em `UseWolverineOutboxCascading`), então o app crasha em startup sem um Postgres real ao lado. Para rodar liveness no workflow, seria necessário subir sidecars Postgres + Kafka + Redis em cada entrada da matriz — overhead alto que duplica trabalho que o `docker compose` pós-publish e o Helm em cluster já fazem com infra completa.

A sonda de readiness completa (`/health` agregado, com checagem de Postgres/Kafka/Redis/MinIO) é exercida em compose pós-publish (manualmente ou via deploy de HML) e no cluster Kubernetes via Helm — fora do escopo deste workflow.

## Como consumir

### Pull anônimo (visibilidade pública)

```bash
docker pull ghcr.io/unifesspa-edu-br/uniplus-api-selecao:v0.1.0
```

### Pin por SHA (auditoria SHA-perfeita)

```bash
docker pull ghcr.io/unifesspa-edu-br/uniplus-api-selecao:sha-1a2b3c4
```

### Helm chart

Configurar em `values.yaml`:

```yaml
image:
  repository: ghcr.io/unifesspa-edu-br/uniplus-api-selecao
  tag: v0.1.0          # release explícita; alternativa: sha-1a2b3c4 para SHA pin
  pullPolicy: IfNotPresent
```

ArgoCD ApplicationSet atualiza `image.tag` por sync — sempre semver explícito ou SHA, nunca canal mutável.

### Listar tags disponíveis

```bash
gh api /orgs/unifesspa-edu-br/packages/container/uniplus-api-selecao/versions \
  --jq '.[].metadata.container.tags[]' | sort -u
```

## Observações operacionais

- **Permissões do `GITHUB_TOKEN`:** `contents: read`, `packages: write`, `id-token: write` (este último prepara o terreno para attestation OIDC futura).
- **Concurrency:** `cancel-in-progress: false` — protege contra dois workflows tentando publicar a mesma tag por race (force-push). Tags semver são imutáveis após publicadas; force-push em tag é uma operação que ninguém deve fazer.
- **Cleanup:** GHCR retention manual é responsabilidade futura. Política planejada: `v*` indefinido, `sha-*` 30 dias — ver [ADR-0050 § Consequências](adrs/0050-registry-ghcr-e-tagging.md#negativas).

## Próximos passos (deferidos)

- **SBOM e attestation cosign keyless** via OIDC do GitHub — issue separada quando HML exigir.
- **Trivy gate** — issue [#24](https://github.com/unifesspa-edu-br/uniplus-api/issues/24) plugando como step adicional no workflow.
- **Multi-arch ARM** — issue separada quando demanda surgir.
