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
| **ProcessoSeletivo — Árvore de Satisfação (padrões estruturais, Story #923 §5)** | Matriz dos padrões estruturais reais do corpus (PS Principal 2027 §41-42, PSIQ 2027 §2) que o folder "Configuração" não cobre — aquele usa só folhas soltas na raiz. 5 padrões, cada um com `ProcessoSeletivo` DEDICADO (setup completo + `PUT documentos-exigidos` + `GET` de verificação da árvore persistida): `Certificado E Histórico` (grupo `E` com 2 folhas); `E[OU[RG,CPF], Certidão]` por membro do núcleo familiar (`repetePorEntidade` na raiz, sem aninhar); guarda condicional (folha com gatilho por atributo de entidade, `SOB_GUARDA`); IRPF/isento por adulto sem renda (grupo `OU` com consequência e base legal própria, gatilho AND por 2 atributos); PF + PJ vinculada (repetição pura, sem atributos) |

A coleção **não** é auto-limpante (diferente de `organizacao`): cria um
`ProcessoSeletivo` novo a cada execução e reaproveita catálogos existentes via
tolerância a `409`. Rodar duas vezes seguidas é seguro, mas acumula processos —
aceitável para smoke, não para ambiente compartilhado sem rotina de limpeza.

## Achados de execução

Executada de ponta a ponta contra o stack local (2026-07-19, pós-merge da
change `documentos-exigidos-cobertura-editais`, Feature #914). **59/59
requests passam, 61/61 assertions** — todo o Setup, as 9 dimensões de
`Definir*` (incluindo o cenário-alvo e2e da task 7.3, os operadores de
exclusão, `formatosPermitidos[]` e o congelamento RN08 de metadado de fato),
toda a Leitura e o ciclo completo de Publicação/Retificação/Sessão editorial.
Reprodutível: rodada duas vezes seguidas contra o mesmo banco compartilhado,
sem falhas em nenhuma.

### Task #942 (2026-07-20, pós-merge da Story #923, PR #939) — matriz estrutural + achados

Reexecução completa contra o stack local, com a imagem `uniplus-api` rebuildada
a partir da `main` pós-PR #939 (o container ativo estava com build de antes do
merge — sem rebuild, os dois achados abaixo teriam passado despercebidos por
estarem escondidos atrás de um binário desatualizado). **104/104 requests,
106/106 assertions**, reproduzido em 2 execuções consecutivas.

- **Achado real corrigido — 4 requests `[Borda]` com o contrato FLAT
  pré-Story #920.** As 4 requests de borda de `ProcessoSeletivo — Configuração`
  (`MODALIDADE EM` fora do domínio, `FAIXA_ETARIA` decimal, `EM` incompatível
  com domínio numérico, `GERAL` com condição) ainda enviavam o corpo achatado
  antigo (`exigidoNaFaseId`/`grupoSatisfacaoId` direto na raiz), nunca
  atualizado quando a Story #920 substituiu esse formato pela árvore
  (`{tipo, documento, ...}`). Passavam a falhar com 400 de model-binding em vez
  do 422 de negócio esperado — corrigidas para o formato atual.
- **Achado real corrigido — contagem estática da "Obter Snapshot Vigente"
  desatualizada.** O teste esperava 6 exigências (1 GERAL + 5 CONDICIONAL) no
  bloco congelado, mas a request "Definir Documentos Exigidos" já tinha 8
  folhas há algum tempo (as 2 exigências de cardinalidade qualificada/repetição
  por entidade das Stories #921/#922 foram adicionadas sem atualizar esta
  asserção). Corrigido para 8.
- **Achado real, não corrigido nesta task — [#943](https://github.com/unifesspa-edu-br/uniplus-api/issues/943):**
  `PUT documentos-exigidos` falha com **500** (`ux_nos_exigencia_raiz_ordem`
  duplicate key) ao substituir uma árvore que já contém um grupo `E`/`OU` por
  qualquer outra — reproduzido de forma consistente contra o stack local, em
  múltiplas combinações de árvore. Bloqueador de produção (quebra a edição de
  qualquer árvore não-trivial via retificação), mas fora do escopo desta task
  corrigir — a matriz de padrões estruturais abaixo usa um `ProcessoSeletivo`
  dedicado por padrão exatamente para não depender da correção.

### Achado real corrigido — gate de fase exige a fase INSCRICAO no cronograma (issue #934)

O gate de fase introduzido na Story #916 (PR #931) recusava **todas** as
condições de gatilho existentes com `DocumentoExigido.PontoResolucaoForaDoCronograma`.
Causa: o cronograma de fases da coleção sempre teve só a fase `AVALIACAO`, mas
todo fato do vocabulário (Story #917) tem `PontoResolucao = "INSCRICAO"` — sem
uma fase `INSCRICAO` no cronograma, o gate (corretamente) recusa qualquer
condição que os referencie. **Não é bug de produção** — o gate está
funcionando como projetado (RN08/fail-closed); nenhum edital real teria
cronograma sem fase de inscrição. Corrigido acrescentando uma `FaseCanonica`/
`FaseCronograma` de código `INSCRICAO` (ordem 1, anterior à `AVALIACAO`) ao
Setup e ao cronograma da coleção.

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
