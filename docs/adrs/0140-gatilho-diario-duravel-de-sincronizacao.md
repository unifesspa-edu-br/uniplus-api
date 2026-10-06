---
status: "accepted"
date: "2026-10-06"
decision-makers:
  - "Tech Lead"
consulted:
  - "Equipe Discentes"
informed:
  - "Equipe de infraestrutura (DevOps)"
---

# ADR-0140: Gatilho diário durável para a sincronização de discentes

## Contexto e enunciado do problema

A réplica de vínculos de discentes é reconciliada uma vez por dia a partir do SIGAA ([ADR-0130](0130-integracao-com-o-sigaa-por-consulta-http-paginada.md)). A sincronização existe, mas nada a dispara: falta o gatilho diário (issue #882).

O gatilho precisa disparar uma vez por dia, ser processado por uma única réplica sem eleição de líder separada, permitir reexecução sem duplicar o trabalho do mesmo dia e continuar funcionando quando a sincronização de um dia falha.

A [ADR-0003](0003-wolverine-como-backbone-cqrs.md) deixou mensagens agendadas fora do escopo do backbone até haver um caso concreto. Este é o caso.

## Drivers da decisão

- Um dia sem sincronização deixa a réplica de vínculos desatualizada.
- A continuidade do ciclo não pode depender do sucesso da sincronização de um dia específico.
- A entrega pode ocorrer mais de uma vez; o trabalho precisa ser seguro para reexecução.
- A produção impõe restrições de infraestrutura: o usuário de banco da aplicação (role) não tem DDL ([ADR-0004](0004-outbox-transacional-via-wolverine.md)) e o schema é provisionado antes do rollout ([ADR-0127](0127-migrations-aplicadas-por-job-de-deploy.md)).

## Resultado da decisão

1. **Exceção à ADR-0003 para agendamento durável.** O gatilho diário é uma mensagem agendada cuja entrega sobrevive a reinício e a queda do processo. A durabilidade é pré-requisito.

2. **O gatilho se reagenda.** Cada disparo agenda o próximo ciclo antes de publicar o trabalho do dia. A continuidade do ciclo não depende do resultado da sincronização.

3. **Reserva da data e reagendamento são confirmados juntos.** A reserva de um dia e o agendamento do próximo disparo são gravados na mesma transação. Uma falha no processamento desfaz as duas coisas, e a reexecução do mesmo dia é possível.

4. **O trabalho do dia não se perde com a queda do processo.** Uma queda no meio da sincronização faz com que ela seja reexecutada depois, com atraso que depende do tempo de reatribuição de réplicas mortas.

5. **Entrega é pelo menos uma vez, sem ordem garantida.** A sincronização deve ser idempotente. Não há promessa de ordem entre dias: uma reexecução pode ocorrer depois do dia seguinte.

6. **Falha é observável.** Falha do gatilho vai para a fila de falhas definitivas (dead letter) com alerta. A ausência de execução do dia, dentro de um horário-limite, dispara alerta independente da própria cadeia.

7. **Um dia com falha não bloqueia os seguintes.** A sincronização de um dia que falha não é reprocessada automaticamente. A falha vai para a fila de falhas definitivas com alerta, e a cadeia segue para o dia seguinte. A reserva por data impede que um reprocessamento manual duplique trabalho, então esse reprocessamento, se for necessário, segue procedimento operacional próprio.

## Motivação

- Sem durabilidade de agendamento, o disparo pendente se perde em reinício e em queda do processo. A configuração atual de produção não tem essa durabilidade.
- Sem reserva por data, uma entrega duplicada bifurca a cadeia e a mantém bifurcada. Reservar na mesma transação do agendamento torna a duplicata inócua.
- Sem durabilidade das filas locais, a sincronização em andamento no momento da queda não é reexecutada. Com essa durabilidade, ela é reexecutada, fora de ordem.
- Um usuário de banco sem permissão na tabela de reserva faz a cadeia parar no primeiro disparo.

A evidência técnica desses pontos foi obtida em prova de conceito com o mesmo stack de produção e fica na issue #882, não nesta ADR.

## Opções consideradas

- **Timer em memória ou serviço em segundo plano sem persistência.** Rejeitada: o disparo se perde em reinício e em queda do processo.
- **Reagendar depois que a sincronização termina.** Rejeitada: a cadeia passaria a depender do sucesso do trabalho do dia, que é justamente o ponto que pode falhar.
- **Reservar a data antes do trabalho, sem transação.** Rejeitada: uma falha transitória deixa a data reservada sem trabalho feito, e a reexecução não faz nada (no-op).

## Consequências

- Positivas: a cadeia sobrevive a reinício, a queda de processo e a falha da sincronização. Uma réplica executa o disparo por dia, sem eleição separada.
- Negativas e custos:
  - A configuração de produção muda: durabilidade de agendamento e durabilidade das filas locais. Hoje nenhuma das duas está ligada.
  - A sincronização precisa ser idempotente, e não há ordem entre dias.
  - Uma queda no meio da sincronização atrasa o dia pelo tempo de reatribuição de réplicas mortas. Esse tempo é configuração de produção e entra no monitoramento.
  - A quebra da cadeia continua sendo um ponto único de falha. A decisão é detectá-la e recuperá-la, não eliminá-la.
- Para a infraestrutura: a role da aplicação precisa de permissões no schema de mensageria e na tabela de reserva, sem DDL. As migrations desses schemas são responsabilidade do job de deploy.

## Acompanhamento

Esta ADR é aceita com pendências de definição, que não bloqueiam a decisão e estão rastreadas na issue #1841:

- Horário-limite, fuso de referência e canal do alerta de dia não executado.
- Política de retry do gatilho em produção.
- Validação do gatilho no caminho de produção real da aplicação.
- Prazo de reatribuição de réplicas mortas em produção.

## Mais informações

- Issue de origem: #882 (gatilho diário durável de sincronização).
- O que a sincronização faz com cada vínculo (contrato, idempotência por registro) fica fora desta ADR, na issue de sincronização.
