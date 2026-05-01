---
status: "accepted"
date: "2026-05-01"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0019: Proibir PII em path segments de URL

## Contexto e enunciado do problema

A LGPD (Lei 13.709/2018, Art. 6º inciso VII) exige medidas técnicas que evitem a exposição de dados pessoais em processamento acessório — incluindo logs, sinks downstream e qualquer trânsito de URL fora do controle direto da aplicação. Em APIs REST, há uma diferença prática entre dados em **query string** e dados em **path segments** quanto à superfície de vazamento.

O `RequestLoggingMiddleware` do `uniplus-api` mascara apenas valores de query string via `QueryStringMasker` (ADR-0011 cobre o lado de logs estruturados). Esse controle é compensatório: roda dentro do pipeline ASP.NET, depois que a request já passou por camadas anteriores. Quando a PII vai no caminho — por exemplo `GET /candidatos/{cpf}` — vaza fatalmente em vetores que o middleware **não** alcança:

- access logs do nginx / proxy reverso, antes de qualquer middleware da aplicação;
- WAF, CDN e load balancer (AWS ALB, Cloudflare, etc.), que costumam logar `request_uri` por padrão;
- cabeçalho `Referer`, transmitido pelo navegador a terceiros quando o usuário clica em links externos a partir da página;
- histórico do navegador, ferramentas de rede e qualquer print/share da URL.

A discussão se origina no review do PR #119 (RequestLoggingMiddleware com PII masking) e no PR #233, que removeu um cross-link cosmético em código-fonte porque a regra precisava ser fixada como ADR canônica do `uniplus-api`. A regra já é seguida na prática nos slices entregues (rotas usam `Guid` como identificador opaco em todos os módulos). Esta ADR formaliza a regra como decisão arquitetural enforçável, não dependente de disciplina ad hoc por code review.

## Drivers da decisão

- Conformidade LGPD por construção, removendo o vazamento na origem em vez de mascarar a posteriori.
- Defesa em profundidade — query string já tem masking via `QueryStringMasker`, mas path não tem nem como tê-lo de forma efetiva (camadas anteriores).
- Auditabilidade: access logs de infra (nginx, WAF, CDN) precisam ficar livres de PII para que possam ser arquivados, exportados e analisados sem tratamento adicional.
- Alinhamento com a Lei 14.129/2021 (Governo Digital) e com o princípio da minimização de dados.
- Evitar acoplar a confidencialidade de PII ao cabeçalho `Referer` do navegador, que escapa do nosso controle.

## Opções consideradas

- Path segments com identificadores opacos (UUID); PII só em query string ou body.
- Path segments com PII e masking adicional via middleware do `uniplus-api`.
- Path segments com PII criptografada ou hasheada.
- Apenas comentário/convenção informal, sem ADR formal.

## Resultado da decisão

**Escolhida:** path segments com identificadores opacos (UUID); PII só em query string (mascarada por `QueryStringMasker`) ou no corpo da requisição.

A regra vale para **todos os módulos** do Uni+ — Seleção, Ingresso e quaisquer módulos futuros — em qualquer API REST exposta interna ou externamente. Identificadores de recursos de domínio são tipados como `Guid` (UUID v4 ou v7) e roteados em path segments (`GET /candidatos/{id}`); valores como CPF, e-mail, nome, RG, telefone, número de matrícula e demais dados pessoais nunca aparecem como segmento de caminho. Quando a operação requer lookup por dado pessoal (caso típico: busca de candidato pelo CPF), o dado vai em query string ou body de uma rota cujo path é fixo (por exemplo `POST /candidatos/buscar` com CPF no body, ou `GET /candidatos?cpf={cpf}` ciente de que a query string será mascarada nos logs estruturados).

Esta decisão é **preventiva**: evita gerar a PII no path em primeiro lugar. Difere do `QueryStringMasker` (ADR-0011 do lado de logs / ADR de pipeline), que é **compensatório** — protege o que a aplicação loga, mas não o que nginx/WAF/CDN logam antes de a request chegar ao código.

## Consequências

### Positivas

- Zero PII em access logs de nginx, WAF, CDN e load balancer — sem necessidade de configurar masking nessas camadas.
- Zero PII em cabeçalho `Referer` enviado a terceiros.
- Zero PII em histórico de navegador, screenshots de URL e ferramentas de rede.
- Conformidade LGPD por construção — não dá para "esquecer de mascarar" porque o dado nunca esteve no path.
- Reduz a superfície de auditoria: logs de infra podem ser exportados/arquivados sem etapa adicional de tratamento de PII.

### Negativas

- Lookups por PII deixam de ser `GET /recurso/{pii}` e passam a `POST /recurso/buscar` (com PII no body) ou `GET /recurso?atributo={pii}` (com query string mascarada). Trade-off deliberado: prefere-se semântica de rota menos óbvia a vazamento estrutural.
- Design de rotas exige disciplina — quem desenha a API precisa pensar em identificadores opacos desde o início.
- Eventuais redirects de sistemas legados (COC, UDOCS) que usavam PII em path precisam de tradução na borda de integração.

### Riscos

- Rota legada introduzindo PII em path passar pelo review e chegar a produção. Mitigação: code review enforça; planeja-se uma fitness rule (ArchUnitNET, ADR-0012) varrendo controllers/minimal APIs e sinalizando parâmetros de path tipados como `Cpf`, `Email`, `string` com nome sugestivo (`cpf`, `email`, `nome`).

## Confirmação

- Code review enforça a regra em todo PR que adiciona ou altera roteamento.
- O `<remarks>` do `RequestLoggingMiddleware` referencia esta ADR como ponteiro acionável a partir do código.
- Sugestão de evolução: fitness rule arquitetural (ArchUnitNET) reprovando tipos como `Cpf`/`Email` em parâmetros de rota anotados como `[FromRoute]` ou em templates de minimal API. Item rastreado fora desta ADR.

## Prós e contras das opções

### Path segments com PII e masking via middleware

- Bom, porque mantém a semântica de rota REST tradicional (`/recurso/{id-natural}`).
- Ruim, porque masking só roda dentro do pipeline ASP.NET — nginx, WAF, CDN e `Referer` não são alcançados. Resolve o sintoma errado (logs da app) deixando a doença (URL com PII) intacta.

### Path segments com PII criptografada ou hasheada

- Bom, porque o dado claro nunca aparece no canal externo.
- Ruim, porque adiciona complexidade desproporcional (chave, rotação, KMS), continua deixando o token único em `Referer` (rastreável), e não resolve o caso de o token derivado ser reversível por dicionário (CPF tem espaço enumerável).

### Apenas comentário/convenção informal, sem ADR formal

- Bom, porque é leve.
- Ruim, porque sem documento canônico no `docs/adrs/` a regra depende de memória institucional. Já houve cross-link removido em código (PR #233) por não haver alvo estável para apontar.

## Mais informações

- ADR-0011 — Mascaramento de CPF em logs via enricher Serilog (peer arquitetural; cobre o lado de logs estruturados, complementar a esta ADR).
- ADR-0012 — ArchUnitNET como biblioteca canônica de fitness tests (caminho para a fitness rule sugerida na seção Confirmação).
- LGPD (Lei 13.709/2018), Art. 6º inciso VII e Art. 46.
- Lei 14.129/2021 — Governo Digital.
- Issue origem (cross-repo): `unifesspa-edu-br/uniplus-docs#68`.
- Issue espelho deste repositório: #234.
- PR #119 — finding I1 da revisão do `RequestLoggingMiddleware`, que originou a discussão.
- PR #233 — removeu cross-link cosmético em `RequestLoggingMiddleware` por política de código-fonte; esta ADR substitui o ponteiro removido.
