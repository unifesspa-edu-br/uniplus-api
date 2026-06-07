---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0086: Trilha de auditoria de autorização com integridade verificável

## Contexto e enunciado do problema

As decisões de autorização sobre dados pessoais e sensíveis — visualizar um detalhe sensível, listar um conjunto, exportar, gerar relatório — precisam de uma **trilha de auditoria**. Essa trilha responde, depois do fato, "quem acessou o quê, quando, sob qual base legal e com qual resultado", e serve como prova em situações de fiscalização ou litígio.

Para ter valor probatório, a trilha precisa ser **íntegra**: uma alteração posterior de um registro deve ser detectável. Um *hash* simples do registro não basta — qualquer um que consiga reescrever o registro reescreve também o *hash*. Além disso, a verificação precisa continuar possível **depois** de a chave usada ter sido rotacionada.

O problema é registrar a trilha de auditoria de autorização de forma **íntegra e verificável ao longo do tempo**, sem expor dados pessoais na própria trilha nem na observabilidade.

## Drivers da decisão

- **Integridade verificável** — adulteração de um registro deve ser detectável.
- **Verificação após rotação de chave** — registros antigos continuam verificáveis.
- **Append-only** — a trilha é forense; não se sobrescreve nem se apaga.
- **Sem dado pessoal** na trilha estruturada e na observabilidade.

## Opções consideradas

- **A**: *Hash* simples (por exemplo, SHA-256) do conteúdo do registro.
- **B**: **Código de autenticação de mensagem (HMAC)** com chave rotacionável guardada em cofre, calculado sobre uma forma canônica determinística do conteúdo, com a versão da chave registrada em cada entrada; trilha append-only.
- **C**: Sem proteção de integridade — confiar apenas no controle de acesso ao banco.

## Resultado da decisão

**Escolhida:** "B — HMAC com chave em cofre, rotacionável e versionada", porque um *hash* simples não autentica (qualquer reescrita do registro reescreve o *hash*), enquanto um código de autenticação com chave secreta torna a adulteração detectável e a verificação reproduzível ao longo do tempo.

- Cada registro de auditoria de autorização carrega um **código de autenticação de mensagem (HMAC)** calculado sobre uma **forma canônica determinística** do seu conteúdo (uma serialização estável, para que o mesmo conteúdo produza sempre o mesmo código).
- A chave do código é guardada em **cofre** e é **rotacionável**. Cada registro guarda a **versão da chave** usada, de modo que um registro antigo permanece verificável com a versão correspondente, mesmo após a rotação.
- A trilha é **append-only** ([ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md)): correções entram como um **novo fato**, nunca sobrescrevem ou apagam um registro anterior.
- A **observabilidade** (logs, métricas, painéis) da autorização **não** expõe dados pessoais; registra eventos e identificadores não sensíveis.

## Consequências

### Positivas

- Adulteração de um registro é detectável — a trilha tem valor probatório.
- Registros antigos continuam verificáveis após a rotação de chave (a versão é registrada).
- A natureza append-only preserva o histórico íntegro.
- A ausência de dado pessoal na trilha/observabilidade reduz a superfície de exposição.

### Negativas

- Exige gestão de chaves no cofre (rotação, retenção das versões para verificação).
- A forma canônica determinística do conteúdo precisa ser mantida estável ao longo do tempo.

### Neutras

- O algoritmo concreto, a periodicidade de rotação e a retenção das versões de chave são parâmetros de operação detalhados na implementação; esta ADR fixa o **mecanismo** (código de autenticação com chave em cofre, rotacionável, versionada por registro, sobre forma canônica, em trilha append-only).

## Confirmação

- **Teste de integridade**: alterar um registro faz a verificação do código de autenticação falhar.
- **Teste de rotação**: um registro gravado antes de uma rotação permanece verificável com a versão de chave correspondente.
- **Teste de privacidade**: nenhum dado pessoal aparece na trilha estruturada nem nos painéis/logs de observabilidade.

## Prós e contras das opções

### A — *Hash* simples do registro

- Bom, porque é trivial de calcular.
- Ruim, porque não autentica: quem reescreve o registro reescreve o *hash*; não detecta adulteração por quem tem acesso de escrita.

### B — Código de autenticação com chave em cofre, rotacionável e versionada (escolhida)

- Bom, porque detecta adulteração e permite verificação reproduzível ao longo do tempo, inclusive após rotação.
- Ruim, porque exige gestão de chaves e uma forma canônica estável.

### C — Sem proteção de integridade

- Bom, porque não há nada a manter.
- Ruim, porque a trilha perde valor probatório: nada distingue um registro íntegro de um adulterado.

## Mais informações

- A trilha é forense e append-only conforme a [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md).
- Registra o uso das concessões da [ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md) e o resultado das decisões da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md), sem expor dado pessoal.
- A verificação de integridade dos registros (recálculo do código com a versão de chave registrada) é uma capacidade de uso interno da auditoria, detalhada na spec de implementação.
