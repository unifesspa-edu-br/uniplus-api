# Coleção Postman — API de Seleção (ProcessoSeletivo)

Smoke test HTTP completo do agregado **ProcessoSeletivo** (Story #758/#851/#554),
versionado junto da própria API (convenção: uma coleção por API, não compartilhada
— mesmo padrão de `organizacao.postman_collection.json`). Exercita **todos os 22
endpoints** de `ProcessoSeletivoController` + os 2 de `DocumentosEditalController`,
incluindo a configuração de catálogo pré-requisito (Publicações/Organização/
Configuração) e o fluxo de documentos exigidos por gatilho e fase (Story #554,
PR #903).

## Arquivos

- `selecao.postman_collection.json` — a coleção (Postman v2.1).
- `selecao.postman_environment.json` — ambiente local (dev): URLs, client e
  credenciais do realm `unifesspa-dev-local`. Valores **dev-only**.

## Pré-condições

O módulo Seleção é servido pela **API UniPlus** (monólito modular, serviço
`uniplus-api`), junto de Configuração/Organização/Publicações — não existe API
"Seleção" própria em porta separada. O override `docker-compose.smoke.yml`
realinha a API ao realm `unifesspa-dev-local` (o override default usa `unifesspa`,
dos frontends) — sem ele o token dev-local do environment é rejeitado com 401.

```bash
cd repositories/uniplus-api
cp docker/.env.example docker/.env                                          # se ainda não existir
cp docker/docker-compose.override.example.yml docker/docker-compose.override.yml  # se ainda não existir

docker compose -f docker/docker-compose.yml \
               -f docker/docker-compose.override.yml \
               -f docker/docker-compose.smoke.yml \
               --env-file docker/.env --project-directory docker \
               up -d postgres redis kafka minio apicurio keycloak uniplus-api
```

Aguarde `uniplus-api` ficar `healthy`:

```bash
until [ "$(docker inspect --format='{{.State.Health.Status}}' docker-uniplus-api-1 2>/dev/null)" = "healthy" ]; do
  sleep 3
done
```

A API fica em `:5200` (`base_url` do environment). O Keycloak (`:8080`) importa o
realm `unifesspa-dev-local` com o usuário `admin` (role `plataforma-admin` — a
única com acesso a todo o `ProcessoSeletivoController`, que é `[Authorize(Roles =
"plataforma-admin")]` na classe inteira).

## Rodar (Newman)

```bash
cd repositories/uniplus-api
P=src/selecao/Unifesspa.UniPlus.Selecao.API/postman
npx --yes newman@6.2.1 run "$P/selecao.postman_collection.json" -e "$P/selecao.postman_environment.json" \
  --reporters cli --reporter-cli-no-banner
```

Ou importe ambos os arquivos no Postman e selecione o ambiente — a coleção roda
**em ordem sequencial** (cada folder monta pré-requisitos do próximo).

## O que é coberto

| Folder | Cobre |
|---|---|
| **Auth** | Password grant contra Keycloak (`unifesspa-dev-local`) |
| **Setup — Publicações** | `POST admin/tipos-ato` (EDITAL_ABERTURA, EDITAL_RETIFICACAO) — tolera 409 em reexecuções |
| **Setup — Organização** | `POST admin/instituicao` (singleton, tolera 409) + `GET instituicao` + `POST admin/unidades` |
| **Setup — Configuração** | Árvore Campus→LocalOferta→Curso→OfertaCurso + Modalidade + TipoDocumento + FaseCanonica (nenhum pré-seedado) |
| **ProcessoSeletivo — Configuração** | `Criar` + as 9 dimensões `Definir*` (etapas, oferta-atendimento, distribuição-vagas, classificação, cronograma-fases, bônus-regional, critérios-desempate, **documentos-exigidos** com gatilho DNF condicional, referência-temporal-fatos) |
| **ProcessoSeletivo — Leitura** | `Listar`, `ObterPorId`, `ObterConformidade`, `ObterConformidadeLegal` |
| **ProcessoSeletivo — Publicação** | Upload de Edital em 3 passos (URL pré-assinada MinIO, PUT direto, confirmação) → `Publicar` → `ObterSnapshotVigente` |
| **ProcessoSeletivo — Retificação (atalho)** | Novo Edital confirmado → `Retificar` (atalho atômico, sem sessão) |
| **ProcessoSeletivo — Sessão editorial** | `AbrirRetificacao` → `PUT etapas` com `If-Match` → `AlterarMotivoRetificacao` com `If-Match` → `FecharRetificacao` com `If-Match` (congela N+1) → abre 2ª sessão → `DescartarRetificacao` com `If-Match` (reidrata) |

A coleção **não** é auto-limpante (diferente de `organizacao`): cria um
`ProcessoSeletivo` novo a cada execução e reaproveita catálogos existentes via
tolerância a `409`. Rodar duas vezes seguidas é seguro, mas acumula processos —
aceitável para smoke, não para ambiente compartilhado sem rotina de limpeza.

## Achados de execução

Executada de ponta a ponta contra o stack local (2026-07-17). **49/49 requests
passam, 50/50 assertions** — todo o Setup, as 9 dimensões de `Definir*`, toda a
Leitura e o ciclo completo de Publicação/Retificação/Sessão editorial.

### Bug real corrigido — DI keyed/não-keyed do mesmo tipo (issue #904)

`POST {id}/documentos-edital` (`IniciarUpload`) respondia **500** de forma
determinística — `MinioStorageService.GarantirBucketExisteAsync` chamava
`_minioClient.BucketExistsAsync(...)`, mas o cliente **interno** (que devia usar
`Storage:Endpoint=minio:9000`) conectava em `localhost:9000` — dentro do
container `uniplus-api`, nada escuta essa porta → `Connection refused`.

Causa raiz: `StorageServiceCollectionExtensions` registrava DOIS `IMinioClient`
— um **não-keyed** (`AddSingleton`, `Storage:Endpoint`) e um **keyed**
`"storage-public"` (`Storage:PublicEndpoint`, usado só para assinar URLs
pré-assinadas devolvidas a clientes externos). `MinioStorageService` injetava os
dois no mesmo construtor, um sem atributo e outro com `[FromKeyedServices]`.
Diagnóstico direto (log no momento da resolução, depois `GetHashCode()`/
`ReferenceEquals` dos dois campos) provou que o container de DI
(`Microsoft.Extensions.DependencyInjection`) injetava a **mesma instância keyed**
nos DOIS parâmetros do construtor sempre que os dois clientes eram de fato
distintos (`Storage:Endpoint` ≠ `Storage:PublicEndpoint`) — o parâmetro
"ambient" (sem chave) não resolvia o registro não-keyed como esperado.

**Corrigido** registrando os DOIS clientes como keyed (`storage-internal` e
`storage-public` — ver `StorageServiceCollectionExtensions.StorageInternalClientKey`/
`StoragePublicClientKey`), eliminando a ambiguidade na raiz: nenhum consumidor
injeta mais um `IMinioClient` "sem chave" que possa ser confundido com um keyed
do mesmo tipo. `MinioHealthCheck` e `MinioStorageService` atualizados. Teste de
regressão em `StorageServiceCollectionExtensionsTests` (Infrastructure.Core).

### Achado secundário — bug do próprio SDK MinIO, documentado (não corrigido)

A URL pré-assinada de upload traz um parâmetro de query espúrio
`content-type=Minio.DataModel.Args.PresignedPutObjectArgs` (o NOME DO TIPO .NET
onde deveria estar `application/pdf`). Rastreado até
`ObjectOperations.PresignedPutObjectAsync` no SDK `Minio` — `Convert.ToString(args.GetType())`
passado como "metaData" em vez do valor real; confirmado presente em **todas**
as versões lançadas (6.0.5 até a atual 7.0.0); o `master` reescreveu esse trecho
por completo mas ainda não tem release. Inofensivo na prática — `X-Amz-SignedHeaders=host`
só assina o header `Host`, o servidor MinIO nunca valida esse parâmetro — mas
**não é corrigível por reescrita da URL**: confirmado por teste que o parâmetro
faz parte do canonical request da assinatura SigV4 (removê-lo devolve `403
SignatureDoesNotMatch`). Corrigi-lo de verdade exigiria reimplementar a
assinatura SigV4 manualmente, bypassando `PresignedPutObjectAsync` do SDK —
risco desproporcional a um problema cosmético. Documentado como comentário em
`MinioStorageService.GerarUrlUploadTemporariaAsync`.

### Bugs de contrato corrigidos durante a construção da coleção

- `Modalidade.Codigo` recusa hífen (`PredicateValidator` — só maiúsculas, dígitos
  e `_`); a coleção usa `AC_SMK_{{run_suffix}}`.
- `FaseCanonica.AgrupaEtapas=true` só é aceito para o código canônico
  `AVALIACAO` (`FaseCanonica.AgrupaEtapasApenasAvaliacao`) — a coleção cria a fase
  canônica com esse código, não um código arbitrário como `RESULTADO_FINAL`.
- Uma fase de cronograma com `ProduzResultado=true` (herdado por snapshot-copy da
  FaseCanonica) exige `AtoProduzidoCodigo` não-nulo — a coleção cadastra um
  `TipoAtoPublicado` `RESULTADO_FINAL` em Setup para satisfazer o guard.

Nenhum desses três é falha da coleção nem do módulo Seleção — são as regras de
negócio corretas dos módulos Configuração/Publicações, apenas não documentadas
neste README antes da primeira execução real.
