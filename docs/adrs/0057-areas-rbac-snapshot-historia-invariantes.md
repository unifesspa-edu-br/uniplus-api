---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0057: Sucessão parcial do RBAC por áreas

## Contexto e enunciado do problema

Esta ADR reuniu identidade institucional, autorização e governança temporal de itens compartilhados. Posteriormente, a [ADR-0077](0077-identidade-institucional-canonica-de-unidade.md) definiu a identidade canônica de `Unidade`, e a [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) definiu o modelo PBAC + ABAC para decisões contextuais de acesso.

O problema é manter as decisões temporais e de auditoria que ainda são necessárias, sem manter a ADR-0057 como fonte normativa para identidade ou para o modelo de autorização.

## Drivers da decisão

- **Fonte normativa clara**: identidade e autorização têm ADRs especializadas.
- **Reprodutibilidade**: fatos publicados e mudanças de governança preservam o contexto que vigorava no instante relevante.
- **Evolução segura**: novos casos não reutilizam o roster fechado nem a autorização por escopo de área.
- **Preservação operacional**: a sucessão do modelo de decisão não remove perfis existentes nem exige migrar endpoints nesta ADR.

## Opções consideradas

- **A**: manter integralmente o modelo original de áreas, papéis e governança.
- **B**: considerar toda a ADR superada, inclusive as decisões temporais.
- **C**: registrar sucessão parcial, preservando somente a governança temporal compatível.

## Resultado da decisão

**Escolhida:** C — sucessão parcial.

### Decisões sucedidas

- A identidade por `AreaCodigo` e o roster fechado são sucedidos pela ADR-0077.
- O modelo de decisão por papéis fixos e escopo de área para recursos protegidos é sucedido pela ADR-0078.

### Decisões que permanecem vigentes

- Fatos que precisam ser reproduzíveis preservam o estado de governança aplicável no instante da publicação ou do vínculo.
- Mudanças de responsabilidade e de associações institucionais preservam histórico temporal suficiente para reconstruir o contexto passado.
- Cache e auditoria podem conservar dados e fatos, mas não decidem nem filtram acesso.
- Operações públicas são declaradas explicitamente por seu contrato; decisões contextuais sobre recursos protegidos seguem a ADR-0078.

### Perfis existentes

Perfis e grupos já adotados pela plataforma, inclusive `plataforma-admin`, são preservados. Eles continuam a representar responsabilidades administrativas e podem compor a concessão avaliada pelo modelo de autorização. Esta ADR não remove perfis, não altera o provisionamento de identidade e não determina a migração de controllers ou contratos existentes.

### Limites da decisão

Esta ADR não define entidades, tabelas, interfaces, claims, rotas, filtros, políticas, cache, eventos ou algoritmos. Esses detalhes pertencem às issues de desenvolvimento e às ADRs especializadas.

## Consequências

### Positivas

- Identidade, decisão de acesso e governança temporal têm responsabilidades separadas.
- A auditoria histórica permanece preservada sem manter o modelo de áreas como regra atual.
- Perfis administrativos existentes continuam estáveis durante a evolução da autorização.

### Negativas

- Uma mudança futura de perfis, concessões ou endpoints deve ser tratada em issue própria, com a transição explicitada.

### Neutras

- A ADR-0057 permanece como referência histórica e normativa apenas para a governança temporal descrita acima.

## Confirmação

- Revisões arquiteturais verificam que novas referências institucionais usem a ADR-0077 e que novas decisões contextuais de acesso sigam a ADR-0078.
- Issues que evoluam perfis existentes explicitam seu impacto de compatibilidade antes da implementação.

## Mais informações

- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — bounded context da organização institucional.
- [ADR-0060](0060-junction-tables-por-entidade-com-view-unificada.md) — associações temporais por entidade.
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — referência cross-módulo por snapshot.
- [ADR-0077](0077-identidade-institucional-canonica-de-unidade.md) — identidade institucional canônica de `Unidade`.
- [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) — modelo PBAC + ABAC para decisões contextuais de acesso.
