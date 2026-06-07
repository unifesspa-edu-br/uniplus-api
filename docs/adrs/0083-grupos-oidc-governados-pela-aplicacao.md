---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0083: Grupos OIDC governados pela aplicação, com marca de propriedade e sincronização não-destrutiva

## Contexto e enunciado do problema

As permissões do Uni+ são empacotadas em **perfis administrativos**. O provedor de identidade (OIDC) organiza os usuários em **grupos**, e cada grupo, ao ser atribuído a um usuário, deve conceder um conjunto de permissões. Surgem duas perguntas: **quem é a fonte da verdade sobre o que cada grupo concede** e **como a aplicação provisiona e mantém esses grupos no provedor sem causar dano**.

O provedor de identidade do Uni+ é compartilhado com outros sistemas institucionais. Hoje são poucos sistemas e poucos perfis, de modo que o impacto de um descuido seria limitado. Ainda assim, um processo de sincronização ingênuo — que liste todos os grupos do provedor e os ajuste ao estado desejado da aplicação — poderia **criar, renomear ou apagar grupos que pertencem a outros sistemas**. É um erro evitável com pouco esforço, e a proteção deve estar pronta antes de o número de sistemas e perfis crescer.

O problema é a aplicação governar **apenas os grupos que ela define**, mantendo o vínculo grupo→permissões, sem jamais tocar o que não é seu.

## Drivers da decisão

- **Fonte da verdade na aplicação**: o que cada grupo concede é definido pela aplicação (registro no banco); o provedor reflete.
- **Provider-agnostic**: o vocabulário de domínio não embute o nome do produto de identidade; os caminhos de grupo seguem o *slug* da Unidade.
- **Propriedade explícita**: a aplicação só opera grupos marcados como dela.
- **Não destrutivo por padrão**: a sincronização observa e relata; qualquer alteração exige um plano aprovado.
- **Isolamento em provedor de identidade compartilhado**: grupos de outros sistemas são invisíveis ao processo de sincronização.

## Opções consideradas

- **A**: Sincronização automática bidirecional — a aplicação cria, renomeia e remove grupos no provedor para casar com o estado desejado.
- **B**: **Vínculo no banco** (registro que define o que cada grupo concede) + **marca de propriedade** em cada grupo provisionado + **filtro de prefixo e de propriedade** + **sincronização somente-leitura por padrão**, com alteração apenas via plano aprovado.
- **C**: Grupos apenas no provedor, sem vínculo no banco.

## Resultado da decisão

**Escolhida:** "B — grupos governados por vínculo no banco, com marca de propriedade e sincronização não-destrutiva", porque mantém a aplicação como fonte da verdade sobre as concessões e impede, por construção, que a sincronização toque grupos que não são da aplicação no provedor compartilhado.

- A aplicação mantém um **vínculo** que define, para cada grupo, o conjunto de permissões concedidas, o perfil e a Unidade associada. Esse vínculo é a fonte da verdade; o provedor reflete.
- Os **caminhos de grupo** seguem o *slug* da Unidade, num espaço de nomes próprio da aplicação (prefixo dedicado), de forma independente do produto de identidade.
- Cada grupo provisionado pela aplicação carrega uma **marca de propriedade** (um atributo que identifica a aplicação como dona).
- O processo de **sincronização opera somente sobre grupos** que satisfaçam, ao mesmo tempo, o **prefixo** da aplicação **e** a **marca de propriedade**. Qualquer grupo fora desses dois filtros é **invisível**: não é listado, não é alterado, não é renomeado, não é removido, nem reportado como divergência.
- O **modo padrão é somente-leitura**: a sincronização observa e relata diferenças. Criar, renomear ou remover grupos exige um **plano aprovado** explicitamente. Uma colisão de caminho com um grupo sem a marca de propriedade é **bloqueada** (nunca sobrescrita), com alerta.
- Concessões **dinâmicas** (por processo, edital, banca) **não** viram grupo no provedor — seu tratamento é decidido em ADR própria desta frente.

## Consequências

### Positivas

- A possibilidade de a sincronização tocar grupos de outros sistemas é eliminada por construção (filtro de prefixo + propriedade).
- A aplicação é a fonte auditável do que cada grupo concede; o provedor é um reflexo.
- O vocabulário permanece independente do produto de identidade.
- A sincronização não-destrutiva por padrão evita remoções acidentais.

### Negativas

- Operações de alteração de grupo exigem um plano aprovado — passo a mais, deliberado.
- A marca de propriedade e o vínculo no banco são estado adicional a manter.

### Neutras

- A forma concreta do registro de vínculo, do atributo de propriedade e do fluxo de aprovação do plano é detalhada na spec de implementação; esta ADR fixa o **modelo de governança** (fonte na aplicação, propriedade, filtro, não-destrutivo).

## Confirmação

- **Teste de isolamento**: um grupo fora do prefixo/propriedade da aplicação nunca entra no plano de sincronização; em modo de aplicação, um plano que o contenha aborta (defesa em profundidade).
- **Teste de não-destrutividade**: o modo padrão lista zero ações de escrita; criar/renomear/remover só ocorre com plano aprovado; colisão de caminho sem marca de propriedade é bloqueada.

## Prós e contras das opções

### A — Sincronização automática bidirecional

- Bom, porque mantém o provedor sempre alinhado sem intervenção.
- Ruim, porque, num provedor compartilhado, pode criar/renomear/remover grupos de outros sistemas — impacto hoje limitado pelo número pequeno de sistemas, mas indesejável e facilmente evitável.

### B — Vínculo + marca de propriedade + sincronização não-destrutiva (escolhida)

- Bom, porque isola o que é da aplicação, mantém a fonte da verdade no banco e evita dano a terceiros.
- Ruim, porque exige plano aprovado para alterações e estado adicional (vínculo + marca).

### C — Grupos só no provedor, sem vínculo no banco

- Bom, porque não há estado duplicado.
- Ruim, porque a aplicação perde a fonte auditável do que cada grupo concede e a junção com metadados de perfil/Unidade para relatórios e decisões.

## Mais informações

- Ancora no modelo de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): um grupo é uma das fontes de concessão efetiva consideradas na decisão.
- O **manifesto de provisionamento** dos grupos é um dos artefatos gerados a partir do catálogo de permissões ([ADR-0080](0080-catalogo-declarativo-de-permissoes-e-codegen.md)).
- A hierarquia institucional **não** se reflete em grupos aninhados ([ADR-0079](0079-hierarquia-institucional-sem-heranca-de-permissao.md)): a árvore de unidades não propaga concessão.
- As concessões contextuais (excepcional, sessão delegada, equipes e bancas) são decididas em ADR própria desta frente e **não** viram grupo no provedor.
