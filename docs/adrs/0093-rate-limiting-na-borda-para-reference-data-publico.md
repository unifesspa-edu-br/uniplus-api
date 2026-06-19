---
status: "accepted"
date: "2026-06-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0093: Rate-limiting de endpoints públicos de reference data na borda, não no app

## Contexto e enunciado do problema

O módulo `Geo` ([ADR-0090](0090-modulo-geo-localidades.md)) expõe endpoints de consulta de reference data marcados `[AllowAnonymous]` — lookup de CEP (`GET /api/cep/{cep}`), listagens de estados/cidades, hierarquia/autocomplete e proximidade. São dados públicos (IBGE/DNE), sem autenticação, e o lookup de CEP em particular tem um **caminho frio** (cache miss / Redis fora) que encadeia consultas sobre as tabelas de faixa.

Mesmo com o cache-aside por selo de versão ([#674], memoizado em processo por [#703]) e o índice de range do caminho frio ([#704]), um endpoint anônimo é alvo natural de tráfego volumétrico/abuso. A pergunta é **onde** aplicar o controle de taxa (rate-limiting): no próprio aplicativo (middleware `RateLimiter` do ASP.NET Core, por endpoint/módulo) ou na **borda** (gateway de ingresso, ex.: Traefik), e se o controle no app deve entrar já.

## Drivers da decisão

- **Preocupação transversal** — rate-limiting de endpoints anônimos vale para todos os módulos, não só o `Geo`; aplicá-lo por módulo gera divergência de política.
- **Ingresso único** — todo tráfego externo passa pelo gateway, que já termina TLS e roteia; é o ponto natural para política uniforme.
- **Defesa em profundidade** — o índice de range ([#704]) e o cache ([#674]/[#703]) já amortecem a pressão no banco; o limitador é a camada contra abuso volumétrico, não a única defesa.
- **Simplicidade do app** — manter o app focado em lógica de domínio, sem política operacional de taxa espalhada em cada API.
- **Reversibilidade** — não fechar a porta para um limitador no app caso políticas por endpoint (ex.: token bucket por IP atrelado à identidade) se tornem necessárias.

## Opções consideradas

- **A**: Rate-limiting no app (ASP.NET Core `AddRateLimiter`), por endpoint/módulo.
- **B**: **Rate-limiting na borda (gateway/Traefik)**; controle no app adiado.
- **C**: Ambos (borda + app) desde já.

## Resultado da decisão

**Escolhida:** "B — rate-limiting na borda (gateway), controle no app adiado", porque o controle de taxa de endpoints anônimos é uma preocupação transversal de infraestrutura, mais bem aplicada de forma uniforme no ingresso único do que replicada e divergente em cada módulo.

A política de taxa (limite, burst, granularidade por IP) vive na configuração do gateway (Traefik), no repositório de infraestrutura, e cobre uniformemente os endpoints `[AllowAnonymous]` de reference data de todos os módulos. O caminho frio do lookup de CEP permanece protegido em profundidade pelo índice de range ([#704]) e pelo cache por selo ([#674]/[#703]). O limitador no app fica **disponível como escalonamento futuro** — se surgir necessidade de política por endpoint que o gateway não exprima bem (ex.: cota atrelada à identidade autenticada), entra com a sua própria ADR. Os controllers afetados carregam um comentário apontando para esta decisão.

## Consequências

### Positivas

- Política única e uniforme no ingresso, sem deriva por módulo.
- App focado em domínio, sem configuração operacional de taxa.
- Coerente com a topologia de gateway já adotada no ambiente.

### Negativas

- Depende de o gateway estar corretamente configurado e de o tráfego não alcançar os pods diretamente (mitigado por política de rede; pods não são expostos diretamente).
- A política de taxa não é visível no código do app — mitigado por esta ADR e pelo comentário-ponteiro nos controllers.

### Neutras

- Os valores concretos da middleware de taxa do Traefik (rate, burst) são parâmetro operacional no repositório de infraestrutura, fora do escopo desta ADR.

## Confirmação

- Comentário em `CepController` (e demais controllers anônimos do `Geo`, conforme evoluírem) apontando para esta ADR.
- A configuração de taxa do gateway materializa a política; um escalonamento para limitador no app adicionaria `AddRateLimiter` com ADR própria.

## Prós e contras das opções

### A — rate-limiting no app

- Bom, porque a política fica versionada junto do código e pode ser fina por endpoint.
- Ruim, porque replica configuração transversal em cada módulo, com risco de divergência, e mistura preocupação operacional com domínio.

### B — rate-limiting na borda (escolhida)

- Bom, porque aplica política uniforme no ingresso único, mantém o app simples e cobre todos os módulos de uma vez.
- Ruim, porque a política não é visível no código do app e depende da configuração correta do gateway.

### C — ambos desde já

- Bom, porque soma defesa em profundidade.
- Ruim, porque adiciona complexidade no app antes de haver necessidade comprovada de política por endpoint — contraria o princípio de só introduzir o controle no app quando o gateway não bastar.

## Mais informações

- Aplica-se aos endpoints `[AllowAnonymous]` de reference data do módulo `Geo` ([ADR-0090](0090-modulo-geo-localidades.md)).
- Mitigações de caminho frio do lookup de CEP: índice de range das faixas (#704), cache-aside por selo de versão (#674) e memoização do selo em processo (#703).
- Origem: revisão do PR #701 (Story #676 — API de lookup de CEP).
