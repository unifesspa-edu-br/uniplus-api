---
status: "accepted"
date: "2026-11-25"
decision-makers:
  - "Tech Lead"
---

# ADR-0179: A consequência do descumprimento é declarada na parte do processo que exige, com três consequências

## Contexto e enunciado do problema

Em 25/11/2026, o líder técnico decidiu que cada condição ou ação exigida na
habilitação tem consequência configurada (UNI-REQ-0501 e UNI-REQ-0502). Só a
exigência documental declarava consequência, numa lista que misturava destino,
como a ampla concorrência, e procedimento, como o prazo para reenvio.

## Opções consideradas

- Manter listas próprias de consequências e procedimentos em cada parte.
- Compartilhar uma lista fechada de consequências e declará-las onde a falta ocorre.

## Resultado da decisão

Cada exigência, grupo de comprovação, banca e formulário pode declarar sua própria
consequência, respectivamente para o descumprimento, a ausência à banca e a falta
de envio do formulário. A declaração é opcional: sem ela, a falta não tem
consequência própria. Não há catálogo de ações. O envio de documentos segue a
consequência de cada exigência, sem declaração global.

A lista compartilhada é fechada e tem três consequências:

- `ELIMINA_DO_PROCESSO`: elimina a pessoa do processo.
- `RETIRA_A_MODALIDADE`: retira a modalidade de vaga que exige a condição não
  comprovada e em que a pessoa concorre; ela segue nas demais modalidades,
  inclusive na ampla concorrência.
- `RETIRA_O_ACRESCIMO`: retira somente o acréscimo do Fator de Atenuação das
  Desigualdades de Acesso. Pode ser declarada em qualquer exigência.

O reenvio é uma propriedade da exigência e do grupo de comprovação,
`abrePrazoParaReenvio`, e não uma consequência. As conferências dessas declarações
formam o assunto «consequências do descumprimento» da conferência única
([ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md)),
que acumula causas ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)).
A execução lê a versão congelada da chamada e só produz efeito depois do resultado
definitivo e dos recursos.

## Consequências

- O documento da configuração, o documento congelado e o resumo mudam no lugar
  até a primeira publicação em produção, sem versão nova
  ([ADR-0109](0109-envelope-canonico-v2-do-congelamento.md) e
  [ADR-0172](0172-esquema-json-versionado-do-documento-de-configuracao.md)).
- Saem `RECLASSIFICA_AC`, `REMOVE_VANTAGEM` e `ABRE_PRAZO_PARA_REENVIO` como
  consequências e `RECLASSIFICA_CONFORME_EDITAL` da modalidade. A recusa de ampla
  concorrência não ofertada permanece para `RETIRA_A_MODALIDADE`.
- A aplicação à pessoa depende de agregados do Ingresso e fica em issues próprias.
