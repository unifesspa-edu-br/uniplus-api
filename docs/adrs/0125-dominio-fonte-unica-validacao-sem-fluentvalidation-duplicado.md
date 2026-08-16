---
status: "accepted"
date: "2026-08-16"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed: []
---

# ADR-0125: Domínio como fonte única de validação — FluentValidation só cobre o que a entidade não cobre

## Contexto e enunciado do problema

`ConfiguracaoDomainErrorRegistration.cs` (e os registros equivalentes de outros módulos) mapeia `code` estáveis por campo/agregado (ex.: `uniplus.configuracao.campus.sigla_obrigatoria`), documentados no catálogo público de erros (`uniplus-developers`). O pipeline Wolverine valida todo command em duas camadas: FluentValidation (`Criar<Agregado>CommandValidator`, middleware, roda antes do handler) e as guardas de domínio (`Entidade.ValidarCampos`, retornam `Result.Failure(DomainError)` — ADR-0046, "Result para fluxo esperado, exceção para o inesperado").

Quando as duas camadas checam a mesma regra — o caso comum: `NotEmpty()`/`MaximumLength()` no validator, `IsNullOrWhiteSpace`/`Length` no domínio — o FluentValidation sempre falha primeiro, porque roda como middleware antes do handler. O handler, e a fábrica de domínio que geraria o `DomainError` específico, nunca chegam a rodar. `GlobalExceptionMiddleware` traduz a falha do FluentValidation para o `code` genérico `uniplus.validacao` — nunca o específico do agregado, mesmo que ele já esteja registrado e documentado publicamente.

Auditoria em `Campus` (agregado de referência para esta decisão) mostrou que a duplicação é quase total: sigla, nome, cidade (código IBGE/nome/UF) e coerência de endereço já têm equivalente exato no domínio — a maior parte via `ReferenciaCidadeGeo.Validar`/`ReferenciaEnderecoGeo.ValidarCoerencia`, componentes compartilhados por vários cadastros que referenciam cidade. O `CriarCampusCommandValidator` não cobria nenhuma regra sem equivalente de domínio. O `AtualizarCampusCommandValidator` tinha uma: `Id.NotEmpty()`, checagem de rota/DTO sem contrapartida em `Campus.Atualizar` — ver nota em Consequências sobre a única diferença de comportamento observável que essa remoção introduz.

## Drivers da decisão

- **ADR-0046** já decidiu "`Result.Failure` para fluxo esperado, exceção para o inesperado" — a duplicação faz a exceção (via FluentValidation) vencer o `Result` do domínio justamente no fluxo mais esperado de todos: campo obrigatório vazio.
- **ADR-0023/0024** já desenham o contrato: `code` estável por causa, `errors[]` por campo na mesma taxonomia do `code` raiz — o catálogo público documenta esses códigos como o comportamento real da API.
- **Fonte única**: duas implementações da mesma regra (validator e domínio) divergem com o tempo sem que ninguém perceba — achado concreto durante a investigação: o validator de `Campus` checava `MaximumLength(20)` sobre a sigla crua, o domínio checa `sigla.Trim().Length`; uma sigla com espaços poderia passar num caminho e falhar no outro.
- **Restrição confirmada do pacote `WolverineFx.FluentValidation`**: `IFailureAction<T>.Throw` não devolve valor (confirmado por decompilação da DLL instalada, v5.40.1, e pela documentação oficial, que recomenda validação explícita no handler para quem quer fugir do lançamento automático de exceção) — não há como o middleware curto-circuitar sem lançar. Qualquer correção que mantenha o FluentValidation como interceptador universal herda essa restrição.
- **Sem produção**: nenhum consumidor depende do formato atual — o momento de corrigir a causa raiz (duplicação) é agora, não depois de mais agregados replicarem o padrão.

## Opções consideradas

- **A. Threading do `code` específico via `.WithErrorCode(...)` no validator + exceção tipada carregando o `DomainError` até o `GlobalExceptionMiddleware`.**
- **B. Domínio como fonte única — `Result` ganha suporte aditivo a múltiplas violações; FluentValidation só cobre o que o domínio genuinamente não cobre (nenhuma regra para `Campus`).**
- **C. Validação explícita dentro do handler (`ValidateAsync` manual), sem depender do middleware nem de exceção — recomendação da própria documentação do Wolverine.**
- **D. Aposentar os `code` específicos hoje inalcançáveis, assumindo `uniplus.validacao` como contrato real.**

## Resultado da decisão

**Escolhida:** "B, com C absorvida por construção — domínio como fonte única de validação", porque é a única que elimina a causa raiz (duplicação de regra) em vez de só reconciliar seus sintomas, e porque, neste desenho, C deixa de ser uma opção à parte: a fábrica de domínio (`Campus.Criar`) **já é** a validação explícita que a documentação do Wolverine recomenda — chamada no início do handler, sem middleware, sem exceção no caminho de validação. Não há necessidade de introduzir `ValidateAsync` manual nem quebrar a convenção "handlers não invocam validator" quando não sobra validator para invocar.

A é descartada como arquitetura-alvo (mantém a duplicação para sempre, exige `.WithErrorCode(...)` disciplinado em ~60 validators do repositório sem nenhuma garantia estrutural contra esquecimento, e ainda assim depende de exceção). D é descartada porque contradiz o desenho de `errors[]` da ADR-0023 e desperdiça o catálogo público já publicado.

Mecanismo, implementado e testado no agregado `Campus`:

1. **`Result`/`Result<T>` ganham suporte aditivo a múltiplas violações** (`FieldError(Field, DomainError)`, `Result.ValidationFailure(IReadOnlyList<FieldError>)`), sem alterar `Result.Error`/`Failure(DomainError)` nem qualquer um dos ~1000 usos mono-erro existentes no repositório — confirmado por build completo sem nenhuma quebra. Nome do método novo é deliberadamente distinto de `Failure` (não um overload): um overload aceitando lista seria ambíguo para uma chamada com `null!`, quebrando o comportamento hoje fixado em `ResultTests.Failure_ComErroNulo_NaoLancaEDeixaErrorNulo`. `errors[]` no `ProblemDetails` (ADR-0023) só é emitido quando algum `FieldError` do lote tem `Field` preenchido — sinal que só existe vindo de `ValidationFailure`. A alternativa óbvia (emitir sempre que o status resolvido for 422) está errada: a maior parte do catálogo de erros do repositório mapeia para 422 por regra de negócio, não por validação de campo, e chega via `Failure` comum — inferir pelo status produziria `errors[{"field": null, ...}]` num erro que não tem campo nenhum associado.
2. **`Entidade.ValidarCampos` acumula em vez de retornar na primeira violação** — cada regra de campo (obrigatório, tamanho) contribui no máximo um `FieldError`, e cada delegação a value object compartilhado acumula por sua vez: `ReferenciaCidadeGeo.Validar` passou a checar código IBGE, nome e UF independentemente (os três ausentes ao mesmo tempo viram três erros, não um só — o FluentValidation removido reportava cada campo de cidade em separado, e a troca não podia perder essa granularidade), com `ReferenciaEnderecoGeo.ValidarCoerencia` contribuindo no máximo um `FieldError` adicional. A resposta usa o primeiro erro para `status`/`type`/`title`/`code` da raiz (fail-fast, mesma semântica que já existia) e todos para `errors[]`.
3. **O validator FluentValidation do agregado é removido quando não sobra nenhuma regra sem equivalente de domínio** — foi o caso de `Campus`. Quando um agregado tiver alguma checagem genuinamente de shape/DTO sem equivalente (não encontrado em `Campus`, mas não descartado para outros agregados), o validator permanece só para essa checagem, sem `.WithErrorCode`, caindo honestamente no `uniplus.validacao` genérico — não há mais nenhum caso em que o validator intercepta uma regra que o domínio também checa.
4. **`Criar`/`Atualizar` (domínio) e `CriarCampusCommand`/`AtualizarCampusCommand` (DTO) passam a aceitar os campos obrigatórios como `string?`** — sem validator garantindo não-nulo a montante, nulo é violação de campo ("obrigatório"), não `ArgumentNullException`/500. Tornar o DTO nullable também importa: com o campo não-anulável, o model binding automático do `[ApiController]` intercepta JSON com o campo ausente/nulo com um 400 genérico do ASP.NET (fora do formato RFC 9457), antes de o Wolverine e o domínio chegarem a rodar — o mesmo problema estrutural desta ADR, só que na camada de binding em vez da de validação. Confirmado com teste de integração que envia JSON com `sigla` genuinamente ausente (não string vazia). O `CA1062` do domínio é suprimido no símbolo, com justificativa (o analisador não reconhece o padrão "nulo vira `Result.ValidationFailure`" como um null-check válido).
5. **Os handlers nunca mutam o agregado nem consultam I/O antes de confirmar todas as fontes de validação** — campo (domínio), endereço (resolvido à parte, via Geo) e unicidade de sigla (repositório) sempre acumulam no mesmo `errors[]`, na ordem: campo → endereço → unicidade, sem retorno antecipado entre eles. Isso vale inclusive para a existência do agregado em `Atualizar`: antes desta ADR, o validator FluentValidation rodava como middleware antes do handler, então um payload mal formado nunca chegava a `ObterPorIdAsync` — validação sempre vencia sobre "não encontrado". Para preservar essa prioridade sem o validator, `Campus.ValidarAtualizacao` roda **antes** da busca do agregado (com `existente: null` na resolução de endereço — a otimização de preservar o carimbo do display cache é refeita depois, já com o agregado em mãos, sem custo de revalidar). Em `Criar`, a mutação já é naturalmente adiada, porque `Campus.Criar` só devolve uma instância nova, nunca rastreada pelo EF antes de `AdicionarAsync`. Em `Atualizar`, o agregado passa a ser rastreado assim que carregado — e o Wolverine roda `SaveChangesAsync` depois do handler retornar mesmo quando o `Result` é falha (mesmo comportamento do padrão de concorrência da ADR-0119), então mutar antes de confirmar tudo persistiria um estado nunca validado com sucesso. Por isso o domínio expõe a validação de campo separada da mutação (`Campus.ValidarAtualizacao`, sem efeito colateral, e `Campus.Atualizar`, que muta) — o handler só chama a segunda depois de já saber, por construção, que o resultado será sucesso.
6. **`errors[].field` usa o casing do payload JSON** (camelCase — "sigla", não "Sigla") e mapeia cada sub-código de `ReferenciaCidadeGeo` para o campo real que ele descreve (`cidadeNome`/`cidadeUf`/`cidadeCodigoIbge`), não um campo fixo para qualquer falha de cidade.

**Escopo desta ADR**: valida o mecanismo (aditivo no `Kernel`, reutilizável por todo módulo) e aplica-o integralmente em `Campus`, com evidência de zero regressão na suíte inteira da solução (unit + arch + integration, todos os módulos) — exceto a diferença de comportamento documentada abaixo (`Id` vazio). **Fora de escopo**: migrar os demais ~60 validators do repositório para o mesmo padrão — trabalho real, mecânico caso a caso (cada agregado precisa da mesma auditoria "o que aqui não tem equivalente de domínio?"), rastreado à parte.

## Consequências

### Positivas

- Elimina a causa raiz: nenhuma regra de campo é checada duas vezes para `Campus`; o `code` específico do catálogo público volta a ser retornado de fato.
- `errors[]` reporta múltiplas violações simultâneas de uma só vez — melhor que o caminho de exceção genérico, que também tem esse array mas hoje o preenche com nomes internos do `PropertyValidator` do FluentValidation quando nenhuma regra usa `.WithErrorCode`.
- A emissão de `errors[]` é aditiva e segura para todo o repositório: como o sinal é a presença de `Field` (só existe vindo de `ValidationFailure`), nenhum dos ~660 sites de `Result.Failure` de erro único hoje existentes — a maioria mapeada para 422 por regra de negócio, não por campo — passa a emitir o array por acidente.
- Sem exceção no caminho de validação de `Campus` — alinhamento total com a ADR-0046 para esse agregado, não um meio-termo.
- Nenhuma escrita parcial: o agregado só é mutado depois que toda fonte de validação (campo, endereço, unicidade) confirma sucesso — vale inclusive para `Atualizar`, onde o agregado já chega rastreado pelo EF antes do handler decidir se o resultado é sucesso ou falha.
- Verificado sem regressão: suíte completa da solução (unit + arch + integration, todos os módulos) permanece 100% verde.

### Negativas

- Rollout mecânico pendente em ~60 outros validators — até lá, esses agregados continuam sujeitos ao problema original.
- Cada agregado migrado exige auditoria própria (confirmar que toda regra do validator tem equivalente de domínio) — não é um `sed` em massa.
- `CA1062` suprimido em `Campus.Criar`/`Atualizar` — supressão justificada (ADR-0117), mas é uma exceção à política padrão que precisa ser repetida (ou generalizada) em cada agregado migrado.
- `Atualizar` valida os campos duas vezes no caminho de sucesso (`ValidarAtualizacao` para decidir se pode mutar, depois `Atualizar` internamente antes de mutar) — custo de CPU sem I/O, aceito em troca da garantia de nunca mutar antes de confirmar sucesso.
- **Única diferença de comportamento observável desta migração**: `AtualizarCampusCommandValidator` recusava `Id` vazio com 422 (`Id do Campus é obrigatório`) — checagem de rota/DTO sem equivalente em `Campus.Atualizar`, porque `Id` não é campo do agregado, é identidade. Sem o validator, `Id = Guid.Empty` chega a `ObterPorIdAsync`, não encontra nenhum Campus (nenhum registro tem esse Id) e devolve 404 `Campus não encontrado` — resposta correta por outro caminho, não uma regressão de correção, mas um `code`/status diferente do anterior para esse caso extremo (só alcançável enviando o GUID zerado tanto na URL quanto no corpo). Decisão deliberada: não reintroduzir um validator só para essa checagem, porque "não encontrado" já é a resposta domain-truthful para um identificador que não existe.

### Neutras

- O validator FluentValidation não desaparece do repositório como conceito — continua sendo o lugar certo para checagens de shape/DTO sem equivalente de domínio (nenhuma encontrada em `Campus`, mas esperado em agregados com estrutura de payload mais rica).

## Confirmação

- `ResultTests`/`ResultGenericTests` (Kernel) — comportamento mono-erro existente intacto, incluindo o pin de `Failure(null!)`.
- `ResultExtensionsTests` — `errors[]` presente sempre que pelo menos um `FieldError` do lote tem `Field` preenchido (só acontece via `ValidationFailure`), com um elemento quando há só uma violação e vários quando o lote acumula mais; ausente tanto em `Failure` que resolve para outro status (`SiglaJaExiste`, 409) quanto em `Failure` comum que resolve para 422 por regra de negócio sem ser validação de campo (ex.: Campus responsável não encontrado) — o sinal correto é a origem do `Result` (`ValidationFailure` vs. `Failure`), não o status HTTP.
- `CampusTests` — acumulação de múltiplas violações, nulo tratado como obrigatório (não lança), violações de fontes diferentes (regra própria + `ReferenciaCidadeGeo`) no mesmo lote, código/nome/UF de cidade ausentes ao mesmo tempo acumulam as três violações rotuladas (`cidadeCodigoIbge`/`cidadeNome`/`cidadeUf`), `errors[].field` mapeado corretamente por sub-código de cidade (`cidadeNome`/`cidadeUf`, não sempre `cidadeCodigoIbge`), `codigoEmec` só de espaços normaliza para nulo sem falhar.
- `ReferenciaCidadeGeoTests` — código, nome e UF ausentes ao mesmo tempo acumulam as três violações independentes (paridade com o FluentValidation removido, que reportava cada campo em separado); mensagem de UF incoerente não ecoa o valor de `cidadeUf` submetido (ADR-0023, "nunca carrega valor ou qualquer reflexo do dado rejeitado"); sem o validator como camada de barreira, este value object compartilhado (também usado por `LocalOferta`/`Instituicao`) passa a ser o único ponto de checagem para quem o consome.
- `CriarCampusCommandHandlerTests` — unicidade consultada só após validação bem-sucedida.
- `AtualizarCampusCommandHandlerTests` — comando com `Sigla`/cidade nulos não lança, devolve a violação de domínio; endereço inválido ou sigla conflitante com os demais campos válidos não muta o agregado já rastreado pelo EF antes do handler confirmar sucesso (o Wolverine roda `SaveChangesAsync` depois do handler mesmo em falha — mutar antes persistiria dado nunca confirmado como válido); Id inexistente com Sigla vazia devolve a violação de campo, não `NaoEncontrado`, sem consultar o repositório — validação sempre vence sobre existência, mesma prioridade que o validator removido garantia.
- `CampusEndpointTests` (integração, Testcontainers) — POST com `sigla`/`nome` vazios simultaneamente devolve as duas violações em `errors[]` com `field` em camelCase; POST com `sigla` genuinamente ausente do corpo JSON (não string vazia) chega ao domínio como 422 específico, não ao 400 genérico do ASP.NET, e ainda carrega `errors[]` com um elemento; CEP inválido e cidade incoerente continuam 422.
- Baseline OpenAPI (`contracts/openapi.configuracao.json`) regenerada e revisada — diff restrito aos cinco campos que passaram a `string?` nos dois schemas de Campus, sem mudança em `required` (padrão já existente no repositório para campos nullable).

## Prós e contras das opções

### A — Threading via `.WithErrorCode(...)` + exceção tipada

- Bom: menor diff por agregado; não exige tocar o `Kernel`.
- Ruim: mantém a duplicação de regra para sempre; depende de disciplina (`.WithErrorCode` correto em toda regra nova) sem garantia estrutural; ainda usa exceção no caminho de validação esperado; acumula um segundo local (`DomainValidationException`) para manter sincronizado com o registro de erros.

### B — Domínio como fonte única (escolhida, com C absorvida)

- Bom: elimina a causa raiz; sem exceção no caminho de validação; `errors[]` nativo, sem depender de anotação por regra; menor TCO recorrente — agregado novo escreve cada regra uma vez, no domínio.
- Ruim: rollout maior por agregado (auditoria, não substituição mecânica); exige a extensão do `Result` (feita, aditiva, sem quebra) e supressão pontual de `CA1062`.

### C — Validação explícita no handler, isolada

- Bom: seria a recomendação oficial do Wolverine para fugir da exceção default.
- Ruim, como opção **isolada**: sozinha não resolve a duplicação — só trocaria o canal de exceção por chamada explícita, mantendo `NotEmpty`/`MaximumLength` no validator e regras equivalentes no domínio. Deixa de ser um problema quando combinada com B: a fábrica de domínio já é a validação explícita, sem precisar de `ValidateAsync` manual nem violar a convenção "handlers não invocam validator".

### D — Aposentar os `code` específicos

- Bom: simplifica o registro de erros — menos entradas para manter sincronizadas com o catálogo público.
- Ruim: contradiz o desenho de `errors[]` da ADR-0023; desperdiça o catálogo público já publicado (`uniplus-developers`) para esses códigos. Rejeitada.

## Mais informações

- ADR-0023 — Formato de erro RFC 9457
- ADR-0024 — Mapeamento DomainError → HTTP
- ADR-0046 — Validação de regras sem exceção (`Result.Failure`)
- ADR-0117 — Política de supressão de análise estática
- Issue de origem: uniplus-api#1176
