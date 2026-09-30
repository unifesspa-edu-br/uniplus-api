---
status: "accepted"
date: "2026-09-29"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0138: Grupo repetível no formulário como fonte das instâncias de exigência

## Contexto e enunciado do problema

A habilitação do edital de Medicina 2027 pede a composição familiar como lista de membros, com parentesco, categoria de renda e condição de menor sob guarda de cada um, e exige documentos por membro (UNI-REQ-0146). A exigência repetida por entidade já existe (UNI-REQ-0069), mas os tipos de entidade e os seus atributos são fixos em código, e nada no formulário produz as instâncias.

## Drivers da decisão

- A lista de membros é coletada no formulário, com regras próprias por ocorrência.
- As exigências por membro precisam das ocorrências da lista, sem um mecanismo novo de exigência.
- O motor de regras resolve um valor por código de fato; uma regra do candidato não sabe escolher entre membros.

## Opções consideradas

- **Tipos de entidade fixos em código**, como hoje.
- **Grupo repetível no formulário como fonte das instâncias**, com agregados citáveis pelo candidato.

## Resultado da decisão

**Escolhida:** "Grupo repetível no formulário como fonte das instâncias", porque a mesma lista serve à coleta e à exigência por membro, e os agregados levam a informação da lista às regras do candidato sem ambiguidade.

- **Item de grupo repetível:** tem mínimo e máximo de ocorrências e não aninha outro grupo. Os subitens coletam fatos de escopo membro, e as suas regras citam fatos do candidato anteriores ou subitens anteriores da mesma ocorrência.
- **Fato de membro** nunca é citado fora do grupo. Fora dele, citam-se os **agregados** sobre a lista: `EXISTE` (booleano, "existe membro que...") e `VALORES_PRESENTES` (categórico multivalorado, "categorias presentes na família").
- **Exigência por membro:** as ocorrências do grupo são as instâncias da exigência repetida por entidade (UNI-REQ-0069), e o gatilho interno avalia os subitens de cada ocorrência. Os tipos de entidade e atributos fixos em código passam a vir do grupo. Não há mecanismo novo de exigência.
- **Pessoa jurídica vinculada** passa a ser um grupo de sistema.
- **Governança do agregado:** o agregado é calculado por mecanismo genérico do sistema (UNI-REQ-0075). O administrador o declara sobre o grupo que compõe, com o vínculo `AGREGACAO_GRUPO:`, o que resolve a exceção deixada em aberto pela ADR-0136.

### Emenda à ADR-0136

O vínculo `AGREGACAO_GRUPO:`, que a ADR-0136 deixou reservado, passa a ser aceito no fato derivado do administrador, ao lado de `REGRA_DERIVACAO:`, com as demais regras daquela ADR para os fatos do administrador, inclusive a de que o derivado nunca tem classificação mais fraca que a mais restritiva das suas dependências: um agregado sobre fato de membro sensível é sensível.

## Consequências

### Positivas

- A composição familiar e os documentos por membro saem da configuração, sem código por tipo de entidade.
- As regras do candidato continuam resolvendo um valor por fato, porque a lista entra nelas só pelos agregados.

### Negativas

- O avaliador de formulário passa a avaliar cada ocorrência, e a exigência passa a depender das ocorrências da lista.
- O grupo coleta dados de terceiros, como familiares e menores sob guarda, e a proteção desses dados segue a classificação dos fatos de membro.

## Confirmação

- Teste de domínio: lista abaixo do mínimo ou acima do máximo é recusada; grupo dentro de grupo é recusado; fato de membro citado fora do grupo é recusado, e os agregados são citáveis.
- Teste de domínio: a exigência repetida por membro é pedida uma vez por ocorrência que satisfaz o gatilho.

## Prós e contras das opções

### Tipos de entidade fixos em código

- Bom, porque já existe.
- Ruim, porque cada tipo novo de entidade exige código, e a lista não vem do formulário.

### Grupo repetível como fonte das instâncias

- Bom, porque uma única lista serve à coleta e à exigência.
- Ruim, porque o avaliador e a exigência passam a tratar ocorrências.

## Mais informações

- UNI-REQ-0069, UNI-REQ-0075 e UNI-REQ-0146.
- ADR-0135 (projeto compartilhado de regras), ADR-0136 (catálogo de fatos administrável) e ADR-0137 (formulários por finalidade).
