# Coleção Postman — API de Seleção (ProcessoSeletivo)

Smoke test HTTP completo do agregado **ProcessoSeletivo** (Story #758/#851/#554),
versionado junto da própria API (convenção: uma coleção por API, não compartilhada
— mesmo padrão de `organizacao.postman_collection.json`). Exercita **todos os 22
endpoints** de `ProcessoSeletivoController` + os 2 de `DocumentosEditalController`,
incluindo a configuração de catálogo pré-requisito (Publicações/Organização/
Configuração) e o fluxo de documentos exigidos por gatilho e fase (Story #554,
PR #903), com o cenário-alvo end-to-end da task 7.3 — os 4 sub-casos de gatilho
DNF (GERAL, `MODALIDADE EM`, `CONDICAO_ATENDIMENTO IGUAL`, conjunção AND) mais 4
testes de borda de validação.

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
| **ProcessoSeletivo — Configuração** | `Criar` + as 9 dimensões `Definir*` (etapas, oferta-atendimento, distribuição-vagas, classificação, cronograma-fases, bônus-regional, critérios-desempate, **documentos-exigidos** com os 4 sub-casos de gatilho DNF da 7.3 (GERAL/nível de ensino, `MODALIDADE EM`/renda, `CONDICAO_ATENDIMENTO IGUAL`/laudo, conjunção AND/reservista) + 4 testes de borda (`[Borda]`, 422 com asserção no `code` do `ProblemDetails`), referência-temporal-fatos) |
| **ProcessoSeletivo — Leitura** | `Listar`, `ObterPorId`, `ObterConformidade`, `ObterConformidadeLegal` |
| **ProcessoSeletivo — Publicação** | Upload de Edital em 3 passos (URL pré-assinada MinIO, PUT direto, confirmação) → `Publicar` → `ObterSnapshotVigente` |
| **ProcessoSeletivo — Retificação (atalho)** | Novo Edital confirmado → `Retificar` (atalho atômico, sem sessão) |
| **ProcessoSeletivo — Sessão editorial** | `AbrirRetificacao` → `PUT etapas` com `If-Match` → `AlterarMotivoRetificacao` com `If-Match` → `FecharRetificacao` com `If-Match` (congela N+1) → abre 2ª sessão → `DescartarRetificacao` com `If-Match` (reidrata) |

A coleção **não** é auto-limpante (diferente de `organizacao`): cria um
`ProcessoSeletivo` novo a cada execução e reaproveita catálogos existentes via
tolerância a `409`. Rodar duas vezes seguidas é seguro, mas acumula processos —
aceitável para smoke, não para ambiente compartilhado sem rotina de limpeza.

## Achados de execução

Executada de ponta a ponta contra o stack local (2026-07-17). **56/56 requests
passam, 58/58 assertions** — todo o Setup, as 9 dimensões de `Definir*`
(incluindo o cenário-alvo e2e da task 7.3), toda a Leitura e o ciclo completo de
Publicação/Retificação/Sessão editorial. Reprodutível: rodada duas vezes
seguidas contra o mesmo banco compartilhado, sem falhas em nenhuma.

### Cenário-alvo e2e da task 7.3 — 4 sub-casos de gatilho DNF + bordas

A mesma configuração de `ProcessoSeletivo` desta coleção define, num único PUT
`documentos-exigidos`, os 4 sub-casos exigidos pela 7.3 (Story #554):

| Sub-caso | Fato/operador | O que prova |
|---|---|---|
| Nível de ensino | `aplicabilidade: GERAL`, `condicoes: []` | GERAL nunca avalia gatilho — exigida de todo candidato |
| Renda | `MODALIDADE EM ["LB_PPI_...", "LB_Q_..."]` | Cardinalidade multivalorada (ADR-0111); domínio dinâmico resolvido contra `DistribuicaoVagas` do próprio processo |
| Laudo | `CONDICAO_ATENDIMENTO IGUAL "PCD_..."` | Fato dinâmico resolvido contra `OfertaAtendimento` do próprio processo (não um catálogo fixo) |
| Reservista | `SEXO IGUAL MASCULINO` **E** `FAIXA_ETARIA MAIOR_IGUAL 18`, mesma `clausula` | Conjunção AND dentro de uma cláusula DNF (`CondicaoGatilho`: OU entre cláusulas, E dentro) |

Depois de `Publicar`, `ObterSnapshotVigente` confere que o bloco V1.2 congelado
(`configuracao.documentosExigidos.exigencias[]`) tem as 5 exigências (GERAL + 4
CONDICIONAL) com o `condicaoGatilho` exato de cada uma — inclusive o par
`SEXO`/`FAIXA_ETARIA` na mesma cláusula do cenário reservista.

**4 testes de borda** (`[Borda] ...`, cada um um PUT isolado que deve ser
recusado com 422 antes do PUT válido final) cobrem os limites do validador do
gatilho DNF, com asserção no `code` do `ProblemDetails` — não só no status:

- `MODALIDADE EM` com um código que não está na `DistribuicaoVagas` do processo → `uniplus.selecao.predicado_dnf.valor_fora_do_dominio`.
- `FAIXA_ETARIA MAIOR_IGUAL "18.5"` (decimal onde o domínio exige inteiro) → `uniplus.selecao.predicado_dnf.valor_incompativel_com_tipo`.
- `FAIXA_ETARIA EM [...]` (operador `EM` não é válido para domínio numérico) → `uniplus.selecao.predicado_dnf.operador_incompativel_com_dominio`.
- `GERAL` com uma condição viva não-vazia (CA-01: GERAL nunca convive com gatilho) → `uniplus.selecao.documento_exigido.geral_com_condicao`.

Simplificação assumida: os 5 itens de `documentos-exigidos` usam a mesma fase
de cronograma (a única fase `AVALIACAO` que o smoke provisiona), não fases
distintas por sub-caso — o objetivo da 7.3 é provar a resolução correta do
gatilho DNF e o congelamento do bloco, não a segregação por fase (já coberta
pelos guards de fase da PR-d).

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
