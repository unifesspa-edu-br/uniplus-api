---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0079: Hierarquia institucional sem herança de permissão

## Contexto e enunciado do problema

A estrutura organizacional da Unifesspa é hierárquica: a instituição tem pró-reitorias e centros como filhos diretos, e estes têm divisões, coordenações e núcleos abaixo. No modelo de dados, cada `Unidade` aponta para a sua unidade superior (`UnidadeSuperiorId`).

A [ADR-0055](0055-organizacao-institucional-bounded-context.md) tratou a PROEG como **supervisora** sobre o CEPS e o CRCA, e a [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) modelou visibilidade entre Unidades. Isso deixa em aberto uma pergunta de autorização: **estar acima na hierarquia institucional concede, automaticamente, acesso ao que está abaixo?**

Estrutura e autoridade de acesso são eixos distintos. Uma pró-reitoria figurar como superior de um centro descreve a organização administrativa; não significa que ela opere — nem deva enxergar por padrão — os dados operacionais daquele centro (inscrições, documentos de candidatos, pareceres de banca). Se a hierarquia propagasse permissão, três efeitos indesejados surgiriam: (1) acesso amplo viraria efeito colateral da posição na árvore, não uma decisão; (2) a auditoria não conseguiria atribuir "quem podia ver isto" a uma concessão explícita; (3) reorganizar a árvore (mudar o pai de uma Unidade por portaria) concederia ou removeria acessos silenciosamente.

O problema é decidir se a hierarquia institucional propaga permissão por herança, ou se o acesso é sempre concessão explícita.

## Drivers da decisão

- **Menor privilégio**: acesso amplo deve ser concessão deliberada, não consequência da posição hierárquica.
- **Auditabilidade**: "quem podia ver ou editar isto, e por quê" deve resolver para uma concessão explícita, não para a topologia da árvore.
- **Realidade institucional**: supervisão administrativa não equivale a acesso operacional aos dados; a PROEG não conduz o backlog diário do CEPS.
- **Sem escalonamento implícito**: reorganização institucional (mudança de unidade superior) não pode alterar acessos sem um ato explícito.

## Opções consideradas

- **A**: Herança por hierarquia — a unidade superior herda o acesso das inferiores (interpretação da supervisão da ADR-0055).
- **B**: **Sem herança** — unidades são irmãs para fins de autorização; visibilidade ampla exige um escopo de auditoria explícito, formal e temporal.
- **C**: Herança opcional, configurável por unidade.

## Resultado da decisão

**Escolhida:** "B — sem herança de permissão por hierarquia", porque preserva o menor privilégio, mantém a autorização auditável a concessões explícitas e desacopla a estrutura institucional da autoridade de acesso.

A hierarquia (`UnidadeSuperiorId`) descreve a estrutura organizacional e **não** propaga permissão nem visibilidade. Para fins de autorização, **PROEG, CEPS e CRCA são unidades irmãs** (filhas diretas da instituição), não mãe e filhas. Nenhuma unidade enxerga ou administra o que pertence a outra por estar acima dela na árvore.

A visibilidade ampla legítima — por exemplo, uma pró-reitoria acompanhar o que ocorre nas unidades a ela vinculadas — é concedida por um **escopo de auditoria explícito**: uma concessão formal, **temporal** (com validade), com **base legal** registrada, materializada na entidade de escopo de auditoria e exercida por permissões de auditoria (`:auditar`). Não é um efeito da hierarquia; é um ato de concessão rastreável.

A resolução de acesso, portanto, **não consulta `UnidadeSuperiorId`** para conceder. A hierarquia permanece disponível para apresentação e navegação institucional, não para decisão de autorização.

## Consequências

### Positivas

- Menor privilégio por padrão; nenhum acesso amplo surge como efeito colateral da árvore.
- Toda visibilidade entre unidades é rastreável a uma concessão explícita, temporal e fundamentada — auditoria responde "por quê".
- Reorganização institucional (mudar a unidade superior) não altera acessos silenciosamente.
- Estrutura e autoridade ficam desacopladas; a árvore pode evoluir sem consequências de segurança implícitas.

### Negativas

- Visibilidade ampla exige um ato explícito (escopo de auditoria), em vez de "vir de graça" pela posição — passo administrativo a mais.
- Contraintuitivo para quem espera que "a unidade superior vê tudo o que está abaixo".

### Neutras

- A hierarquia continua modelada e útil para apresentação/navegação; apenas não participa da decisão de acesso.

## Confirmação

- **Fitness test**: a resolução de autorização não usa `UnidadeSuperiorId` para conceder acesso; toda visibilidade entre unidades passa por um escopo de auditoria ativo.
- **Golden authorization test**: uma unidade superior **não** enxerga, por padrão, recursos de uma unidade subordinada; passa a enxergar somente mediante escopo de auditoria válido e dentro da validade.

## Prós e contras das opções

### A — Herança por hierarquia

- Bom, porque é simples e intuitivo ("o superior vê tudo").
- Ruim, porque viola o menor privilégio, torna o acesso não auditável a uma concessão explícita e faz a reorganização da árvore alterar acessos silenciosamente.

### B — Sem herança; visibilidade por escopo de auditoria explícito (escolhida)

- Bom, porque preserva o menor privilégio, mantém a autorização auditável e desacopla estrutura de autoridade.
- Ruim, porque exige um ato explícito para conceder visibilidade ampla.

### C — Herança opcional configurável

- Bom, porque permite ajustar caso a caso.
- Ruim, porque reintroduz a herança como caminho de acesso (com os mesmos riscos de auditoria e escalonamento implícito), agora de forma inconsistente entre unidades.

## Mais informações

- **Refina** a [ADR-0055](0055-organizacao-institucional-bounded-context.md): a relação de **supervisão** da PROEG sobre CEPS/CRCA, ali enunciada, é revista — para fins de autorização essas unidades são **irmãs**; supervisão administrativa não implica acesso operacional.
- A visibilidade entre Unidades, que o modelo anterior ([ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md)) — superseded pela [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) — tratava como propriedade dos papéis, passa a depender de concessão de auditoria explícita.
- Depende do modelo de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) (a concessão de auditoria é uma das fontes consideradas pelo ponto de decisão).
- A entidade de escopo de auditoria, sua expiração e seus índices são detalhados nas specs de implementação desta frente; esta ADR fixa apenas o princípio de **não herança**.
