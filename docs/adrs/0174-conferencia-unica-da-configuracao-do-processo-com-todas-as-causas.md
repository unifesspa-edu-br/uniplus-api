---
status: "proposed"
date: "2026-11-16"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0174: Conferência única da configuração do processo, com todas as causas

## Contexto e enunciado do problema

A conferência de que um processo pode ser congelado era uma cadeia de verificações que devolvia a primeira causa. O painel de conformidade, a retificação e a publicação tinham listas próprias de regras, com ordens e textos que podiam divergir. O diagnóstico precisa mostrar todas as causas independentes de uma vez ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)) e as declarações parciais já produzem recusas próprias ([ADR-0173](0173-regras-da-configuracao-sobre-declaracoes-parciais.md)).

## Drivers da decisão

- Uma ordem única de conferência para gravação, painel e congelamento.
- Todas as causas independentes numa só resposta, sem causa duplicada nem exceção.
- Uma regra só deixa de avaliar a unidade cujo dado foi recusado; o item que não pôde ser verificado nunca fica aprovado por omissão.
- O agregado não guarda um estado derivado da própria conferência.

## Opções consideradas

- Manter as cadeias de primeira causa e acrescentar os assuntos novos ao lado.
- Conferir a configuração por acumulação, com uma configuração apta a congelar que só existe sem causas.

## Resultado da decisão

**Escolhida:** "Conferir a configuração por acumulação, com uma configuração apta a congelar que só existe sem causas", porque é a única forma de dar a mesma ordem e o mesmo texto a todos os consumidores.

1. A configuração apta a congelar só existe quando a conferência não tem causa. A conferência devolve a configuração apta ou o conjunto de causas, nunca os dois, e a configuração apta carrega apenas o identificador do processo.
2. Os cadastros que a conferência lê (catálogos, referências, tipos de documento e fatos protegidos) entram como um único valor, lidos uma vez, num instante único, pela camada de aplicação. O domínio não lê porta. Os motivos de decisão escolhidos entram nesse valor com a situação do motivo de origem no cadastro e o número da versão que nasce: o processo congela a cópia feita na escolha, e a conferência só lê do cadastro se o motivo continua ativo, sem refazer a cópia.
3. A ordem dos assuntos de regra é única: identificação, conformidade por item, declarações, conjunto, completude, procedimentos de recurso, bancas, correção independente, tipos de ato, cascata, as condições que a canonicalização exige e os motivos de decisão escolhidos. No assunto dos motivos, a ordem é o motivo desativado no cadastro, o motivo de um tipo que a fase ou a etapa não declara e a cobertura por lugar; o motivo recusado por uma das duas primeiras não conta para a cobertura. Quando a leitura recusou uma fase ou etapa inteira, ou a lista dos tipos de decisão de uma delas, não se sabe quais lugares declaram um tipo: o assunto não recusa o motivo desse lugar nem aponta que nenhum lugar declara o tipo, e os itens do painel que dependem disso ficam pendentes. Cada assunto é uma função que devolve todas as suas causas, e a ordem é a da concatenação delas em `CausasQueImpedemCongelar`; uma regra que lê o dado que outra recusou deixa de avaliar dentro do próprio predicado. A conferência do estado (status, motivo, versão anterior) é conflito, e não causa da configuração: fica fora dessa ordem, em `OrigemDoCongelamento`, e impede todo o resto quando recusa.
4. O congelamento confere nesta ordem: o estado, o ato e o documento no mesmo instante, a configuração, a montagem, a transição e a gravação. As recusas das etapas da aplicação se acumulam às da configuração, e só deixa de rodar a etapa que lê um dado que outra recusou. A aprovação da proteção é regra sobre uma leitura que nunca falha, e não falha da leitura: a recusa dela sai na frente, antes da configuração e da operação, e a recópia e a conferência da configuração rodam sempre. O corte é por exigência: a exigência que a aprovação recusa (tipo pendente, tipo excluído ou fato comprovado pendente) fica fora das pendências que leem a proteção, e as demais exigências seguem conferidas.
5. A recusa é um lote: resposta 422 com `errors[]`, em que o erro raiz é a primeira causa, e cada causa traz o campo do documento da configuração quando aponta o documento. O painel de conformidade tem um item por assunto de declaração e de conjunto (`cronograma_fases_declaradas`, `cronograma_fases_em_conjunto`, `etapas_declaradas`, `etapas_em_conjunto`), cada um derivado da mesma conferência que a configuração chama, de modo que o painel e a conferência concordam em tudo. O painel tem também um item por regra dos motivos de decisão escolhidos.

## Consequências

### Positivas

- O diagnóstico, o painel e o congelamento mostram as mesmas causas, na mesma ordem e com o mesmo texto.
- Sai do agregado o código que repetia as regras de conformidade.

### Negativas

- Algumas primeiras causas mudam de ordem, e cada mudança tem teste que a registra.
- O painel ganha os itens de cada assunto novo, entre eles os três dos motivos de decisão escolhidos, e as mensagens de erro do congelamento ganham ponto final.

### Neutras

- O estado derivado sai da raiz, e o campo das causas passa a ponteiro JSON.

## Confirmação

Testes de unidade fixam a ordem dos assuntos, a paridade do painel com a conferência e o bloco de cada causa no documento. Testes de arquitetura garantem que a configuração apta a congelar não tem construtor público.

## Prós e contras das opções

### Manter as cadeias de primeira causa

- Bom, porque não altera o que já funciona.
- Ruim, porque painel e congelamento divergem e o diagnóstico mostra uma causa por vez.

### Conferir por acumulação

- Bom, porque a ordem, o texto e a regra de quando deixar de avaliar vivem num lugar só.
- Ruim, porque muda a primeira causa de alguns estados.

## Mais informações

- Emendada pela [ADR-0175](0175-o-processo-publicado-nao-tem-edicao-direta-nem-sessao-editorial.md): saem a restauração, o rascunho de retificação aberto e o fechamento da sessão, e a retificação passa a ser conferida só por esta conferência.
- [ADR-0110](0110-retificacao-como-sessao-editorial.md), [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md), [ADR-0167](0167-servicos-de-aplicacao-com-dependencia-no-construtor-e-orquestrador-do-congelamento.md), [ADR-0173](0173-regras-da-configuracao-sobre-declaracoes-parciais.md).
