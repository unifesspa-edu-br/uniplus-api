---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0084: Concessão excepcional e atuação institucional avaliadas no servidor

## Contexto e enunciado do problema

Além das permissões empacotadas em grupos ([ADR-0083](0083-grupos-oidc-governados-pela-aplicacao.md)), o Uni+ precisa de dois mecanismos de concessão fora do perfil padrão:

1. **Concessão excepcional** — conceder a um usuário específico uma permissão que seu grupo não dá, de forma **temporária** e **escopada** (a uma unidade, processo, chamada ou tipo de recurso). Exemplo: liberar, por um período, que um servidor auxilie em um processo específico de outra unidade.
2. **Atuação institucional** (sessão delegada) — permitir que um administrador de plataforma **opere em nome de uma unidade** por um motivo registrado e por tempo limitado, para resolver uma situação pontual.

A tentação é colocar essas concessões **no token** (via um mapeador do provedor de identidade). Isso é problemático: o token vive por um tempo e não reflete uma revogação imediata (token velho continua concedendo); acopla a decisão de autorização ao ciclo de emissão do token; e dificulta carregar o **escopo** da concessão.

O problema é conceder permissões fora do perfil padrão e operar sessão delegada de forma **temporal, escopada, revogável e auditável**, sem depender do conteúdo do token.

## Drivers da decisão

- **Revogação imediata** — retirar uma concessão deve ter efeito no ato, não ao expirar o token.
- **Escopo e validade explícitos** — toda concessão excepcional é limitada a um recurso e a um período.
- **Sessão delegada auditável** — quem atua em nome de quem, por quê e por quanto tempo, fica registrado.
- **Operações sensíveis sob dupla aprovação** — atuar em nome de outra unidade em operação sensível exige um segundo aprovador.

## Opções consideradas

- **A**: Injetar a concessão excepcional **no token**, via mapeador do provedor de identidade.
- **B**: **Concessão excepcional e atuação institucional como entidades no servidor**, consultadas na decisão de acesso, com escopo e validade obrigatórios e dupla aprovação para operações sensíveis.
- **C**: Conceder via um **grupo OIDC temporário** criado e removido no provedor.

## Resultado da decisão

**Escolhida:** "B — concessões avaliadas no servidor", porque é o único caminho que dá revogação imediata, escopo explícito e auditoria, sem prender a decisão ao ciclo do token.

- A **concessão excepcional** é registrada no servidor com: a permissão concedida, um **escopo obrigatório** (unidade, processo, chamada ou tipo de recurso — ao menos um), uma **validade obrigatória** (com teto), a justificativa e a aprovação. É **consultada na decisão de acesso** (modelo da ADR-0078), não embutida no token; a revogação tem **efeito imediato**.
- A **atuação institucional** (sessão delegada) é registrada no servidor com: quem atua, em nome de qual unidade, o motivo, o instante de início e o de expiração (com teto). A decisão de acesso considera a atuação ativa como contexto.
- **Operações sensíveis em sessão delegada exigem dupla aprovação**: a operação só prossegue com um segundo aprovador distinto do solicitante.
- A **concessão excepcional** é uma **fonte de concessão efetiva** considerada pelo ponto de decisão, com **origem rastreável** (a decisão registra de qual fonte veio a autorização). A **atuação institucional** é considerada **contexto** de sessão delegada (não é fonte de concessão efetiva); seu uso fica rastreável na trilha de auditoria.

## Consequências

### Positivas

- Revogação tem efeito imediato — não há janela de token velho concedendo acesso retirado.
- Toda concessão fora do padrão tem escopo e validade explícitos e aprovação registrada.
- A sessão delegada é auditável (quem, em nome de quem, por quê, por quanto tempo).
- Operações sensíveis delegadas ficam protegidas por dupla aprovação.

### Negativas

- A decisão consulta o servidor para essas concessões; a estratégia que concilia essa consulta com desempenho e revogação imediata é decidida em ADR própria desta frente.
- Há um processo de aprovação (e, no caso sensível, de dupla aprovação) a operar.

### Neutras

- A forma concreta das entidades (campos, constraints de escopo e validade, consumo atômico da dupla aprovação) é detalhada na spec de implementação; esta ADR fixa que essas concessões são **avaliadas no servidor**, **escopadas**, **temporais** e **revogáveis no ato**.

## Confirmação

- **Teste de escopo e validade**: uma concessão excepcional sem escopo é rejeitada; uma concessão expirada não concede.
- **Teste de revogação**: revogar uma concessão tem efeito na decisão seguinte (sem janela de token velho).
- **Teste de dupla aprovação**: uma operação sensível em sessão delegada sem um segundo aprovador distinto é negada.

## Prós e contras das opções

### A — Concessão excepcional no token

- Bom, porque a decisão lê tudo do token, sem consulta adicional.
- Ruim, porque o token vive por um tempo (revogação não é imediata), acopla a autorização ao ciclo de emissão e dificulta carregar o escopo da concessão.

### B — Concessões avaliadas no servidor (escolhida)

- Bom, porque dá revogação imediata, escopo e validade explícitos e auditoria.
- Ruim, porque consulta o servidor na decisão e exige processo de aprovação.

### C — Grupo OIDC temporário

- Bom, porque reaproveita o mecanismo de grupos.
- Ruim, porque cria e remove grupos para concessões efêmeras (poluindo o provedor), não carrega escopo fino por recurso e mistura concessão pontual com perfil administrativo estável.

## Mais informações

- Ancora no modelo de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): a concessão excepcional é fonte de concessão efetiva e a atuação institucional é contexto de sessão delegada, ambas com origem/uso rastreável.
- Contrasta com os grupos da [ADR-0083](0083-grupos-oidc-governados-pela-aplicacao.md): estas concessões são **dinâmicas** e **não** viram grupo no provedor.
- O **cache e a revogação imediata** (como o efeito imediato é garantido sob cache) são decididos em ADR própria desta frente.
- A **trilha de auditoria** que registra o uso dessas concessões é decidida em ADR própria desta frente.
