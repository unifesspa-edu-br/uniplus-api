---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0026: Paginação via cursor opaco cifrado e propagação por `Link` header

## Contexto e enunciado do problema

Endpoints da `uniplus-api` que retornam coleções (`GET /editais`, futuros `/inscricoes`, `/recursos`, etc.) precisam de uma estratégia de paginação consistente. As escolhas em jogo são clássicas: paginar por **offset/limit**, por **page number**, por **cursor opaco**, ou variações dessas; e propagar metadados de navegação no **body** ou em **headers HTTP**.

A [ADR-0025](0025-wire-formato-sucesso-body-direto.md) já fixou que o body de uma coleção é um array JSON puro — sem wrapper de objeto, sem sibling fields no root. Por consequência sintática, qualquer metadado de paginação precisa ir por header HTTP. Resta decidir **qual estratégia de paginação** e **como o cursor é construído** quando a opção for cursor.

A escala alvo justifica investimento na decisão correta: o módulo Seleção sozinho deve operar com dezenas de milhares de inscrições por edital × dezenas de editais × histórico multi-anual. Estimadores como `pg_class.reltuples` indicam que `COUNT(*)` em PostgreSQL passa a ser custoso (O(N) na tabela acima de algumas dezenas de milhares de rows), e paginação por offset enfrenta dois problemas conhecidos: custo crescente do `OFFSET` à medida que o usuário avança, e instabilidade de janela quando rows são inseridos/removidos durante a navegação.

## Drivers da decisão

- **Estabilidade durante navegação.** Inscrições, recursos e classificações mudam concorrentemente. Cursor baseado em `last_id`/`sort_key` mantém uma janela estável; offset gera duplicatas e omissões quando a base muda durante a paginação.
- **Performance no PostgreSQL.** `OFFSET N LIMIT M` faz scan de `N+M` rows mesmo quando descarta as primeiras `N`. Cursor com índice apropriado é O(M) independente da posição.
- **Custo de `COUNT(*)`.** Expor `total` por padrão exige `COUNT(*)` que é O(N) sem índices auxiliares. Em editais com 50k+ inscrições isso é caro o suficiente para impactar latência percebida.
- **Coerência com [ADR-0025](0025-wire-formato-sucesso-body-direto.md).** Body é array puro; metadados (`Link`, `has_more`, `cursor_next`) precisam viajar exclusivamente em headers HTTP.
- **Padrão aberto para metadados de navegação.** RFC 5988/8288 (`Link` header com `rel=next/prev/first/last`) é o padrão consagrado, reconhecido por GitHub, GitLab, Atlassian e tooling automatizado (clientes HTTP, OpenAPI codegen).
- **Opacidade contra reverse-engineering e tampering.** Cursor em texto claro (`?cursor=last_id:42,sort:created_at`) revela esquema interno, permite enumeration attacks (manipular `last_id` para pular rows), e exige validação adicional no servidor para detectar tampering.
- **LGPD.** Cursor não pode embutir PII (CPF, número de inscrição, identificador externo) nem permitir derivar PII de seu conteúdo, mesmo descriptografado.

## Opções consideradas

- **A. Cursor opaco cifrado (AES-GCM)** com payload server-side contendo `last_id`, `sort_key`, `expiry` e propagação via `Link` header.
- **B. Cursor opaco em texto claro** (base64-url do mesmo payload, sem cifragem) com validação de assinatura HMAC no servidor.
- **C. Paginação por offset/limit** (`?offset=N&limit=M`).
- **D. Paginação por page number/size** (`?page=N&size=M`).

## Resultado da decisão

**Escolhida:** "A — Cursor opaco cifrado (AES-GCM) propagado via `Link` header", porque é a única opção que combina estabilidade de janela, performance independente da posição, opacidade total para o cliente e propagação de metadados em headers (compatível com a decisão da [ADR-0025](0025-wire-formato-sucesso-body-direto.md)).

### Forma do cursor

O cursor recebido pelo cliente é uma string base64-url-safe sem padding. Seu conteúdo descriptografado server-side é um payload binário compacto que carrega:

- **`last_id`** ou **chave de ordenação composta** — referência inequívoca do último item da página anterior. Tipicamente um ULID ou GUID, **nunca** CPF, número de inscrição ou outro identificador externo.
- **`sort_key`** — coluna ou expressão de ordenação aplicada (ex.: `created_at_desc`, `numero_edital_asc`). Endpoint declara qual valor é válido; cliente não escolhe.
- **`expiry`** — timestamp absoluto após o qual o cursor não é mais aceito.
- **`scope`** — escopo de tenant/contexto quando aplicável (multi-tenancy futuro). Garante que cursor emitido em um contexto não é replicável noutro.

A cifragem usa **AES-GCM** com chave gerenciada por infraestrutura externa de gerenciamento de chaves (em produção: HashiCorp Vault transit engine; em desenvolvimento e CI: fixture local equivalente). A chave nunca sai do gerenciador; cifragem e decifragem ocorrem via API. Rotação periódica é responsabilidade do gerenciador — cursors emitidos com chave anterior continuam válidos até expirarem por TTL, sem revogação retroativa.

### Forma da resposta

A resposta de um endpoint de coleção segue a [ADR-0025](0025-wire-formato-sucesso-body-direto.md): body é array JSON do recurso. Metadados de paginação vão **exclusivamente em headers HTTP**:

- **`Link`** (RFC 5988/8288) — links de navegação. Valores possíveis: `rel="next"`, `rel="prev"`, `rel="first"`. Ausência de `rel="next"` significa fim da paginação (substitui um campo `has_more` em body). Cada link carrega o cursor opaco no parâmetro `cursor`.
- **`X-Page-Size`** — tamanho efetivo da página retornada (pode diferir do `limit` solicitado quando o final da coleção é alcançado).

Total de itens **não é exposto por padrão**. Endpoints que precisam expor total declaram opt-in explícito via parâmetro de query (ex.: `?include_total=true`) e o resultado é retornado como header `X-Total-Count-Estimated`, com valor obtido de estimadores eficientes (`pg_class.reltuples` ou similares); contagem exata é custosa o bastante para ser opt-in caso a caso, com justificativa no endpoint.

### Parâmetros de query aceitos

- **`cursor`** (string opaca) — cursor recebido em `Link` da página anterior. Ausência indica primeira página.
- **`limit`** (inteiro) — tamanho da página. Cada endpoint define um valor padrão e máximo; valor solicitado acima do máximo é silenciosamente reduzido (não retorna erro).
- **`direction`** (`next`/`prev`, opcional) — direção quando o endpoint suporta navegação reversa. Default é `next`.

A ordenação **não** é parametrizável por query no V1 — cada endpoint declara sua ordenação canônica. Isso simplifica a chave do cursor, evita combinações inválidas e elimina ataques que tentam descobrir índices ausentes via `?sort=campo_sem_index`. Endpoints que precisarem de múltiplas ordenações expõem variantes (`/editais` e `/editais?sort=encerramento`, decisão por endpoint).

### Erros específicos da paginação

- **Cursor inválido / tampered / não decifrável** → 400 Bad Request com `code: uniplus.pagination.cursor_invalido`. Cliente deve reiniciar a paginação a partir do início.
- **Cursor expirado** → 410 Gone com `code: uniplus.pagination.cursor_expirado`. Mesma resolução: reiniciar.
- **`limit` negativo ou não-numérico** → 422 Unprocessable Entity com erro de validação por campo (ADR-0023, extension `errors[]`).

### Esta ADR não decide

- A infraestrutura de gerenciamento de chaves (HashiCorp Vault transit, AWS KMS, alternativas) é detalhe de implementação; a decisão binding é "cifrado com chave gerenciada externamente, nunca embarcada no código".
- Como o índice PostgreSQL é estruturado para suportar cursor (composite index, partial index) — decisão de tarefa de implementação por slice.
- TTL exato do cursor — sugestão razoável é 24h, mas o valor é configurável por endpoint conforme padrão de uso (ex.: paginação em listagens administrativas pode tolerar TTL maior; busca em portal público pode preferir TTL curto). Decisão fica para a tarefa de implementação.
- Estratégia de paginação retroativa para o `EditalController` existente (3 endpoints, sem paginação atual) — escopo do PR pilot.

## Consequências

### Positivas

- **Janela estável.** Inserções e remoções concorrentes não afetam a navegação em curso — cursor referencia um ponto absoluto na ordenação.
- **Performance previsível.** Latência de página é função do `limit`, não da posição. Crucial em editais com 50k+ inscrições.
- **Cliente não pode reverse-engineer estrutura.** Cifragem opaca elimina enumeration attacks e tampering como vetores; nenhuma validação adicional de schema do cursor é necessária no servidor além da decifragem.
- **Compatibilidade com tooling padrão.** `Link` header é interpretado por bibliotecas HTTP (axios `link-parser`, `link-parser-rust`, `httpx`, etc.) e por OpenAPI codegen; integradores ganham paginação sem tratamento custom.
- **Coerência com [ADR-0025](0025-wire-formato-sucesso-body-direto.md).** Não há tentação de reintroduzir wrapper de objeto para acomodar `total`/`has_more` — esses ficam fora do body por construção.

### Negativas

- **Dependência de infraestrutura de chave.** A `uniplus-api` em produção requer Vault (ou equivalente) up para emitir cursors; falha do gerenciador de chaves degrada paginação. Mitigação: cache em memória da chave decifrada com TTL curto (decisão de implementação) reduz dependência ao boot.
- **Navegação não-linear é limitada.** Cursor é unidirecional ou bidirecional restrito; salto direto para "página 50" não existe. Endpoints que demandarem essa UX precisam de `include_total` + paginação por offset opt-in (caminho explicitamente não default), justificada por endpoint.
- **Custo de implementação inicial.** Cifragem, gerenciamento de chave, mapeamento de erros específicos e tooling para depuração de cursor (ferramenta interna que decifra o conteúdo para suporte) são esforço extra na primeira slice — amortizado nas demais.

### Neutras

- A decisão não impede paginação por offset existir como **opção opt-in** em endpoints específicos com justificativa documentada. Default permanece cursor opaco cifrado.
- Ausência de `total` por padrão pode surpreender quem vem de APIs offset-based; atenuado pela documentação explícita no portal e pelos `Link` headers de navegação.

## Confirmação

1. **Spectral rule no CI** — endpoints de coleção declarados no spec OpenAPI devem (i) aceitar parâmetro `cursor` opcional + `limit` com default e máximo; (ii) declarar `Link` header em respostas 200; (iii) **não** declarar campo `total`, `count`, `has_more`, `cursor_next` ou similar como propriedade de body. Falha indica reintrodução acidental de paginação por offset ou metadado em body.
2. **Smoke E2E (Postman/Newman) no CI** — cenários cobrem (a) primeira página retorna `Link rel="next"`; (b) navegação via cursor preserva ordenação e não duplica/omite rows; (c) cursor adulterado retorna 400 com `code: uniplus.pagination.cursor_invalido`; (d) cursor expirado retorna 410 com `code: uniplus.pagination.cursor_expirado`.
3. **Teste de performance regressivo** — endpoint pilot (`/editais` ou um endpoint de listagem mais alto-volume) tem teste de carga que mede latência da N-ésima página com volume sintético; regressão dispara alerta no CI.
4. **Inspeção manual de cursor.** Ferramenta interna (CLI ou endpoint admin protegido) decifra cursor para suporte/debug, com auditoria de uso. Garante que opacidade do cliente não vire opacidade interna.

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella (princípios 3 LGPD, 5 API pública, 6 padrões abertos).
- [ADR-0025](0025-wire-formato-sucesso-body-direto.md) — body de coleção como array puro (origem da restrição de metadado em headers).
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — wire format de erro consumido por respostas 400/410/422 desta ADR.
- [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) — princípio LGPD aplicado também ao conteúdo do cursor.
- [RFC 5988 — Web Linking](https://www.rfc-editor.org/rfc/rfc5988.html) e [RFC 8288 — sucessora](https://www.rfc-editor.org/rfc/rfc8288.html).
- [RFC 9110 — HTTP Semantics, §15.4 Status Codes](https://www.rfc-editor.org/rfc/rfc9110.html#name-status-codes) — base do uso de 410 Gone para cursor expirado.
- Use the Index, Luke! [No Offset](https://use-the-index-luke.com/no-offset) — referência canônica sobre problemas de paginação por offset em RDBMS.
- [PostgreSQL — Row Estimation Examples](https://www.postgresql.org/docs/current/row-estimation-examples.html) — base para `pg_class.reltuples` mencionado.
