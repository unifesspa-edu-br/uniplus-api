---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0085: Cache de decisão e revogação diferenciados por sensibilidade

## Contexto e enunciado do problema

A decisão de acesso ([ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md)) consulta as concessões do solicitante, parte delas avaliada no servidor ([ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md)). Consultar o estado de concessões a cada decisão tem custo; um cache reduz esse custo. Mas o cache cria uma **janela** entre o que ele guarda e uma **revogação**: depois que uma concessão é retirada, um acesso a dado sensível **não pode** ser autorizado por um cache desatualizado.

Tratar todas as decisões com o mesmo cache força uma escolha ruim: ou o cache é tão curto que perde a utilidade, ou é longo o bastante para autorizar, por instantes, um acesso já revogado a dado sensível. O problema é equilibrar desempenho e revogação imediata **de acordo com a sensibilidade** do que está sendo acessado, e degradar de forma segura quando o mecanismo de revogação falha.

## Drivers da decisão

- **Revogação com efeito imediato** para dado sensível e exportação.
- **Desempenho aceitável** no caso comum (dado público/interno).
- **Degradação segura** (fail-closed): se o mecanismo que propaga revogações atrasar, o sistema fica mais restritivo, não mais permissivo.

## Opções consideradas

- **A**: Cache uniforme com tempo de vida fixo para toda decisão.
- **B**: **Cache diferenciado por sensibilidade** — público/interno com tempo de vida curto; pessoal com tempo de vida muito curto; sensível, exportação e dupla aprovação **sem cache** (consulta direta); uma marca de revogação versionada para invalidar; **fail-closed** quando a propagação de revogações atrasa.
- **C**: Sem cache — consulta direta sempre.

## Resultado da decisão

**Escolhida:** "B — cache diferenciado por sensibilidade com fail-closed", porque ajusta o risco à sensibilidade: economiza no caso comum e nunca deixa o cache autorizar um acesso sensível já revogado.

- O cache da decisão é **diferenciado pela sensibilidade** da permissão: dado **público/interno** tolera um tempo de vida curto; dado **pessoal** usa um tempo de vida muito curto; dado **sensível**, **exportação** e operações de **dupla aprovação** **não usam cache** — a decisão consulta o estado diretamente.
- Uma **marca de revogação versionada** registra que as concessões de um sujeito (ou de um grupo, ou de uma concessão específica) mudaram; a decisão compara a versão em cache com a versão corrente e invalida o cache quando divergem.
- O mecanismo que propaga revogações é **monitorado**. Se o seu atraso ultrapassar um limite, o sistema entra em **modo de segurança elevada**: as decisões sobre dado sensível passam a consultar diretamente e as **exportações são negadas** até a recuperação. A degradação é **fail-closed** — sob incerteza, nega-se.

## Consequências

### Positivas

- Revogação tem efeito imediato onde mais importa (dado sensível, exportação), sem janela de cache.
- O caso comum mantém desempenho com cache curto.
- A falha do mecanismo de revogação leva a negar, não a liberar — postura segura.

### Negativas

- O cache diferenciado é mais complexo que um tempo de vida único.
- Exige monitorar o mecanismo de propagação de revogações e operar o modo de segurança elevada.

### Neutras

- Os tempos de vida concretos por sensibilidade e os limites de atraso são parâmetros de operação ajustados na implementação; esta ADR fixa a **política** (diferenciar por sensibilidade; sensível sem cache; fail-closed).

## Confirmação

- **Teste de revogação imediata**: revogar uma concessão e, em seguida, acessar uma permissão sensível resulta em negação (a decisão não usa cache para sensível).
- **Teste de fail-closed**: com o mecanismo de propagação de revogações parado além do limite, uma exportação é negada até a recuperação.

## Prós e contras das opções

### A — Cache uniforme com tempo de vida fixo

- Bom, porque é simples.
- Ruim, porque obriga a escolher entre cache inútil (curto demais) e janela insegura para dado sensível (longo demais).

### B — Cache diferenciado por sensibilidade com fail-closed (escolhida)

- Bom, porque ajusta o risco à sensibilidade e nunca autoriza acesso sensível por cache velho.
- Ruim, porque é mais complexo e exige monitorar o mecanismo de revogação.

### C — Sem cache

- Bom, porque a revogação é sempre imediata.
- Ruim, porque consulta o estado a cada decisão, inclusive no caso comum de dado público/interno, com custo desnecessário.

## Mais informações

- Ancora no modelo de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): o cache e a revogação operam sobre a decisão de acesso.
- A sensibilidade de cada permissão vem do catálogo de permissões ([ADR-0080](0080-catalogo-declarativo-de-permissoes-e-codegen.md)).
- As concessões cujo efeito de revogação esta ADR garante são as da [ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md).
