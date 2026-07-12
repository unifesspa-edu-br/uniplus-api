---
status: "accepted"
date: "2026-07-10"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
  - "P.O. CRCA"
informed:
  - "Equipe Uni+"
---

# ADR-0106: Publicar um Edital registra o ato em Publicações de forma síncrona, antes de concluir

> **Superseded no mecanismo pela [ADR-0108](0108-registro-do-ato-por-mensagem-duravel.md).** A chamada síncrona
> in-process decidida aqui não foi implementada: a [ADR-0107](0107-vaga-de-linhagem-unica-por-objeto.md),
> posterior, tornou a vaga de linhagem monotônica, e com ela o "ato órfão" que esta ADR aceitava como resíduo
> passou a deixar o certame **impublicável em definitivo**. A alternativa atômica é inviável no pipeline do
> Wolverine, que além disso desaconselha explicitamente a chamada síncrona entre handlers. O registro do ato
> passa a viajar por mensagem durável no outbox. O que esta ADR decidiu sobre *ownership* e sobre a assimetria
> de dependência entre os módulos **permanece vigente**.

## Contexto e enunciado do problema

A ADR-0105 decidiu que o módulo `Publicacoes` é o registro central dos atos normativos, e que os domínios (Seleção, Ingresso) o referenciam por valor, sem que `Publicacoes` conheça `ProcessoSeletivo`/`Chamada`. O que a ADR-0105 não decidiu — porque ainda não existia o problema concreto — é **como** um domínio aciona esse registro no instante em que publica.

Hoje, publicar um `ProcessoSeletivo` (`PublicarProcessoSeletivoCommandHandler`) não toca `Publicacoes` em nenhum momento: cria um `Edital` interno ao módulo Seleção, congela um `SnapshotPublicacao`, e para. Registrar o ato correspondente em `Publicacoes` seria um segundo passo manual, numa tela separada — o que reintroduz exatamente o retrabalho que a concentração do registro em `Publicacoes` deveria eliminar, e quebra a rastreabilidade "quais atos pertencem a este processo" que é a razão de existir do módulo.

A proposta avaliada: a publicação em `Publicacoes` deveria ser **automática**, disparada pela própria publicação do Edital — sem passo manual, e case a caso trivialmente extensível para `Chamada` (Ingresso) quando esse módulo ganhar camada `Application`.

Duas hipóteses de mecanismo foram descartadas por evidência, não por preferência — registro abaixo porque a próxima pessoa que revisitar esta decisão vai fazer as mesmas perguntas:

1. **Evento de domínio assíncrono, disparado depois do commit de Seleção** (fire-and-forget pós-`Publicar()`). Falha em dois pontos verificados no código: (a) a ADR-0104 (linha 59) já decide que **"o ato é gravado primeiro e a versão depois"** — um mecanismo que publica o Edital e só depois tenta registrar o ato inverte essa ordem; (b) a garantia de entrega que esse mecanismo precisaria (retry, dead-letter, reconciliação) está **documentada na ADR-0004 mas não implementada em nenhum lugar do código** — não há `OnException<T>().RetryTimes(N)` nem `DurabilitySettings.DeadLetterQueueExpirationEnabled` configurados. Construir sobre essa garantia seria construir sobre uma promessa não cumprida.
2. **`ICommandBus.Send` como se fosse mensagem cascading.** Verificado no código: `WolverineCommandBus.Send` chama `bus.InvokeAsync`, que é despacho síncrono in-process — não passa pelo outbox, não herda a durabilidade do outbox nem tem retry automático de transporte. Isso não invalida `ICommandBus.Send` como ferramenta — define para que serve: é o mecanismo certo para uma chamada síncrona bloqueante entre módulos, que é exatamente o que a ordem da ADR-0104 exige.

## Drivers da decisão

- **ADR-0104**, já aceita: o ato é gravado primeiro, a versão de configuração depois — sem ciclo de inserção.
- **ADR-0105**, já aceita: `Publicacoes` nunca depende de um domínio; os domínios podem depender de `Publicacoes`.
- **Ausência de infraestrutura de retry/DLQ configurada** torna qualquer mecanismo assíncrono, hoje, uma promessa de confiabilidade que o código não cumpre — descoberto ao verificar a ADR-0004 contra o código real, não assumido.
- **Precedente vivo**: o padrão `{Module}.Contracts` (hoje `Configuracao.Contracts`, consumido por `Selecao.Application`) já é o mecanismo aprovado para uma chamada síncrona cross-módulo, e o fitness test R8 já o valida automaticamente — sem exigir mudança no teste.
- **Evitar retrabalho operacional** (motivação original da proposta): a operação de publicar deve continuar sendo **um clique**, sem tela adicional em `Publicacoes`.

## Opções consideradas

- **A**: Escrita síncrona na mesma transação ADO.NET, compartilhada entre `SelecaoDbContext` e `PublicacoesDbContext`.
- **B**: Escrita síncrona, mesmo request, duas transações separadas, mas **sem ordem obrigatória** (Selecao escreve, depois tenta Publicações, ignora falha).
- **C**: Assíncrono via cascading message / outbox — Selecao publica um evento, Publicações reage depois.
- **D (escolhida)**: Escrita síncrona, mesmo request, duas transações separadas, com **ordem obrigatória**: `Publicacoes` é chamada e confirma **antes** de qualquer escrita do lado Seleção.

## Resultado da decisão

**Escolhida: "D — orquestração síncrona, ato primeiro", com a chamada mediada por uma porta própria de Seleção.**

`PublicarProcessoSeletivoCommandHandler` não deve depender diretamente de `Publicacoes.Contracts` — um acoplamento handler-a-handler faria o handler crescer a cada nova necessidade síncrona no fluxo, não só o registro do ato. A chamada a Publicações é mediada por uma porta própria de Seleção:

- `Selecao.Application.Abstractions` define a porta — `IRegistroDeAtoNormativo.RegistrarAsync(...)` — em vocabulário só de Seleção (nenhum tipo de `Publicacoes.Contracts` atravessa a porta; o retorno é `RegistroAtoNormativoResultado(AtoId, AtoHash, InstantePublicacao)`, um record de Seleção, não o DTO de Publicações).
- `Selecao.Infrastructure/ExternalServices` implementa `RegistroDeAtoNormativoAdapter` — a ÚNICA classe de Seleção que conhece a forma de `RegistrarAtoNormativoCommand` e faz `ICommandBus.Send`. Mesmo padrão de `DocumentoEditalStorageService`, na mesma pasta: adapter público, registrado via `AddScoped<IRegistroDeAtoNormativo, RegistroDeAtoNormativoAdapter>()`.
- `PublicarProcessoSeletivoCommandHandler` (e, simetricamente, `RetificarProcessoSeletivoCommandHandler`) dependem só de `IRegistroDeAtoNormativo`. Zero referência a `Publicacoes.Contracts` na Application — a referência ao projeto migrou para `Selecao.Infrastructure`.

Qualquer coisa **além** desse registro sob a ordem "ato primeiro" (uma segunda validação síncrona, um segundo sistema a notificar antes de prosseguir) muda o adapter, nunca o handler. Consumidores que **não** precisam de resposta síncrona (notificação, indexação, auditoria) já têm canal próprio, sem tocar este handler: `ProcessoSeletivo` já emite `ProcessoPublicadoEvent` via cascading message (ADR-0005), drenado depois do `SaveChanges` — qualquer módulo futuro assina esse evento existente.

Sequência do handler, antes de qualquer chamada ao agregado `ProcessoSeletivo`:

1. **Pré-checar, sem tocar Publicações, o que o agregado também vai validar.** `Status == Rascunho` (senão `TransicaoInvalida`) e `AvaliarConformidade()` sem pendências (senão `ConformidadeInsuficiente`) — ambas checagens puras, sem mutação, hoje já nas duas primeiras linhas de `ProcessoSeletivo.Publicar()` (`ProcessoSeletivo.cs`, linhas 436-449). Uma pré-checagem redundante roda ANTES do registro em Publicações: publicar um processo não-conforme ou já publicado sem essa pré-checagem registraria um ato em Publicações e só depois falharia dentro de `Publicar()` — órfão determinístico, não um caso raro de corrida concorrente, toda vez que a validação reprovar (situação comum quando o usuário tenta publicar antes de terminar a configuração). `Publicar()` mantém as mesmas checagens internamente — a pré-checagem não as substitui nem as remove do agregado: é o agregado, não uma checagem externa opcional, quem continua garantindo a invariante para qualquer chamador (outro handler, teste, caminho futuro). A pré-checagem existe só para não gastar uma chamada a Publicações num caso que o agregado já sabe, de antemão, que vai recusar.
2. Montar os primitivos do registro — `IdempotencyKey`, `TipoCodigo`, `NumeroDeclarado`, `DocumentoHash`, `AtoRetificadoId?`, `MotivoRetificacao?`, o vínculo genérico da ADR-0105 (`EntidadeTipo`/`EntidadeId`, `"ProcessoSeletivo"`/`processo.Id`), e `HashDoEfeito` (hash de `canonico.Bytes` + `documento.HashSha256` + `dados`, ver Idempotência) — e chamar `registroDeAtoNormativo.RegistrarAsync(...)`. A ADR-0105 já define a consulta unificada "todos os atos deste certame" sobre um vínculo genérico `(ato, tipo_entidade, entidade_id)` populado pelo domínio (rótulo opaco, sem FK — Story #801). Sem `EntidadeTipo`/`EntidadeId` no comando de registro, a publicação automática criaria o ato mas nunca o vincularia ao processo — quebrando a rastreabilidade que motiva esta ADR. `Publicacoes` persiste `HashDoEfeito` junto com o registro, para comparar em replays futuros por chave. Ambos os campos são gravados no MESMO registro do ato, não como escrita separada.
3. Injetar `ICommandBus` cru no handler exigiria opt-in de codegen (`WolverineCommandBus` é `internal`, `ServiceLocationPolicy.NotAllowed`, ADR-0098). Extrair a porta não elimina essa exigência: o codegen do Wolverine recursa a árvore de dependências ao tentar inline-construir a chain, e como o construtor do adapter pede `ICommandBus`, a mesma restrição reaparece um nível abaixo. O opt-in é declarado na porta (`AlwaysUseServiceLocationFor<IRegistroDeAtoNormativo>()`, em `SelecaoCodegenRegistration`) — mais estreito e intencional que opinar em `ICommandBus` diretamente, que liberaria service location de graça para qualquer handler futuro que injete `ICommandBus` cru por outro motivo. Sem esse opt-in, `Host.IntegrationTests` (fitness test do próprio ADR-0098) e todo teste de `Selecao.IntegrationTests` que exercita `Publicar()`/`Retificar()` pela pipeline HTTP real quebram com `InvalidServiceLocationException`. A implementação real (#799 em diante) inclui esse opt-in desde o primeiro commit.
4. Se a resposta for `Result.Failure`, o handler de Seleção devolve a falha imediatamente. **Nada é persistido do lado Seleção.** É o mesmo padrão de UX que `ConformidadeInsuficiente` já usa hoje — o usuário vê o motivo, corrige, tenta de novo.
5. Se a resposta for `Result.Success` com `{AtoId, AtoHash, InstantePublicacao}`, o handler chama `processo.Publicar(...)` (assinatura ganha os três novos parâmetros) — que valida `TransicaoInvalida`/`ConformidadeInsuficiente` de novo (a mesma checagem do passo 1, agora dentro do agregado) e, se passar, grava o `Edital`/`SnapshotPublicacao` referenciando o ato **por valor** (ADR-0061), sem FK. A segunda checagem é redundante no caminho feliz — nada muda entre o passo 1 e o passo 5 na ausência de concorrência — e intencional: é o agregado, não a orquestração, quem é responsável por nunca publicar um processo inválido, para qualquer chamador. `Edital.EmitirAbertura`/`EmitirRetificacao` e `SnapshotPublicacao.Congelar` recebem `InstantePublicacao` — o instante que `Publicacoes` já comitou — em vez de derivar um novo instante de `TimeProvider.GetUtcNow()` na hora da chamada: num retry após uma falha 5xx anterior, o instante de commit em Publicações não muda, mas um `GetUtcNow()` fresco mudaria, fazendo `Edital.DataPublicacao` divergir do instante que Publicações associa ao mesmo ato — risco concreto perto de virada de dia.
6. `SaveChanges` do lado Seleção conclui a operação.

Mesmo raciocínio de ordem se aplica a qualquer outra checagem de negócio que hoje viva dentro de `Publicar()`/`Retificar()` e não dependa do ato: se é validação pura (sem mutação, sem precisar do `AtoId`), a pré-checagem do passo 1 também a cobre, mas o agregado continua validando por conta própria — o passo 1 evita uma chamada desnecessária a Publicações, não substitui a validação de `Publicar()`.

`Selecao.Infrastructure` referencia `Publicacoes.Contracts` — não `Selecao.Application`. **Nada em `Publicacoes` referencia `Selecao`** — a assimetria da ADR-0105 permanece intacta, e o fitness test R8 comprova isso automaticamente (verificado no spike #819: R8 permanece verde com o `ProjectReference` agora em Infrastructure, porque `.Contracts` não está na lista de namespaces bloqueados em nenhuma camada).

### Idempotência

Não é herdada de outbox — este mecanismo não usa outbox. O risco real é outro: o handler de Seleção comitar 1 (chamar Publicações com sucesso) e falhar em 2 (o próprio `SaveChanges` de Seleção), levando o chamador HTTP a repetir a operação inteira. Como `ProcessoSeletivo.Status` só muda para `Publicado` no passo 5, a expectativa ingênua é que um retry reexecute o passo 1 e chame `Publicacoes` de novo — **isso só é verdade para falhas 5xx**, não em geral (ver ressalva abaixo).

Chave de correlação: **reaproveitar o `Idempotency-Key` HTTP já exigido** em `POST .../publicacao` e `POST .../retificacoes` (`[RequiresIdempotencyKey]`, confirmado nos dois endpoints) — mas **não o valor cru do header sozinho**. A store HTTP existente (`IdempotencyEntryConfiguration.cs`) indexa por `(Scope, Endpoint, IdempotencyKey)`, não só a chave: `Scope` = `user:{UserId}` (`IdempotencyFilter.ResolveScope`), `Endpoint` = `{METHOD} {path-concreto}`, incluindo o id do recurso, não o template (`IdempotencyFilter.ResolveEndpoint`). Propagar só o header cru para o índice único de `Publicacoes` reintroduziria exatamente a colisão que esse desenho evita hoje: dois usuários diferentes — ou o mesmo usuário publicando dois processos distintos — que (coincidentemente) usem o mesmo valor de `Idempotency-Key` colidiriam no índice de `Publicacoes`, e o segundo receberia de volta o `AtoId` do primeiro, de um ato não relacionado.

A identidade propagada como `RegistrarAtoNormativoCommand.IdempotencyKey` precisa carregar os mesmos três discriminadores — por exemplo, `{Scope}:{Endpoint}:{IdempotencyKey}` — não o header isolado. Quem compõe esse valor é o controller (camada HTTP, onde `Scope`/`Endpoint` já são resolvidos pelo filter), não o handler nem o adapter; `PublicarProcessoSeletivoCommand.IdempotencyKey` recebe o composto já pronto.

`Publicacoes` grava um índice único sobre esse valor composto — mas replay por chave sozinha, sem comparar o payload, reabre exatamente o problema que a store HTTP já resolve via `BodyHash` (`IdempotencyDomainErrorCodes.BodyMismatch`, hit-mismatch → 422): se o registro original falhar do lado Seleção com um 5xx (reservation HTTP é deletada — ver ressalva abaixo — cliente pode legitimamente reenviar com o MESMO `Idempotency-Key` mas dados corrigidos), `Publicacoes` devolveria o `AtoId` do registro ANTERIOR, criado para um payload diferente — Seleção persistiria um Edital referenciando, por valor, um ato que prova outra coisa.

Um hash canônico enumerando campos (`TipoCodigo`, `NumeroDeclarado`, `DocumentoHash`, ...) tem o mesmo risco de ficar desatualizado que qualquer lista replicada, e **reaproveitar o `BodyHash` HTTP também não basta**: `BodyHash` cobre só os campos que o CLIENTE enviou no corpo da requisição — não cobre o `SnapshotCanonico` que `canonicalizer.Canonicalizar(processo, dados, documento.HashSha256!)` deriva do **estado vivo do agregado** (`PublicarProcessoSeletivoCommandHandler.cs`, linha 91), computado DEPOIS que o corpo HTTP chega mas ANTES de chamar Publicações. Se a configuração do processo mudar (outro request altera etapas/vagas) entre uma tentativa que falhou com 5xx e o retry do cliente com o MESMO corpo, `BodyHash` continua batendo, mas o `SnapshotCanonico` — e portanto o efeito realmente publicado — é diferente; `Publicacoes` replayaria o `AtoId` antigo para um snapshot novo, e Seleção persistiria esse snapshot referenciando, por valor, um ato que não prova o que foi publicado.

Mais robusto: o valor a comparar não é derivado da requisição HTTP (nem campo a campo, nem `BodyHash`) — é o `HashDoEfeito` (ver passo 2 da sequência), computado pelo próprio handler sobre `canonico.Bytes` (+ `documento.HashSha256`, `dados`), no instante em que os dados que serão persistidos já estão materializados, imediatamente antes de chamar `RegistrarAsync`. Independente do hash que `SnapshotPublicacao.Congelar` deriva internamente depois (ADR-0100, outro propósito) — `HashDoEfeito` é calculado só para a comparação de idempotência cross-módulo, viaja no comando de registro e é persistido por `Publicacoes` junto com o ato, e cobre por construção qualquer coisa que acabe no efeito publicado, incluindo estado do agregado que a requisição HTTP nunca carregou.

**Ressalva sobre o retry natural "recuperar reexecutando o passo 1":** só é garantido quando a falha de Seleção em 2 é 5xx. `IdempotencyFilter` só deleta a reservation HTTP (permitindo reexecução real do handler) para `status >= 500 || Canceled` — uma falha de domínio traduzida em 422 (ex.: corrida concorrente de publicação, `UniqueConstraintViolation` → `Edital.AberturaJaExiste`, já tratada em `PublicarProcessoSeletivoCommandHandler`) é **cacheada como resposta final**. Um cliente que perdeu essa corrida — cujo ato em `Publicacoes` já foi criado com sucesso antes de a escrita em Seleção falhar — reenviando a mesma `Idempotency-Key` recebe o 422 **replayed do cache**, sem o handler rodar de novo, sem nova chamada a `Publicacoes`. O ato fica órfão **permanentemente**, não só "até o próximo retry" — esta ADR não resolve esse gap; a implementação real (#799 em diante) precisa decidir entre (a) tratar essas falhas pós-registro-do-ato como não-cacheáveis (mesmo tratamento do 5xx), ou (b) reconciliação/detecção de órfãos.

## Consequências

### Positivas

- Respeita a ordem já decidida na ADR-0104 por construção — não por disciplina de quem escreve o handler.
- `PublicarProcessoSeletivoCommandHandler` não cresce a cada novo consumidor síncrono: a porta (`IRegistroDeAtoNormativo`) é o único ponto de acoplamento direto, e existe porque a ordem "ato primeiro" estruturalmente exige uma resposta (`AtoId`/`AtoHash`) antes de prosseguir — pub/sub não devolve valor a quem publica, então isso não é uma escolha de estilo, é inerente a precisar do id de volta. Qualquer mudança nesse registro (novo campo, segunda validação síncrona) muda o adapter em Infrastructure, nunca o handler. Consumidores que não precisam de resposta síncrona usam o `ProcessoPublicadoEvent` que o handler já emite hoje (ADR-0005) — zero mudança no handler para eles.
- Zero infraestrutura nova: reaproveita `ICommandBus`, o padrão `.Contracts`, o `Idempotency-Key` HTTP já existente. Nenhuma fila, nenhum tópico, nenhuma política de retry a inventar.
- **Falha em `Publicacoes` nunca deixa Seleção num estado intermediário** — essa metade da garantia é airtight (CA-03, comprovada por teste): recusa em Publicações → zero escrita em Seleção. A garantia **não é simétrica** na outra direção: se `Publicacoes` tiver sucesso e a escrita de Seleção falhar depois, o ato fica registrado sem o `Edital` correspondente — recuperável por retry do cliente só quando essa falha for 5xx (busts a reservation HTTP); falhas de domínio traduzidas em 422 (corrida concorrente, por exemplo) são cacheadas e o retry nem chega a reexecutar o handler, deixando o ato órfão permanentemente sem reconciliação (ver Idempotência, ressalva). Não é "publica tudo, ou não publica nada" — é "nunca publica parte de Seleção sem Publicações confirmado antes", uma garantia mais estreita, e o gap do lado inverso é uma decisão explícita para #820, não resolvida por esta ADR.
- O mecanismo generaliza para `Ingresso`/`Chamada` sem mudança de desenho: quando o módulo ganhar camada `Application`, seu handler de publicação faz a mesma chamada síncrona ao mesmo `Publicacoes.Contracts`.
- Não reabre o debate de transporte cross-módulo (PG queue vs. Kafka, ADR-0001/0044/0056) — não é roteamento de evento, é a mesma chamada síncrona in-process que o padrão `Reader` já legitima para leitura, agora para escrita.

### Negativas

- **Acopla disponibilidade**: se `Publicacoes` estiver indisponível ou rejeitando por bug, a publicação de Editais em Seleção para junto. É uma dependência síncrona nova no caminho crítico de um módulo de domínio, coisa que a ADR-0105 evitou no plano de *ownership* mas este ADR reintroduz no plano de *disponibilidade*. Aceito conscientemente: hoje não existe infraestrutura de retry/DLQ que tornasse a alternativa assíncrona genuinamente mais segura — o trade-off é entre "acoplar disponibilidade" e "arriscar inconsistência silenciosa sem rede de segurança". Se/quando a ADR-0004 for cumprida de fato (retry+DLQ configurados, alertas, reconciliação), este ADR pode ser revisitado.
- `Publicacoes.Contracts` passa a expor um comando de escrita, não só leitura — extensão do padrão estabelecido pela ADR-0056, que precisa ser lida como tal.
- **Órfão permanente em corrida concorrente perdida**: quando a escrita de Seleção falha com um 422 de domínio (não 5xx) depois de `Publicacoes` já ter registrado o ato, o cliente que perdeu a corrida não tem recuperação automática — a resposta é cacheada pelo `IdempotencyFilter` e o retry não reexecuta nada (ver Idempotência, ressalva). Esta ADR não resolve o gap; fica decisão explícita de #820 escolher entre tornar essas falhas não-cacheáveis ou implementar reconciliação de órfãos.
- **`Publicacoes` precisa comparar payload, não só chave, antes de devolver um ato existente por replay** — sem isso, uma chave reenviada com dados diferentes (após um 5xx legítimo do lado Seleção) devolveria o ato do registro anterior, e Seleção persistiria um Edital referenciando, por valor, um ato que prova outra coisa. Mesma disciplina do `BodyHash` já usado pela store HTTP, replicada no nível do comando cross-módulo — não implementado neste ADR, requisito para #820.
- Constrói sobre o modelo atual (`Edital` interno a `ProcessoSeletivo`), que a própria ADR-0105 já decidiu ser transitório — quando #802/#803/#804 substituírem `Edital` por `VersaoConfiguracao`, este mecanismo de chamada sai do lugar (dentro de `ProcessoSeletivo.Publicar`) e entra no handler que cria `VersaoConfiguracao`, mas a FORMA da orquestração (síncrono, ato primeiro) não muda.

### Neutras

- `Ingresso` fica de fora da primeira entrega — não tem camada `Application` hoje. O contrato em `Publicacoes.Contracts` já nasce genérico o bastante para servir aos dois domínios; o adaptador de Ingresso é sequenciado depois, sem redesenho.
- O documento físico do Edital continua fisicamente sob a chave de storage de Seleção; `Publicacoes` registra o **hash**, não o binário — não há decisão de custódia de arquivo a tomar aqui.

## Confirmação

- **Fitness test R8** (`CrossModuleReadIsolationTests`) permanece verde com `Selecao.Infrastructure` → `Publicacoes.Contracts`: comprovado no spike #819, sem alteração no teste. A regra cobre as quatro camadas (Domain/Application/Infrastructure/API) — mover a referência de Application para Infrastructure não muda o veredito.
- **Teste de ordem**: handler de Publicações precisa ter executado com sucesso, e devolvido `AtoId`, antes de qualquer `SaveChanges` do lado Seleção — comprovado por teste que planta falha no lado Publicações e assere zero linhas escritas em Seleção. Os testes mockam `IRegistroDeAtoNormativo` (a porta), não `ICommandBus`/tipos de `Publicacoes.Contracts` — o próprio arquivo de teste não referencia nenhum tipo de Publicações, provando por construção que o handler não depende de nada além da porta.
- **Teste de idempotência**: mesma `Idempotency-Key` enviada duas vezes não duplica o ato.
- **Teste do princípio (ADR-0103)**: nenhum código em `Publicacoes` ramifica por tipo de ato ao processar `RegistrarAtoNormativoCommand` — mesma disciplina do fitness test que já protege o módulo.
- **Opt-in de codegen Wolverine (ADR-0098)**: `SelecaoCodegenRegistration.ConfigurarCodegenWolverine` precisa de `AlwaysUseServiceLocationFor<IRegistroDeAtoNormativo>()` — sem isso, `InvalidServiceLocationException` no primeiro build. O adapter ser público não basta sozinho: o Wolverine recursa a árvore de dependências até o `ICommandBus` interno dentro dele. Esse comportamento só aparece rodando a suíte completa — os testes direcionados às propriedades CA-02/CA-03/CA-04 passam com mock de `IRegistroDeAtoNormativo`, que não passa pelo codegen real.
- **Suíte completa da solução**: 23/23 projetos verdes (`dotnet test UniPlus.slnx`), incluindo os 128 testes pré-existentes de `Selecao.IntegrationTests` que exercitam `Publicar()`/`Retificar()` pela pipeline HTTP real — confirma que o mecanismo não regride nenhum comportamento hoje coberto por teste, com o desenho final (porta + adapter).
- **Fora do escopo comprovado pelo spike**: o vínculo genérico (`EntidadeTipo`/`EntidadeId`), a pré-checagem de conformidade/transição antes do registro (passo 1, redundante com a validação que `Publicar()` mantém internamente), a comparação de payload via `HashDoEfeito`, e a propagação de `InstantePublicacao` para `Edital`/`SnapshotPublicacao` em vez de um `TimeProvider.GetUtcNow()` fresco. O spike #819 usava uma entidade descartável em Publicações, sem nenhum desses quatro elementos — #820 precisa de teste próprio para cada um.

## Prós e contras das opções

### A — Transação ADO.NET compartilhada entre DbContexts

- Bom, porque elimina qualquer janela, por menor que seja.
- Ruim, porque exige `Selecao.Application`/`Infrastructure` referenciar peças de persistência de `Publicacoes` (não só `.Contracts`) — viola a ADR-0105 de forma mais grave que qualquer alternativa.
- Ruim, porque não há precedente de transação cross-`DbContext` no código, e os interceptors de outbox do Wolverine presumem um `DbContext` por transação (ADR-0004) — mecanismo a inventar do zero. A é a única opção sem NENHUMA janela de ato órfão (nem determinística, nem por corrida) — D não elimina essa janela (ver Idempotência/Consequências), só a torna rara e visível; o custo de inventar transação cross-`DbContext` do zero é o preço que D paga para não pagar.

### B — Síncrono sem ordem obrigatória (ignora falha)

- Bom, porque é o mais simples de escrever.
- Ruim, porque força escolher entre bloquear Seleção por uma falha em Publicações (mesma desvantagem de A, sem a vantagem de zero-janela) ou aceitar que o Edital publique e o ato nunca seja criado, **sem qualquer outbox por trás para recuperar** — pior propriedade operacional das quatro opções, porque nem tem a rede de segurança do outbox que C teria.

### C — Assíncrono via cascading/outbox

- Bom, porque isola completamente a disponibilidade de Publicações da de Seleção.
- Bom, porque a entrega sobrevive a crash de processo (outbox persistido na mesma transação do Edital).
- Ruim, porque inverte a ordem que a ADR-0104 já decidiu (ato depois do commit de Seleção, não antes) — exigiria ADR-0104 ser reaberta, não apenas uma nova ADR complementar.
- Ruim, porque depende de retry/DLQ que a ADR-0004 promete e o código não implementa — construir sobre isso significa construir DUAS coisas novas (o mecanismo em si e a infraestrutura de confiabilidade que ele pressupõe), quando D não depende de nenhuma das duas.
- Ruim, porque reabre a pergunta "PG queue cross-módulo é permitido, ou isso é território exclusivo de Kafka" (ADR-0001/0044/0056), que hoje trata cross-módulo como sinônimo de cross-processo — o que não é mais verdade sob a topologia de processo único da ADR-0097, mas essa reconciliação não está feita e não deveria ser pré-requisito silencioso desta decisão.

### D — Síncrono, ato primeiro (escolhida)

- Bom, porque é a única opção que respeita a ADR-0104 por construção, sem depender de infraestrutura que não existe.
- Bom, porque reaproveita três mecanismos já provados (`.Contracts`, `ICommandBus`, `Idempotency-Key` HTTP) em vez de inventar um quarto.
- Ruim, porque acopla disponibilidade — ver Consequências Negativas. Esse foi o trade-off central que o Tech Lead confirmou explicitamente ao aceitar esta ADR, não uma consequência escondida.
- Ruim, porque não elimina totalmente a janela de ato órfão — só a torna determinística e rara em vez de garantida. Validar tudo que não depende do ato ANTES de registrar (passo 1 da sequência) elimina a classe comum (processo não conforme/já publicado); o que sobra é só a corrida concorrente rara na escrita final de Seleção, sem reconciliação implementada por esta ADR — ver Consequências Negativas.

## Mais informações

- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — referência cross-módulo por valor
- [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md) — o padrão `{Module}.Contracts`, aqui estendido de leitura para escrita
- [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) — três executáveis, banco único; contexto que torna C tecnicamente possível mas que este ADR não escolhe
- [ADR-0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) — retificação como relação; o comando `RegistrarAtoNormativoCommand` carrega `AtoRetificadoId`/`Motivo` quando aplicável
- [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md) — a ordem "ato primeiro, versão depois" que motiva esta decisão
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — a assimetria de dependência que este ADR preserva
- Evidência de código verificada neste spike: `ICommandBus.cs`, `WolverineCommandBus.cs`, `ProcessoSeletivo.cs` (linhas 424-481, 540-560), `Edital.cs` (linhas 26-118), `CrossModuleReadIsolationTests.cs`, `ProcessoSeletivoController.cs` (linhas 279, 331)
- Spike de investigação (issue #819, fechada): branch `spike/819-coordenacao-selecao-publicacoes` — referência de código (porta `IRegistroDeAtoNormativo` + adapter `RegistroDeAtoNormativoAdapter`, opt-in de codegen, testes de ordem/isolamento/idempotência), **não mergeada** — usa uma entidade descartável em Publicações e campos de wiring ilustrativos no lugar do que #799 entrega de verdade.
- Story da implementação real: #820 (sub-issue de #40) — bloqueada por #799; recomendação de sequenciamento em relação a #802/#803/#804 registrada lá, a confirmar.
