# CI — Publicação de imagens Docker

Documento operacional sobre o workflow `.github/workflows/publish-images.yml`. As decisões binding (registry, naming, tagging, retention) vivem em [ADR-0050](adrs/0050-registry-ghcr-e-tagging.md) — este guia explica **como o workflow opera** e **como consumir** as imagens publicadas.

## Imagens publicadas

| Módulo | Imagem |
|---|---|
| Seleção | `ghcr.io/unifesspa-edu-br/uniplus-api-selecao` |
| Ingresso | `ghcr.io/unifesspa-edu-br/uniplus-api-ingresso` |
| Portal | `ghcr.io/unifesspa-edu-br/uniplus-api-portal` |

Visibilidade: pública (espelha o repositório).

## Triggers e tags publicadas

| Evento | Tags geradas |
|---|---|
| `push` em `main` | `sha-<7>`, `main`, `latest` |
| `push` em tag `v<semver>` (ex.: `v1.2.3`) | `sha-<7>`, `v1.2.3`, `v1.2`, `v1` |

A tag `sha-<7>` é **sempre imutável** e é a recomendada para ArgoCD pinar em ambientes promovidos. `main` e `latest` são canais mutáveis para DEV. Tags `v*` representam releases e nunca devem ser sobrescritas — release branch protection é responsabilidade do operador (não force-push em tags publicadas).

## Plataformas

Apenas `linux/amd64`. ARM (`linux/arm64`) está deferido até existir cluster ARM ou demanda concreta de Apple Silicon — ver [ADR-0050](adrs/0050-registry-ghcr-e-tagging.md#não-objetivos).

## Cache

Build cache via `type=gha` (GitHub Actions cache backend) com `scope=<module>` por matriz. Cada módulo tem cache isolado — alterar `Directory.Packages.props` invalida os 3 caches; alterar só `src/portal/` invalida apenas o cache do portal.

## Smoke test

Após o push, cada imagem é instanciada via `docker run` com `ASPNETCORE_URLS=http://+:8080` e `ASPNETCORE_ENVIRONMENT=Production`. O step espera até 30s para `/health/live` responder 200.

`/health/live` é a sonda de **liveness**: só verifica que o processo .NET está vivo e o Kestrel está respondendo. **Não** depende de Postgres, Kafka ou Redis — por isso é seguro rodar em CI sem infra.

A sonda de readiness completa (`/health` agregado, com checagem de Postgres/Kafka/Redis/MinIO) é exercida no cluster Kubernetes via Helm — fora do escopo deste workflow.

## Como consumir

### Pull anônimo (visibilidade pública)

```bash
docker pull ghcr.io/unifesspa-edu-br/uniplus-api-selecao:main
```

### Pin por SHA (recomendado em produção)

```bash
docker pull ghcr.io/unifesspa-edu-br/uniplus-api-selecao:sha-1a2b3c4
```

### Helm chart

Configurar em `values.yaml`:

```yaml
image:
  repository: ghcr.io/unifesspa-edu-br/uniplus-api-selecao
  tag: sha-1a2b3c4   # imutável para promoção entre ambientes
  pullPolicy: IfNotPresent
```

ArgoCD ApplicationSet atualiza `image.tag` por sync — não usar `latest` em ambiente promovido.

### Listar tags disponíveis

```bash
gh api /orgs/unifesspa-edu-br/packages/container/uniplus-api-selecao/versions \
  --jq '.[].metadata.container.tags[]' | sort -u
```

## Observações operacionais

- **Permissões do `GITHUB_TOKEN`:** `contents: read`, `packages: write`, `id-token: write` (este último prepara o terreno para attestation OIDC futura).
- **Concurrency:** publishes da mesma `ref` são serializados (`cancel-in-progress: false`) — perda de tag mutável (`main`/`latest`) nunca é aceitável.
- **Cleanup:** GHCR retention manual é responsabilidade futura. Política planejada: `v*` indefinido, `sha-*` 30 dias, `<branch>` purga ao deletar branch — ver [ADR-0050 § Consequências](adrs/0050-registry-ghcr-e-tagging.md#negativas).

## Próximos passos (deferidos)

- **SBOM e attestation cosign keyless** via OIDC do GitHub — issue separada quando HML exigir.
- **Trivy gate** — issue [#24](https://github.com/unifesspa-edu-br/uniplus-api/issues/24) plugando como step adicional no workflow.
- **Multi-arch ARM** — issue separada quando demanda surgir.
