---
status: "proposed"
date: "2026-11-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Equipe de desenvolvimento do Uni+"
---

# ADR-0169: Catálogo de tipos de conceito da avaliação no módulo Configuração

## Contexto e enunciado do problema

A etapa de um processo seletivo é avaliada por nota numérica, mas há etapas cujo resultado é um conceito, como "apto" e "inapto". Hoje o vocabulário do conceito não existe como cadastro: cada etapa teria de carregar a própria escala, e o Seleção não teria de onde ler o significado de um valor, isto é, se ele conta como aprovação. O tipo de etapa (ADR-0123) não diz como suas etapas são avaliadas, e a forma e a regra de consolidação da avaliação (ADR-0150) pressupõem que o valor lançado tenha uma escala conhecida.

## Drivers da decisão

- Variável é configurável: o vocabulário do conceito é cadastro, não enum nem código escrito no fonte (ADR-0123).
- Um só lugar para a escala e para o significado de cada valor, lido pelo Seleção por porta e congelado por valor (ADR-0061).
- Recusas acumuladas num único `errors[]` (ADR-0125).
- Código de catálogo é chave natural imutável (ADR-0123).

## Opções consideradas

- **Catálogo de tipos de conceito em Configuração, com natureza numérica ou categórica.** Prós: uma fonte, vocabulário configurável, mesma forma dos demais catálogos. Contras: mais um cadastro.
- **Escala declarada em cada etapa.** Prós: sem cadastro novo. Contras: repete a escala em cada etapa e não dá significado único aos valores.
- **Enum fechado no Seleção.** Prós: simples. Contras: cada conceito novo exige código; contraria o princípio de configuração.

## Resultado da decisão

**Escolhida:** "catálogo de tipos de conceito em Configuração", porque a escala e o significado dos valores passam a ter uma fonte única e configurável.

1. Um **tipo de conceito** tem `codigo` imutável e único, inclusive entre desativados, `nome`, `descricao` opcional, `ativo` e uma **natureza**:
   - `numerico`: escala mínima, escala máxima e casas decimais (0 a 4);
   - `categorico`: no mínimo dois valores, cada um com `codigo`, `rotulo` e `contaComoAprovacao`.
   Os parâmetros da outra natureza são recusados.
2. O **tipo de etapa** declara os conceitos que admite (`conceitosAdmitidos`, por código). O vínculo só aceita tipo de conceito existente e ativo, sem repetição. O tipo de etapa admite, não impõe: quem diz o conceito de uma etapa concreta é quem monta o certame.
3. A leitura é pública e traz só os tipos ativos; criar, atualizar e desativar exige a permissão de manter tipos de conceito. Desativar impede novos vínculos, não libera o código: a edição do tipo de etapa recusa o conceito desativado que lê. Uma desativação que confirma entre essa leitura e a gravação do vínculo equivale a vincular e depois desativar, estado válido, pois desativar nunca remove os vínculos existentes.
4. O Seleção lê por porta e congela a cópia por valor. A nota máxima de uma etapa numérica não pode exceder a escala máxima do conceito declarado, conferência que fica no Seleção.
5. O catálogo tem dois tipos iniciais, `NOTA` (numérico) e `APTO_INAPTO` (categórico, com `APTO` contando como aprovação), e os vínculos com os tipos de etapa existentes. A carga inicial é um script SQL idempotente do módulo, que roda depois da migration (ADR-0170), e segue a regra de correção do catálogo da ADR-0112.

## Consequências

### Positivas

- A escala e o significado do valor têm um lugar só.
- Conceito novo é cadastro, sem mudança de código.

### Negativas

- Mais um cadastro a administrar.
- O Seleção precisa conferir a nota máxima contra a escala do conceito.

### Neutras

- A forma de avaliação e a regra de consolidação da etapa seguem como estão na ADR-0150; o conceito só descreve o valor lançado.

## Confirmação

- Testes de domínio das recusas por natureza e por valor categórico.
- Teste de arquitetura de código de erro mapeado e de classificação de proteção das colunas novas.
- Baseline OpenAPI regenerada com os endpoints do catálogo.

## Mais informações

- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md), [ADR-0112](0112-fronteira-append-only-do-catalogo-de-regras.md), [ADR-0123](0123-tipos-etapa-configuraveis.md), [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md), [ADR-0150](0150-parametros-de-avaliacao-da-etapa.md).

## Emenda 1 (2026-11-16) — a etapa declara o conceito entre os admitidos pelo tipo

**Contexto.** A decisão diz que o tipo de etapa admite conceitos e que quem monta o certame diz o conceito de cada etapa, mas não diz onde a etapa guarda essa escolha, quando ela é conferida nem o que um conceito categórico implica para a nota da etapa.

**Decisão.** A etapa declara um conceito, entre os que o tipo admite, e o copia por valor na declaração: código, nome, natureza e a escala ou os valores categóricos, na ordem do cadastro (ADR-0061). A admissão é conferida na declaração, e o snapshot do tipo de etapa não copia os conceitos admitidos. O conceito categórico não declara nota (UNI-REQ-0175): a etapa não declara nota máxima, nota mínima, peso, passo do lançamento, itens de avaliação nem escala de atendimento. No conceito numérico, a nota máxima da etapa não passa da escala máxima do conceito, e a gravação recusa o excesso. A etapa de nota do ENEM não declara conceito. Desativar o conceito no catálogo impede novas declarações e não invalida a etapa que já o copiou.

**Consequências.** A consequência neutra da decisão original, de que a regra de consolidação segue como está, deixa de valer: a regra de consolidação passa a depender da natureza do conceito (emenda da ADR-0150). A etapa sem conceito é estado legítimo em rascunho, e as recusas que dependem do conceito ficam com quem cobra a publicação. Cada código de recusa novo tem página no portal de desenvolvedores.
