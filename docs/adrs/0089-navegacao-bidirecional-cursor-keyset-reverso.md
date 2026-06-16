---
status: "accepted"
date: "2026-06-16"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed: []
---

# ADR-0089: Navegação bidirecional na paginação por cursor via keyset reverso

## Contexto e enunciado do problema

A [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) fixou a paginação por cursor opaco cifrado e **já antecipou** a navegação reversa: declara `direction` (`next`/`prev`, default `next`) entre os parâmetros de query, prevê `rel="prev"` no header `Link` e reconhece o cursor como "unidirecional ou bidirecional restrito". O que aquela ADR explicitamente **não decidiu** foi a *mecânica* da navegação reversa — a forma do query backward, a integridade do cursor face à direção e a semântica das flags de continuidade.

Hoje a implementação é **forward-only**: os repositórios fazem `WHERE Id > cursor ORDER BY Id ASC` (chave keyset = `Id`, GUID v7 ordenável por criação, [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md)) e o helper `OkPaginatedAsync` só emite `rel="next"`. O frontend precisa oferecer navegação **Anterior / Próximo** (mais clara que "carregar mais" para listas administrativas), o que exige completar a navegação reversa que a ADR-0026 deixou em aberto.

Esta ADR decide **como** implementar essa navegação reversa, completando a ADR-0026.

## Drivers da decisão

- **Conformidade com a ADR-0026.** `direction` já é query param; o cursor permanece opaco.
- **Estabilidade de janela keyset.** A navegação reversa mantém a mesma garantia que a forward — sem o problema de janela do offset.
- **Decode no boundary ([ADR-0031](0031-decoding-de-cursor-opaco-no-boundary-http.md)).** O handler recebe primitivas; a direção é resolvida no boundary HTTP.
- **Sem `COUNT(*)`.** As flags de continuidade (`hasPrevious`/`hasNext`) não podem custar uma contagem O(N).
- **Integridade do par cursor+direção.** Um cursor não pode ser reutilizado com a direção errada produzindo janela incoerente.
- **Ordem de apresentação estável.** Independente da direção navegada, a página é exibida em ordem canônica ascendente por `Id`.

## Opções consideradas

- **A. `direction` como query param (ADR-0026), vinculada por integridade no cursor, + keyset reverso com reversão em memória.**
- **B. Direção embutida apenas no payload do cursor**, sem query param.
- **C. Janela materializada / offset reverso** para salto arbitrário e flags exatas.

## Resultado da decisão

**Escolhida:** "A", porque conforma à ADR-0026 (que fixou `direction` como query param), mantém o cursor opaco, fecha a brecha de integridade do par cursor+direção e entrega flags exatas sem custo de contagem.

Mecânica:

- **Query forward (`direction=next`, e primeira página).** `WHERE Id > @after ORDER BY Id ASC LIMIT @limit + 1` (sem `@after` na primeira página). `hasNext = retornados > limit`; a página são os primeiros `limit`.
- **Query backward (`direction=prev`) — corte antes da reversão.** `WHERE Id < @after ORDER BY Id DESC LIMIT @limit + 1`. A ordem do corte é crítica: `hasPrevious = retornadosDesc > limit`; descarta-se o item-probe **ainda em ordem DESC** (`pageDesc = retornadosDesc.Take(limit)` — o probe é o mais antigo) e só então reverte-se para ascendente (`pageAsc = pageDesc.Reverse()`). Reverter antes de cortar omitiria o boundary e incluiria o probe.
- **Flags exatas, sem `COUNT`.** A flag do lado **navegado** vem do probe `n+1` (já buscado). A flag do lado **oposto** vem de um `EXISTS` indexado barato sobre a mesma query base (com os mesmos filtros): `hasPrevious = AnyAsync(Id < primeiroIdDaPagina)` quando se navegou forward; `hasNext = AnyAsync(Id > ultimoIdDaPagina)` quando se navegou backward. Na primeira página `hasPrevious = false` por definição. Não se infere flag pela direção de chegada — sob deleção concorrente ou cursor antigo essa inferência daria falso positivo.
- **`direction` é query param, mas vinculada ao cursor.** O servidor lê `direction` do query param (ADR-0026) e o decoda no boundary (ADR-0031). Para impedir reuso incoerente (pegar o cursor de `next` e chamar `direction=prev`), o payload passa a carregar a direção/âncora para a qual foi emitido; no decode valida-se `query.direction == payload.Direction` — divergência é tratada como cursor adulterado (`400 uniplus.cursor.invalido`). O cursor segue **opaco e cifrado AES-GCM**; o `CursorEncoder` não muda (serializa o payload como está). O campo de direção é obrigatório no payload — o sistema ainda não está em produção, então não há cursores em circulação a preservar.
- **Emissão dos links.** Com a página em ordem ascendente, `prevCursor` cifra o `Id` do primeiro item (com `Direction=prev`) e o link recebe `direction=prev`; `nextCursor` cifra o `Id` do último item (com `Direction=next`) e o link recebe `direction=next`. Cada `rel` é emitido só quando a flag correspondente é verdadeira. `LinkHeaderBuilder`/`PageLinks` já suportam `prev`.
- **Página vazia com cursor válido.** Um cursor válido (não expirado) cuja janela ficou vazia (ex.: itens deletados) retorna `200 []` **sem** `rel="prev"`/`rel="next"` (apenas `self`) — não há boundary para emitir e não se fabricam links a partir da âncora original. O cliente trata ausência de links como fim e reinicia se necessário.

## Consequências

### Positivas

- Completa a navegação reversa prevista pela ADR-0026 sem alterar o `CursorEncoder` nem a forma opaca do cursor.
- `hasPrevious`/`hasNext` **exatos** em todas as páginas via um `EXISTS` indexado por página — zero `COUNT(*)`.
- Integridade do par cursor+direção: cursor de `next` não pode ser usado como `prev` (validação no decode).
- Links auto-suficientes (RFC 5988): o cliente segue `rel="prev"`/`rel="next"` opacos sem conhecer a convenção.

### Negativas

- Um `EXISTS` extra por página para a flag do lado oposto (indexado, `LIMIT 1` — barato, mas não gratuito).
- Reversão em memória O(limit) no backward (desprezível para os limites atuais, máx. 100).
- Sem salto para página arbitrária (intrínseco a cursor; já registrado na ADR-0026).
- O payload ganha um campo de direção/âncora obrigatório — evolução de schema do cursor (sem impacto de migração: sistema fora de produção, sem cursores em circulação).

### Neutras

- A garantia keyset é a da ADR-0026: elimina duplicação/omissão por *shift* de janela do offset, **não** é snapshot consistency — inserções/remoções concorrentes refletem na travessia (itens novos após a âncora aparecem; removidos somem). Travessia consistente da coleção exigiria um marcador de snapshot no cursor, fora do escopo desta ADR.
- Cada endpoint paginado estende seu repositório (WHERE/ORDER condicionais + `EXISTS` do lado oposto) e handler (corte/​reversão + flags + dois boundaries); a chave keyset por `Id` mantém isso mecânico.

## Confirmação

- **Testes de integração** por endpoint: navegação forward e backward percorrendo a coleção sem duplicar/omitir; ordem ascendente preservada na página `prev`; `hasPrevious`/`hasNext` exatos nas bordas; reuso de cursor com `direction` trocada rejeitado com `400`; página vazia com cursor válido retorna `200 []` sem `prev`/`next`.
- **Testes de unidade** do model binder/decode: `?direction=prev|next` parseado; valor inválido rejeitado; default `next`; mismatch `query.direction × payload.Direction` → `400`.
- **Spectral / OpenAPI**: parâmetro `direction` declarado nos endpoints de coleção; `Link` sem metadado em body (ADR-0025/0026).

## Prós e contras das opções

### A. Query param vinculado ao cursor + keyset reverso

- Bom, porque conforma à ADR-0026 e mantém o cursor opaco.
- Bom, porque entrega flags exatas sem `COUNT(*)` e fecha a brecha de integridade cursor+direção.
- Ruim, porque adiciona um `EXISTS` por página e um campo ao payload.

### B. Direção apenas no payload do cursor (sem query param)

- Bom, porque o link fica self-describing sem param visível.
- Ruim, porque contraria a ADR-0026 (que fixou `direction` como query param); o link já é opaco e auto-suficiente com o param + binding.

### C. Janela materializada / offset reverso

- Bom, porque permitiria salto para página arbitrária e flags exatas triviais.
- Ruim, porque reintroduz os custos de offset (instabilidade de janela, scan O(N)) que a ADR-0026 rejeitou.

## Mais informações

- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — paginação por cursor opaco cifrado; esta ADR completa a navegação reversa que aquela deixou como "não decidido".
- [ADR-0031](0031-decoding-de-cursor-opaco-no-boundary-http.md) — decode do cursor no boundary HTTP; `direction` é resolvido e validado no mesmo ponto.
- [ADR-0025](0025-wire-formato-sucesso-body-direto.md) — body de coleção como array puro; metadados de navegação seguem só em headers.
- [ADR-0032](0032-guid-v7-para-identidade-de-entidades.md) — `Id` GUID v7 ordenável, base da chave keyset.
- [RFC 5988 / 8288 — Web Linking](https://www.rfc-editor.org/rfc/rfc8288.html); modelo de navegação bidirecional inspirado nas Relay Connections (GraphQL).
