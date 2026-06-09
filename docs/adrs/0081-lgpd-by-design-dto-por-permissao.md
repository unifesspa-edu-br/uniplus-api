---
status: "accepted"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Encarregada de Proteção de Dados (DPO) — validada pelo Parecer Técnico 002/2026 (08/06/2026)"
informed:
  - "Equipe Uni+"
---

# ADR-0081: LGPD-by-design — projeção por permissão como controle primário de proteção de dado pessoal

## Contexto e enunciado do problema

O Uni+ trata dados pessoais e sensíveis de candidatos: CPF, raça/cor, condição de deficiência, renda familiar, entre outros. As respostas da API que carregam esses dados precisam expor **apenas o que a permissão do solicitante autoriza** — um resultado público não pode conter CPF nem raça/cor; um perfil operacional vê os campos que sua permissão comporta; um auditor vê sob escopo e base legal.

A tentação é resolver isso com **mascaramento na serialização**: um conversor que, ao escrever o JSON, substitui o valor de um campo sensível por uma máscara quando o solicitante não tem acesso. Esse mecanismo é **frágil como controle primário**: é contornado por uma projeção customizada, por *streaming*, por exportação, por um objeto aninhado, por um novo endpoint que serializa de outra forma. Quando o controle de proteção depende de um passo opcional na borda de saída, qualquer caminho que não passe por ele vaza o dado.

O problema é garantir, por construção, que cada resposta exponha somente os campos que a permissão autoriza, com a classificação e a base legal de cada dado conhecidas.

## Drivers da decisão

- **LGPD-by-design**: cada campo pessoal/sensível tem classificação (público, interno, pessoal, sensível) e base legal; a decisão de exposição é parte do desenho, não um filtro de saída.
- **Controle primário robusto** — não contornável por caminhos alternativos de serialização.
- **Defesa em profundidade** — um segundo mecanismo (mascaramento) cobre o que escapar do primeiro.
- **Verificabilidade** — é possível testar que um DTO público não vaza dado pessoal.

## Opções consideradas

- **A**: Mascaramento na serialização como controle primário (um conversor que mascara campos conforme o solicitante).
- **B**: **Projeção por permissão** (DTO específico por permissão) como controle primário; mascaramento como defesa secundária; classificação e base legal por campo; testes de exposição (BOPLA).
- **C**: Filtro genérico por papel aplicado a uma entidade única de saída.

## Resultado da decisão

**Escolhida:** "B — projeção por permissão como controle primário", porque o que não é projetado não pode vazar: o controle vive no formato da resposta, não num passo opcional de saída.

Cada permissão que retorna dado pessoal tem um **DTO específico**, projetado no servidor, contendo **apenas** os campos que aquela permissão autoriza. Um resultado público projeta um DTO sem CPF, sem raça/cor, sem identificador interno; um perfil operacional projeta um DTO com os campos que sua permissão comporta; um auditor projeta um DTO sob escopo de auditoria e base legal. A projeção é o **controle primário**.

Como **defesa secundária**, prevê-se um **mascaramento na serialização da resposta**: um campo classificado que, por engano, chegue a um DTO indevido seria mascarado na saída. É rede de segurança, não o controle primário — e é **distinto** do mascaramento de dados pessoais já aplicado aos logs (que permanece em vigor).

Cada campo de dado pessoal/sensível carrega sua **classificação** e a **base legal** de tratamento. Permissões que retornam dado sensível exigem base legal aplicável na decisão de acesso (modelo da ADR-0078).

A conformidade é verificada por **testes de exposição** (Broken Object Property Level Authorization): para cada DTO com dado pessoal, um teste confirma que a serialização para um solicitante sem acesso não contém o campo protegido — incluindo o teste da projeção real (não apenas do tipo).

## Consequências

### Positivas

- O dado não projetado não vaza — o controle é estrutural, não um passo opcional na borda.
- Classificação e base legal por campo tornam o inventário de tratamento derivável e auditável.
- A defesa em profundidade (projeção + mascaramento) cobre erro humano.
- A exposição é testável por DTO, com prova negativa (não vaza) e contraprova positiva (projeta o permitido).

### Negativas

- Mais DTOs a manter (um por cenário de permissão), em vez de uma entidade de saída única.
- Disciplina exigida: criar um DTO específico para cada nova permissão que retorna dado pessoal.

### Neutras

- A forma concreta dos atributos de classificação e do conversor de mascaramento evolui com a frente; esta ADR fixa a **ordem de precedência** (projeção primeiro, mascaramento depois), não o detalhe de cada atributo.

## Confirmação

- **Fitness test**: nenhum DTO marcado como público contém campo classificado como pessoal/sensível; todo campo sensível tem base legal declarada.
- **Golden BOPLA test por DTO**: a serialização para um solicitante sem acesso não contém o campo protegido, validando a projeção real (não só o tipo declarado).

## Prós e contras das opções

### A — Mascaramento na serialização como controle primário

- Bom, porque é centralizado num único conversor.
- Ruim, porque é contornável por projeção customizada, streaming, exportação ou objeto aninhado; um caminho que não passe pelo conversor vaza o dado.

### B — Projeção por permissão como controle primário (escolhida)

- Bom, porque o dado não projetado não pode vazar; classificação e base legal ficam explícitas; é testável.
- Ruim, porque exige um DTO por cenário de permissão e disciplina para mantê-los.

### C — Filtro genérico por papel sobre entidade única

- Bom, porque há um só tipo de saída.
- Ruim, porque o filtro por papel não acompanha a granularidade real (a exposição depende de permissão + contexto, não só do papel) e reincide na fragilidade do controle de saída.

## Mais informações

- Ancora no modelo de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): a permissão determina qual DTO é projetado e se a base legal é exigida.
- Relaciona-se com a [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) (sem dado pessoal em URL) e a [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) (trilha forense).
- A classificação específica do **nome social e do nome civil** (quando cada um é público ou pessoal) é decidida na ADR seguinte desta frente, por tocar dignidade e legislação própria.
- A base legal de cada tratamento e a classificação de cada dado foram **validadas pela Encarregada de Proteção de Dados (DPO)** da instituição — Parecer Técnico 002/2026 (08/06/2026), fundamentado nos arts. 7º II, 7º III e 23 da LGPD.
