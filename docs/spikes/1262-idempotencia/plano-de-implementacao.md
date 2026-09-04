# Idempotência: política de armazenamento, liberação por tipo de exceção e timeout de requisição

## Contexto

O filtro de idempotência do `uniplus-api` armazena respostas de recusa e retém reservas por 24 h em situações onde nada foi mutado. Um spike de dez provas (branch `spike/1262-idempotencia-provas`, quatro commits, não destinado a merge) mediu o comportamento real contra Postgres efêmero:

- um **403** emitido de dentro da action é gravado e replayado por 24 h, **mesmo depois de a permissão ser concedida** — é a issue #1262;
- um **422 de validação** deixa a entrada em `Processing` por 24 h: um corpo inválido inutiliza a chave;
- o replay de um **401** sai sem `WWW-Authenticate`, justamente o header que sinaliza renovar credencial;
- **discriminator polimórfico ausente** devolve 500 (o desconhecido devolve 400) e também retém a chave;
- exceção lançada **depois que a action retorna não reverte**: a transação fecha em torno da invocação do handler, então ali a mutação já está gravada.

Duas revisões independentes derrubaram desenhos intermediários. A segunda, adversarial, mostrou que a invariante de segurança que eu havia escrito garantia a propriedade errada (reexecução *sequencial*, quando o risco era *concorrente*), e que a frente de "prazo curto de reserva" destruiria o replay legítimo. Ela foi retirada do escopo (#1423, bloqueada) e substituída por liberação **por tipo de exceção**, que resolve os casos medidos sem introduzir concorrência.

**Resultado pretendido:** o filtro deixa de armazenar respostas que não refletem mutação persistida; a chave deixa de ser inutilizada por corpo inválido; e a API passa a ter teto de duração de requisição.

Sem produção — não há restrição de compatibilidade de dados, schema HTTP ou envelope.

## Ordem de execução

Uma issue por PR. A ordem vem da revisão adversarial: o conserto seguro primeiro, a mudança de infraestrutura depois.

| # | Issue | Depende de |
|---|---|---|
| 1 | **#1412** — renomear teste cujo nome afirma o contrário do que exercita | — |
| 2 | **#1410** — corpo com discriminator ausente devolve 400, não 500 | — |
| 3 | **#1409** — exceção que comprovadamente não mutou libera a reserva | #1410 |
| 4 | **#1422** — invariante (a): declaração, ADR e cobertura dos efeitos externos | — |
| 5 | **#1262** — `4xx` produzido dentro do MVC deixa de ser armazenado | #1422, #1409 |
| 6 | **#1411** — `catch` de constraint descarta o rastreamento | — |
| 7 | **#1418** — falha após a reivindicação lança em vez de retornar `Failure` | #1409 |
| 8 | **#1420 (i)** — teto de duração e pipeline, sem tocar contrato | — |
| 9 | **#1420 (ii)** — declaração do `504` e regeneração dos baselines | (i) |
| 10 | **#1420 (iii)** — gates de inicialização | (i) |

**#1423 permanece `blocked`**, e o inventário de (b) sai de #1422 para issue nova, bloqueada junto.

**Por que #1410 antes de #1409, e #1409 antes de #1262** — três razões, todas apuradas na revisão:

1. **#1410 sozinho não libera a chave.** O 400 dele sai do tratador global, fora do MVC; ali o filtro vê exceção pendente e **retém**, pela mesma mecânica descrita na seção do #1262. O tipo que #1410 introduz precisa entrar na lista fechada de #1409 — sem isso, ao fim da rodada o corpo com discriminator errado devolve 400 e **continua inutilizando a chave por 24 h**, que é o defeito colateral que motivou a issue.
2. **#1409 antes de #1262 evita testes que se anulam.** Na ordem inversa, #1262 escreveria a regressão "409 propagado ao tratador global continua retendo" e #1409, no PR seguinte, teria de invertê-la para "libera". Com #1409 primeiro, o teste de #1262 nasce já na forma final.
3. **#1409 não depende de #1422** — ele libera apenas onde está provado que nada foi mutado.

---

## 1. #1412 — renomear o teste

`tests/Unifesspa.UniPlus.Selecao.IntegrationTests/Outbox/Cascading/SessaoEditorialEndpointTests.cs:433-453`

`BadRequestSemIfMatch_ContinuaArmazenado` envia `Idempotency-Key: "chave inválida com espaços"` — malformada, recusada na validação do header **antes de qualquer reserva** — e faz uma única requisição, sem replay. O nome afirma que um 400 de corpo continua cacheado; nada é armazenado, e o defeito está no header.

Renomear para o que verifica (chave malformada recusada com 400) e corrigir o comentário. **Não** escrever a cobertura que o nome antigo anuncia: sob #1262 ela nasceria para ser apagada.

---

## 2. #1410 — corpo com discriminator ausente devolve 400

O desserializador polimórfico lança `NotSupportedException` quando o discriminator `$tipo` está **ausente**; o formatter de JSON só converte `JsonException` em erro de model state. Daí a incoerência: `{"tipo": …}` devolve 500 e `{"$tipo": "inexistente"}` devolve 400.

**Traduzir na fronteira de desserialização, não no tratador global.** Capturar `NotSupportedException` crua no `GlobalExceptionMiddleware` é largo demais — há duas fontes reais dela sem qualquer relação com o corpo da requisição:

- `MotivoDecisaoIsencaoRepository.cs:63` — `throw new NotSupportedException("Motivo de decisão de isenção não é removido…")`, um erro de programação que viraria 400 "formato inválido";
- `CriterioDesempateConfiguration.cs:56` — documenta uma `NotSupportedException` lançada **ao ler o jsonb do banco** se a ordenação de chaves mudar; corrupção de dado viraria "seu corpo está malformado".

Pior que o status: as duas hoje saem por `LogUnhandledError` em `LogLevel.Error` e passariam a logar como falha de cliente — **sumiriam do painel de erro**.

Some-se que o critério de aceite "o corpo do erro identifica o campo e diz qual discriminator era esperado" é **inalcançável do tratador global**, que não tem model state nem informação de campo.

Desenho: envolver ou derivar o `SystemTextJsonInputFormatter` para que a `NotSupportedException` de desserialização vire erro de model state, como já ocorre com `JsonException`. Isso entrega o 400 com o campo de graça, e dá a #1409 um tipo preciso para listar. Se essa rota for recusada em revisão, a alternativa mínima é lançar uma exceção dedicada — `CorpoRequisicaoInvalidoException` — apenas na fronteira de desserialização, e capturar **essa**, nunca `NotSupportedException` crua.

**A ordem dos testes dentro do PR importa.** Escrever **primeiro** o teste de integração contra um endpoint polimórfico real, capturando e afirmando o **tipo concreto** da exceção; só depois os unitários do middleware ou do formatter. Sem isso, os `[Fact]` que injetam a exceção à mão ficam verdes mesmo com a cláusula errada — o spike não registrou o tipo observado, então essa informação ainda não existe.

Isolar as anomalias: o corpo do spike (`{"tipo": "sempre"}`) sobrepõe duas — propriedade errada **e** valor inexistente. Testar separadamente discriminator ausente, objeto vazio, discriminator desconhecido e corpo não-objeto.

Alcança os quatro tipos-raiz polimórficos: `PredicadoObrigatoriedade`, `ArgsCriterioDesempate`, `ArgsRegraAjusteDistribuicao`, `ArgsRegraEliminacao`.

**Atenção a código de erro novo:** `MapeamentoDeDomainErrorTests` varre `Infrastructure.Core` como fonte de **registro**, nunca de **emissão** — uma constante nova sem par no registro passa verde no gate e devolve 500 genérico em runtime.

## 4. #1422 — invariante (a), declarada e coberta

**Primeira ação: dividir a issue em duas.** A #1422 atual carrega duas invariantes com custos muito diferentes. Fica em #1422 a invariante (a) — declaração, ADR e cobertura dos três efeitos externos —, que é o que destrava #1262 e cabe num PR revisável. O inventário dos 79 endpoints e o gate de (b) saem para issue nova, que **não bloqueia** #1262 e é pré-requisito de #1423.

Editar a #1422 para o escopo reduzido, abrir a derivada com o conteúdo de (b) descrito abaixo, e ajustar as referências cruzadas em #1262 e #1423 antes de começar a implementar.

**Documentação.** Emenda datada na ADR-0027 e seção em `CONTRIBUTING.md`, distinguindo:

> **Consolidar as emendas.** Três issues desta rodada (#1422, #1262, #1409) emendam a **mesma** seção da ADR-0027, em PRs sequenciais — conflito de rebase garantido em markdown, mais a linha do índice. Escrever **uma única seção de emenda** no primeiro PR da família e apenas acrescê-la nos seguintes.
>
> A ADR-0027 não tem emenda datada hoje: a única existente é rotulada pela ADR de origem, sem data. O repositório tem dois formatos vivos — seção ao fim e bloco de citação inline. Escolher um e manter. Não usar a ADR-0122 como referência: a emenda recente dela foi editada no lugar, sem marcador nem data.

- **(a) reexecução sequencial** — a requisição roda de novo depois que a anterior terminou. Efeito fora da transação só é admissível se idempotente. É o que #1262 precisa.
- **(b) reexecução concorrente** — duas execuções simultâneas não podem ambas ter sucesso; a exclusão vem de chave natural única ou guarda de estado no agregado, **nunca** da transação ambiente, que isola mas não impede a segunda execução.

**Cobertura de (a)** — três efeitos externos, cada teste junto do endpoint que cobre:

| efeito | propriedade a travar |
|---|---|
| `InstituicaoCacheInvalidator` / `UnidadeCacheInvalidator` | remoção de chave fixa, após o commit; repetir é inócuo |
| cópia selada em `ConfirmarUploadDocumentoEditalCommandHandler` | chave determinística, gravação sobrescrevível |
| URL pré-assinada em `IniciarUploadDocumentoEditalCommandHandler` | não escreve objeto |

### Issue derivada — inventário e gate de (b)

Fora do caminho crítico: não bloqueia #1262, e é pré-requisito de #1423.

São **79 endpoints** com `[RequiresIdempotencyKey]`, em 28 controllers e 4 módulos (Configuracao 41, Selecao 32, OrganizacaoInstitucional 4, Publicacoes 2) — todos method-level.

**Metade do gate já existe, e eu não tinha visto.** Os contratos OpenAPI já são o registro declarado: os 79 aparecem lá com o parâmetro `Idempotency-Key` obrigatório, injetado a partir do próprio atributo. O cruzamento declarado-vs-realidade já roda nos dois sentidos, pelo teste de drift byte-a-byte mais uma regra do linter de contrato no CI. **Endpoint idempotente novo e não declarado já quebra o CI hoje.**

Logo, a entrega de (b) **não é o gate de enumeração** — é a coluna escrita à mão: qual chave natural ou guarda de estado faz a segunda execução concorrente falhar fechado. Nenhum gate verifica isso, e nenhum pode.

Isso também muda a forma. Um roster de 79 linhas escritas à mão seria o primeiro do repositório — todos os registros existentes são derivados por reflexão ou gerados, com a convenção escrita no próprio código ("um módulo novo passa a ser cobrado sem depender de alguém lembrar de listá-lo aqui"). E duas contagens cuidadosas deste mesmo levantamento divergiram entre si, que é exatamente o argumento contra listas manuais. O que precisa ser manual é a **justificativa** por endpoint, não a **enumeração**.

Se ainda assim um gate próprio for feito, o molde do plano estava errado: `IdempotenciaCoHostingTests` exige host, Postgres e migrations, roda no job de integração e **não enxerga o Portal**. O lugar certo é o projeto de testes de arquitetura, que carrega todos os assemblies de API por glob, sem host nem container, no job unitário.

O padrão de enumeração já existe e deve ser reusado, não reinventado:

- `tests/Unifesspa.UniPlus.Host.IntegrationTests/IdempotenciaCoHostingTests.cs:60-71` — enumeração via `IActionDescriptorCollectionProvider`; `:109-111` — predicado `ExigeIdempotencia` (method-level vence, class-level como fallback); `:87-100` — acumular violações numa lista e asserir uma vez, para a falha listar todos de uma vez;
- `tests/Unifesspa.UniPlus.ArchTests/MapeamentoDeDomainErrorTests.cs:44-54` — cruzamento `emitidos.Except(registrados)`; `:105-111` — âncora anti-vacuidade.

**Guard obrigatório:** `cobertos.Should().BeGreaterThan(0)`, no molde de `IdempotenciaCoHostingTests.cs:103`. Sem ele, um erro de enumeração deixa o gate verde sem cobrir nada.

Cruzar nos **dois sentidos**: `Except` de ida pega endpoint novo não declarado; de volta pega rota removida ou renomeada.

**Forma do registro** — o repositório já tem o padrão "registro declarado por módulo + gate que cruza": `IDomainErrorRegistration`, conferida por `MapeamentoDeDomainErrorTests`. Seguir essa forma em vez de inventar arquivo de dados solto, pela mesma razão que a levou a existir: o registro fica junto do módulo que o declara, e o gate falha quando os dois divergem.

O gate assevera **declaração**, não qualidade da proteção — nomear a chave natural ou guarda não prova que ela funciona. O valor está em obrigar quem adiciona endpoint idempotente a responder a pergunta, e em fazer o revisor olhar quando a lista muda. Dizer isso explicitamente na ADR, para o gate não ser lido como garantia que não é.

---

## 5. #1262 — `4xx` deixa de ser armazenado

`src/shared/Unifesspa.UniPlus.Infrastructure.Core/Idempotency/IdempotencyFilter.cs`

Substituir `RespostaDePrecondicao` (`:532-534`) por uma regra: status entre 400 e 499 **libera** a reserva. `412`, `428` e `400-com-If-Match` deixam de ser exceção enumerada e passam a ser instâncias do caso geral. O XML doc de `:503-531` sai junto — o conteúdo migra para a emenda da ADR.

**A regra vale para resposta produzida dentro do MVC.** Um `4xx` escrito pelo tratador global (422 de validação, 409 de concorrência propagada) nunca chega a ser um status que o filtro leia: ali ele vê exceção pendente e retém. Esse caminho é o da #1409.

**O que não muda:** `2xx` continua sendo armazenado; `5xx` continua liberando; o ramo de exceção pendente continua retendo (correto — a mutação pode estar gravada); `SerializeCachedHeaders` **não é tocado** (o `WWW-Authenticate` se dissolve por não haver mais o que replayar).

**Verificado — os erros do próprio filtro não são alcançados pela regra.** `400` de chave ausente ou malformada, `401` de principal, `413` de corpo grande, `422` de divergência de corpo e `409` de reserva concorrente são todos produzidos por short-circuit **antes** do `TryReserveAsync`, com `return` próprio. A regra opera apenas sobre o status posterior ao `next()`, então não há caminho em que o filtro tente liberar uma reserva que nunca criou.

**Testes** — cada um deve falhar sem o fix:

- 403 de dentro da action libera, e a segunda chamada com a permissão concedida devolve 201 (cenário provado no spike);
- 401 de identidade incompleta não é armazenado;
- 406 por `Accept` não suportado libera;
- 409 de concorrência **emitido dentro do MVC** libera;
- 409 de concorrência **propagado ao tratador global** continua retendo — a distinção que impede cobertura de fachada;
- regressão: 412/428/400-com-`If-Match` seguem liberando, agora pela regra geral (`SessaoEditorialEndpointTests`);
- regressão: replays de sucesso verdes, com pelo menos um módulo além de Seleção.

**Emenda na ADR-0027**, na subseção "Status codes em cache", registrando o **critério** — armazena-se a resposta que reflete mutação persistida — e não a classe de status, que é apenas um proxy fiel hoje. Registrar também, como consequência aceita, a perda do `422 body_mismatch` após um `4xx`.

Baselines de contrato **não** são tocados: nenhum status entra ou sai da superfície.

**Verificado — nenhum teste existente quebra.** A varredura por asserções de que `4xx` fica armazenado encontrou apenas `BadRequestSemIfMatch_ContinuaArmazenado`, que a #1412 já renomeia por não exercitar o que o nome diz. O teste do `412` (`SessaoEditorialEndpointTests.cs:139`) assevera o comportamento correto — que ele **não** é cacheado e o retry executa — e continua valendo, agora pela regra geral em vez da exceção enumerada.

---

## 3. #1409 — liberar por tipo de exceção

Mesmo arquivo, ramo de `:233-236`. Lista fechada, com o argumento de segurança escrito junto de cada entrada:

| exceção | por que liberar é seguro |
|---|---|
| `ValidationException` | o middleware de validação roda **antes** do handler; nada foi mutado |
| `DbUpdateConcurrencyException` | vem do `SaveChanges`; a transação ambiente reverte |
| a exceção de corpo inválido introduzida por #1410 | nasce no model binding, antes do corpo da action e de qualquer handler; nenhuma mutação foi tentada |

Qualquer outro tipo **retém**, inclusive exceção de origem desconhecida — foi provado que ali a mutação pode estar gravada. Em particular, `OperationCanceledException` **não entra** na lista: quando o teto de #1420 estourar, a mutação pode já ter sido gravada.

Os handlers que produzem **409 de concorrência dentro do MVC** vivem em Configuração, não em Seleção — `PromoverVersaoTermoConsentimento`, `EditarRascunhoTermoConsentimento`, `MarcarRevisadoTermoConsentimento`, `MarcarVigenteCalendarioDiasUteis`. O teste vai para `Configuracao.IntegrationTests`, o que já satisfaz a exigência de cobrir um módulo além de Seleção.

**Testes:** 422 de validação libera e o retry com corpo corrigido executa; concorrência propagada libera; **exceção de tipo não listado retém** (a regressão que protege o caminho pós-commit).

Emenda na ADR-0027 registrando o critério: libera-se quando o tipo prova ausência de mutação, não por tempo decorrido.

**Atualizar o `CLAUDE.md` do repositório**, na subseção de concorrência otimista. Ele afirma hoje que deixar `DbUpdateConcurrencyException` propagar "corrompe o cache de idempotência, porque `IdempotencyFilter` não verifica `ResourceExecutedContext.Exception` antes de cachear a resposta (issue de correção própria, não resolvida ainda)". Isso está factualmente desatualizado — o filtro **passou a verificar**, e o spike mediu o comportamento atual: a entrada fica em `Processing`, não vira um 200 fabricado. Com esta issue, propagar deixa de reter a chave, e a bifurcação "dois padrões conforme `[RequiresIdempotencyKey]`" da ADR-0119 perde a razão de existir.

Registrar a mudança também como emenda na ADR-0119: a condição que ela própria fixou ("enquanto o gap do `IdempotencyFilter` não for corrigido") deixa de valer aqui — mas **sem** abrir a Opção B em bloco, que continua exigindo o inventário de (b).

---

## 6. #1411 — descartar o rastreamento

Acrescentar `DescartarAlteracoesNaoSalvas()` nos ~12 `catch` de violação de constraint que devolvem `Result.Failure` sem ele (`PrecedenciasFase`, `TiposDocumento`, `TiposBanca`, `ObrigatoriedadesLegais`, `AtosNormativos`, `ProcessosSeletivos`).

**O teste precisa falhar sem o fix, e a técnica já existe no repositório.** Um POST duplicado sequencial não serve — é barrado pela verificação prévia. Exercitar o handler duas vezes sobre o mesmo contexto **também não**, pela mesma razão.

O molde canônico é `tests/Unifesspa.UniPlus.Configuracao.IntegrationTests/CategoriasDocumento/CategoriaDocumentoCorridaDeUnicidadeTests.cs:46-88`: semeia a linha conflitante num contexto separado, injeta um repositório cuja consulta de unicidade devolve `false` fixo, chama o handler direto, e a asserção decisiva é que o `SaveChangesAsync` **posterior** ao retorno do handler não lança. Há mais dois exemplares em `TiposDeficiencia` e em `Publicacoes`.

**Sobre o ramo morto: medir antes de afirmar.** Eu havia registrado que `IsHashConflict` é inalcançável, porque o `RegraCodigo` entra no cálculo do hash. O raciocínio está certo, a conclusão provavelmente não: como toda violação de hash é também violação de `regra_codigo`, quem reporta é o índice verificado primeiro — e na migration inicial o índice de **hash** é criado **antes** do de `regra_codigo`. A leitura mais provável é a inversa.

Antes de qualquer afirmação no PR, acrescentar asserção de `ConstraintName` a `ObrigatoriedadeLegalPersistenceTests.cs:127` (que hoje viola as duas constraints de uma vez e assevera só o tipo da exceção) e usar regras que violem **uma** por vez. Remover o ramo que a medição indicar — não o que eu supus.

**Alcance real:** 12 blocos, 14 pontos de `Result.Failure`, em **três** módulos (Configuração 4, Publicações 2, Seleção 6). Organização Institucional não entra — o `catch` dela já descarta. Os quatro de `ProcessosSeletivos` exigem processo publicado com envelope congelado, setup caro. Considerar um PR por módulo.

Dois pontos para o corpo do PR, não para o código: `MarcarVigenteCalendarioDiasUteisCommandHandler.cs:61` **não** entra na lista — não descarta de propósito, com justificativa escrita, e ali o descarte seria inócuo; e `RegistrarAtoNormativo` já está neutralizado a jusante por um `Clear()` no handler de requisição, então a correção nele é defesa em profundidade, não 500 observável hoje.

---

## 8. #1420 — timeout de requisição, em três fatias

O escopo original — teto, pipeline nos dois hosts, transformer, cinco baselines, dois gates de inicialização, documentação e issue no frontend — é grande demais para um PR. Dividir:

**(i) Teto e pipeline.** `AddRequestTimeouts` com política padrão global e políticas nomeadas, `UseRequestTimeouts` nos **dois** hosts. Sem tocar contrato. Teste de integração provando que o estouro produz **504 e não 500**.

A ordem no pipeline decide isso: `GlobalExceptionMiddleware` é registrado cedo, então `UseRequestTimeouts()` precisa entrar **depois** dele — entre `UseAuthorization()` e `MapControllers()`. Antes, o tratador global captura a `OperationCanceledException` no ramo genérico e escreve 500. **Não** acrescentar `UseRouting()` explícito: nenhum host o chama, o roteamento é auto-inserido no início, e movê-lo mudaria a relação do CORS com o endpoint resolvido.

**Consequência de face para o cliente, a registrar e testar:** o estouro cancela o token, a `OperationCanceledException` sobe por `next()` e o filtro **retém a reserva**. Quem estourar o teto e retentar com a mesma chave recebe `409 processing_conflict` por 24 h. É defensável — a mutação pode ter sido gravada —, mas precisa de teste que fixe o comportamento e de uma linha na emenda. É também o argumento mais forte para #1423 seguir bloqueada.

**(ii) Declaração do `504`.** O mecanismo existe: `AuthorizationOperationTransformer` é o molde — injeta 401/403 em massa com referência ao esquema de erro e respeita quem já declarou.

Duas armadilhas apuradas: o transformer principal roda **primeiro** e é ele que coage 4xx/5xx para `application/problem+json`, então um transformer registrado depois precisa setar o media type explicitamente; e o molde pula minimal APIs — são **10 das 184 operações**, e o baseline de Ingresso tem **só** essas duas, de modo que ele **não mudaria em nada**. Sem tratar isso, o critério "504 em todas as operações" fica descumprido e um baseline sem diff parecerá erro de execução.

**Validar o diff por script, não por leitura.** São ~1.840 linhas adicionadas em 184 operações. O diff é estritamente aditivo (o normalizador não reordena chaves). Afirmar por script que **toda** operação ganhou exatamente um `504` e que **nada mais** mudou — comparar os dois JSON com o `504` removido de cada bloco de respostas, que devem ficar idênticos — e colar a saída no corpo do PR.

**(iii) Gates de inicialização.** `DisableRequestTimeout` combinado com `[RequiresIdempotencyKey]` falha no boot; política nomeada e não registrada falha no boot, não no primeiro acesso.

**Nota para a ADR:** `AddRequestTimeouts` cancela um token; não encerra trabalho que ignore o `CancellationToken`. É o que limita o que o teto garante.

**Fora do escopo deste repositório, sem dono:** o Geo foi extraído para `unifesspa-geo-api` e entra como imagem, atrás do mesmo gateway — fica sem teto, e o comportamento do gateway fica assimétrico. Precisa de issue gêmea lá ou de nota explícita. E o Portal **não tem baseline de contrato**, então o 504 dele não é verificável por contrato — hoje ele expõe um único controller, o que torna isso aceitável, mas deve ser dito.

## Padrão de execução por issue

Via: `/issue-driven-implementation <issue>`, uma issue por vez.

**Worktree própria, sempre** — `git fetch origin && git worktree add ../uniplus-api-wt-<issue> origin/main -b fix/<issue>-<slug>`. Nunca trabalhar no checkout principal, que costuma estar ocupado por outra sessão.

O `fetch` não é decorativo: são dez PRs sequenciais, três deles tocando o mesmo arquivo do filtro. Um `origin/main` velho ramifica silenciosamente de antes do merge anterior.

**Gates locais antes de todo push** (comandos do `ci.yml`, não do `CONTRIBUTING.md`, que documenta dois quebrados):

```bash
~/.dotnet/dotnet restore UniPlus.slnx --locked-mode
~/.dotnet/dotnet format UniPlus.slnx --verify-no-changes --no-restore --exclude-diagnostics CA1515
~/.dotnet/dotnet build UniPlus.slnx --configuration Release --no-restore
~/.dotnet/dotnet test UniPlus.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName!~IntegrationTests"
~/.dotnet/dotnet test UniPlus.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~IntegrationTests"
bash tools/adr-lint/validate.sh && npx --yes markdownlint-cli2@0.22.1 'docs/adrs/**/*.md'
bash tools/forbidden-deps/check.sh
```

`dotnet test UniPlus.slnx` **sem filtro pula os 17 projetos unitários em silêncio** — rodar os dois filtros sempre.

**Ciclo de PR, sem esperar o usuário:**

1. `gh auth switch --user marmota-alpina` **antes de qualquer push** — push com a conta de review invalida a aprovação dela e trava o merge;
2. `gh pr create` com o template preenchido e `Closes #N` em texto plano;
3. `/home/jeferson/Projects/workspaces/uniplus/.claude/hooks/pr-watch.sh mark <pr>` **imediatamente após o push** — marcar tarde esconde reação já registrada;
4. agir nos três desfechos (achado novo, CI vermelho, 👍) sem pedir instrução; corrigir, responder **e resolver** a thread; rearmar após cada push;
5. antes de aprovar, consolidar commits de revisão com `git reset --soft` + recommit, provando com `git diff <sha antigo> HEAD` vazio — rebase merge leva cada commit para a main;
6. aprovar com `jf2s`, voltar para `marmota-alpina`, mergear com `--rebase`;
7. `update-branch` redispara a análise: conferir threads não resolvidas entre a aprovação e o merge.

**Qualidade:** SOLID, Clean Code, DRY. Comentário só quando explica o que o código não consegue dizer — nada de narrar mudança, processo de revisão ou ferramenta. Todo teste escrito para uma correção **tem de falhar sem ela**. Zero tolerância a findings: bloqueante, importante e sugestão se corrigem antes do merge.

O workspace já mecaniza parte disso, e os hooks devem ser tratados como parte do fluxo, não como ruído:

| hook | o que faz |
|---|---|
| `revisao-antes-do-commit.sh` | inspeciona o diff em stage procurando duplicação, comentário supérfluo e acoplamento alto; devolve o achado como pergunta |
| `guard-git.sh` | bloqueia commit na `main` e qualquer atribuição de IA em artefato Git |
| `gate-pr-watch.sh` | impede encerrar o turno com ciclo de revisão aberto |
| `wait-review.sh` | reinvoca o agente quando a revisão chega, em vez de deixar em polling |

Um achado do hook de revisão é sinal para reexaminar o diff, não para contornar.

**O `CONTRIBUTING.md` não tem seção de idempotência, timeout nem concorrência — nenhuma das três.** Onde o padrão de handler é ensinado, a regra de concorrência otimista não aparece; ela vive só no `CLAUDE.md`, que por convenção não pode ser citado como autoridade para desenvolvedores. São seções a **criar**, não a atualizar — orçar o trabalho nos PRs de #1422 e #1420(i), não tratá-lo como retoque.

**Commits:** conventional commits em pt-BR, indicativo presente 3ª pessoa, descrevendo a mudança no código — nunca o processo. Sem `Co-Authored-By`, sem `--no-verify`.

## Pontos de parada — decisão do líder técnico, não minha

A revisão adversarial do plano isolou oito. Três já estão resolvidos; os cinco restantes ficam registrados aqui e devem ser levados antes de o PR correspondente começar.

**Resolvidos:** fatiar #1422 em (a) e (b) — decidido; escopo da rodada — decidido; merge autônomo — decidido.

**Em aberto:**

1. **Declarar `504` enquanto o `500` continua não declarado.** Nenhum dos cinco baselines declara qualquer `5xx` hoje. Declarar o 504 em todas as operações e deixar o 500 invisível é a mesma incoerência que #1410 aponta. O dado não estava à mesa quando a decisão foi tomada.
2. **O valor de 30 s** colide exatamente com o timeout do cliente de registro de esquemas. Um teto externo igual ao maior timeout interno mascara o erro específico com um 504 genérico — precisa ser estritamente maior, ou escolher outro valor.
3. **Mudar o desenho de #1410** do tratador global para a fronteira de desserialização. É o que torna alcançável o critério de aceite que a issue já declara, mas muda a proposta escrita nela.
4. **Roster escrito à mão em #1422(b)** contraria convenção explícita do repositório, e o gate de enumeração que ele traria já existe via contratos. Só a justificativa por endpoint é trabalho novo.
5. **O teto do Geo**, em `unifesspa-geo-api` — outro repositório, sem dono neste plano.

Além desses, dois pontos onde a decisão é técnica mas não deve ser solo: **remover o ramo de colisão em #1411** (só após a medição de `ConstraintName`, e removendo o que a medição indicar) e **remover a guarda de `Confirmar(...)` em #1418** (retirar guarda de domínio não é decisão de quem implementa).

## Verificação

Ao fim de cada PR: CI verde nos jobs `Build`, `Unit + arch tests`, `Integration tests`, `ADR lint`, `Spectral`, `Formatação`, `forbidden-deps`; zero threads abertas; issue fechada pelo `Closes`.

Ao fim da rodada, verificação de ponta a ponta contra o comportamento medido no spike — os testes do spike servem de roteiro, adaptados como cobertura permanente:

1. 403 de dentro da action, permissão concedida, mesma chave → **201** (hoje: 403 replayado);
2. 422 de validação, corpo corrigido, mesma chave → **executa** (hoje: 409 por 24 h);
3. `{"tipo": …}` num campo polimórfico → **400** (hoje: 500);
4. replay de sucesso → inalterado, com `Idempotency-Replayed: true`;
5. corpo com discriminator ausente → **400 e a chave liberada** (hoje: 500 e chave retida por 24 h) — o par #1410 + #1409 só fecha se o tipo entrar na lista;
6. requisição que excede o teto → **504** com corpo de contrato (hoje: sem teto), e a chave **retida**, com o comportamento fixado por teste.

A worktree do spike (`../uniplus-api-wt-spike-1262`) é removida ao final; a branch fica como registro da investigação, sem merge.
