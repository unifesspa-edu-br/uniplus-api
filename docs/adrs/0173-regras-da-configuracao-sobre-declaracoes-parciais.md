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

# ADR-0173: Regras da configuração avaliadas sobre declarações parciais

## Contexto e enunciado do problema

As regras de cronograma, de etapas, de tipos de ato e de bancas eram avaliadas por conferências que liam as entidades do agregado e devolviam a primeira causa. Algumas regras de declaração eram reconferidas na publicação, e a restauração tinha cópias próprias das regras de estrutura do cronograma. O diagnóstico da configuração precisa de todas as causas de uma vez, inclusive quando parte da declaração é recusada, como pede a [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md).

## Drivers da decisão

- Uma regra deve viver num lugar só: gravação, publicação e restauração a avaliam pelo mesmo predicado e com o mesmo texto.
- O diagnóstico deve acumular as causas independentes entre si.
- Os tipos da fase e os da etapa não devem depender uns dos outros.

## Opções consideradas

- Manter as conferências lendo as entidades e acrescentar as regras novas ao lado.
- Avaliar as regras sobre declarações conferidas, e as conferências delegarem a elas.

## Resultado da decisão

**Escolhida:** "Avaliar as regras sobre declarações conferidas, e as conferências delegarem a elas", porque é a única forma de manter cada regra num lugar só e de acumular as causas.

1. Cada fase e cada etapa é interpretada como declaração conferida: os dados declarados, normalizados, e as recusas da própria declaração, pelos mesmos predicados da entidade. A entidade também produz a sua declaração. A configuração completa só se constrói sem recusas nem causas.
2. As regras se dividem em famílias, e cada família é um grupo de regras: declaração, conjunto (gravação e publicação) e completude (só publicação), com códigos que não se repetem entre elas. Cada regra, com predicado, código e texto, vive num lugar só. As conferências a chamam e a conferência da configuração as acumula ([ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md)).
3. As causas independentes se acumulam por família e depois por posição de entrada. Uma regra deixa de avaliar uma unidade só quando lê o valor de um dado que a declaração recusou. Um item do painel de conformidade que não pôde verificar uma unidade fica pendente, nunca aprovado por omissão.
4. As declarações guardam valores, e não entidades. Fase e etapa só se conhecem por valores comuns, e as regras que relacionam as duas ficam num lugar próprio.
5. O modo de avaliação da etapa (nota numérica, conceito categórico, nota do ENEM ou nenhum) é decidido num ponto único, e só quando os dados que o decidem foram aceitos.

## Consequências

### Positivas

- O diagnóstico de um rascunho mostra todas as causas independentes.
- As conferências e a restauração não repetem as regras de cronograma e de etapas.
- A conferência da isenção cobre todas as fases que a coletam.

### Negativas

- Algumas primeiras causas mudam de ordem, e cada mudança tem teste que a registra.

### Neutras

- A restauração não tem regra própria para a etapa sem fase: ela confere só a integridade do que restaura, e a etapa sem fase é completude, recusada no congelamento ([ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md)).
- A conferência por acumulação no lugar das cadeias de primeira causa é a da [ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md).

## Confirmação

Testes de arquitetura garantem que os tipos da fase e os da etapa não dependem uns dos outros. Testes de unidade fixam a ordem das causas e a sequência do painel de conformidade.

## Prós e contras das opções

### Manter as conferências lendo as entidades

- Bom, porque não altera as conferências.
- Ruim, porque duplica regra e diverge entre gravação, publicação e restauração.

### Avaliar sobre declarações conferidas

- Bom, porque cada regra tem um lugar e as causas se acumulam.
- Ruim, porque muda a primeira causa de alguns estados.

## Mais informações

- [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md): domínio como fonte única das regras e acúmulo de causas.
