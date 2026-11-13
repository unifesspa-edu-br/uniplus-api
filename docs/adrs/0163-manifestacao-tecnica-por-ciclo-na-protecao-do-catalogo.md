---
status: "proposed"
date: "2026-11-12"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Encarregados de dados da Unifesspa"
informed:
  - "Equipe Uni+"
---

# ADR-0163: Manifestação técnica na proteção do catálogo de tipos de documento e de fatos do candidato como evento imutável por ciclo de aprovação, gravada sob o controle de concorrência do item

## Contexto e enunciado do problema

A [ADR-0153](0153-projecao-por-protecao.md) pôs todo tipo de documento e todo fato do candidato sob a aprovação da proteção de dados: o item nasce pendente, o administrador do cadastro o envia numa solicitação e quem aprova decide (aprova, recomenda ajuste ou reprova). Cada ato fica num registro só acrescentado, e a edição que muda a proteção, ou qualquer edição de item reprovado ou com ajuste recomendado, reinicia a aprovação sem apagar esse registro (UNI-REQ-0205).

A [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md) separou quem decide de quem fiscaliza: o Representante do controlador decide a proteção do catálogo de tipos de documento e de fatos do candidato, e o Encarregado de dados registra manifestação técnica, sem decidir. A regra de negócio está em UNI-REQ-0373 e UNI-REQ-0374: a manifestação tem parecer adequada, recomenda ajuste ou inadequada, com texto obrigatório nos dois últimos; vale para o ciclo de aprovação em que foi feita; a nova manifestação da mesma pessoa substitui a vigente sem apagar a anterior; manifestações de pessoas diferentes coexistem; a falta de manifestação não impede a decisão; e aprovar ou recomendar ajuste diante de manifestação desfavorável vigente exige justificativa, registrada no ato da decisão, inclusive na aprovação, que hoje não guarda justificativa.

O problema é decidir onde a manifestação fica, como ela se liga ao ciclo, como a decisão enxerga a manifestação vigente sem corrida entre as duas, e como a justificativa entra no registro do ato.

## Drivers da decisão

- **Evidência imutável.** Registro e substituição são fatos que não se alteram nem se apagam ([ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md)); a edição do item não os apaga (UNI-REQ-0373).
- **Ciclo explícito.** O reinício encerra as manifestações do ciclo; a edição que não reinicia não as encerra (UNI-REQ-0373).
- **Sem corrida entre manifestação e decisão.** Se uma é gravada entre a leitura e o envio da outra, a segunda é recusada por conflito e refeita depois de reler (UNI-REQ-0374), com o padrão de concorrência otimista da [ADR-0119](0119-padrao-concorrencia-otimista-handlers-wolverine.md).
- **Quem fiscaliza não decide.** A manifestação não muda a situação da aprovação nem a proteção do item ([ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md)).
- **Validação acumulada.** As recusas de forma saem num único lote ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)).
- **Pré-produção.** Sem produção, o contrato e o banco mudam sem compatibilidade com o formato anterior.

## Opções consideradas

- **A**: Manifestação como coleção dentro do tipo de documento e do fato, alterada a cada substituição.
- **B**: Manifestação como registro próprio, só acrescentado, ligado ao item e ao ciclo, com a decisão lendo esse registro a cada vez, sem tocar a linha do item na gravação da manifestação.
- **C**: Manifestação como registro próprio, só acrescentado, ligado ao item e ao ciclo, cuja gravação também altera o estado do circuito de aprovação na linha do item, sob o mesmo controle de concorrência da decisão.

## Resultado da decisão

**Escolhida:** "C — registro próprio por ciclo, gravado junto de uma mudança no circuito do item", porque é a única em que a decisão e a manifestação disputam a mesma versão do item, de modo que nenhuma das duas é gravada sem ter visto a outra, e mantém a manifestação como evidência que não se altera.

### Ciclo de aprovação

O circuito de aprovação do item passa a ter um número de ciclo, que começa em 1. O reinício da aprovação a partir de qualquer situação diferente de pendente avança o ciclo; o item pendente que tem a proteção mudada continua no mesmo ciclo, porque nenhuma solicitação aconteceu nele. O ciclo é a chave que liga manifestação, solicitação e decisão: as manifestações de um ciclo encerrado continuam no histórico e deixam de ser vigentes.

### Manifestação

- A manifestação só é registrada em item com aprovação solicitada, por quem tem `configuracao:protecao-do-catalogo:manifestar` (UNI-REQ-0373).
- Quem se manifesta informa o ciclo que leu. Ciclo diferente do atual é recusado com conflito sem nova tentativa automática: o item foi reiniciado, e talvez solicitado de novo, depois de lido, e a manifestação falaria do que não foi visto.
- Cada gravação é um evento só acrescentado, com o item, o ciclo, o parecer, o texto, quem se manifestou (emissor, `sub` e nome no instante) e o instante. A substituição é um evento novo que aponta para a manifestação vigente da mesma pessoa no mesmo ciclo; a anterior não é alterada. Vigente é a manifestação do ciclo atual que nenhum evento substituiu; a de ciclo encerrado é lida como encerrada.
- O texto tem até 2.000 caracteres, o mesmo limite da justificativa da decisão, e é obrigatório nos pareceres recomenda ajuste e inadequada.
- O banco garante uma única cadeia por pessoa e ciclo: no máximo uma manifestação inicial (sem substituída) por item, ciclo e pessoa, e no máximo uma substituição de cada manifestação.

### Estado do circuito na linha do item

A gravação da manifestação altera, na mesma transação, o circuito de aprovação do item: quantas manifestações o ciclo recebeu, o instante da última e se há manifestação desfavorável vigente, calculado sobre as vigentes depois do evento. A contagem avança a cada evento, então toda manifestação muda a linha do item, mesmo quando o relógio repete o instante e o indicador não muda. Como o circuito é gravado na linha do item, a mudança passa pelo mesmo token de concorrência otimista que a decisão, o aceite e a edição usam. Por isso:

- a decisão lida antes de uma manifestação gravada responde conflito e, relida, enxerga a manifestação;
- a manifestação lida antes de uma decisão gravada responde conflito e, relida, encontra o item fora de solicitação;
- duas manifestações concorrentes, da mesma pessoa ou de pessoas diferentes, são serializadas da mesma forma, e a refeita coexiste com a outra ou a substitui.

O reinício do circuito zera a contagem, o instante e o indicador junto com o avanço do ciclo. A manifestação não muda a situação da aprovação nem a proteção do item.

### Decisão diante de manifestação desfavorável

- Aprovar ou recomendar ajuste em item com manifestação desfavorável vigente (recomenda ajuste ou inadequada) exige justificativa; sem ela, a decisão é recusada com código próprio. A reprovação já a exige sempre (UNI-REQ-0374).
- A aprovação passa a aceitar justificativa opcional também sem manifestação desfavorável, como o ajuste já aceitava.
- O ato da decisão registra a justificativa, o ciclo e se havia manifestação desfavorável vigente no instante da decisão. O banco recusa o ato de aprovação ou de ajuste marcado como diante de manifestação desfavorável sem justificativa.
- O aceite da recomendação pelo administrador do cadastro não exige nova justificativa e não reinicia o ciclo.

### Avisos

A solicitação avisa, além de quem aprova, quem tem a permissão de se manifestar: um e-mail por pessoa e por solicitação, sem nome de pessoa nem texto livre, no mesmo outbox da gravação. A falta de quem possa se manifestar não impede a solicitação.

### Consultas

Quem aprova ou consulta a proteção do catálogo de tipos de documento e de fatos do candidato lê as manifestações vigentes no detalhe do item e o histórico de todos os eventos de manifestação do item, de todos os ciclos, com a situação derivada na leitura (vigente, substituída ou encerrada). As leituras passam pelo registro de acesso das permissões de classe pessoal ([ADR-0153](0153-projecao-por-protecao.md)).

## Consequências

### Positivas

- Manifestação e decisão nunca se cruzam sem que a segunda veja a primeira, sem trava pessimista nem tabela extra de controle.
- O histórico mostra cada manifestação, inclusive as substituídas e as de ciclos encerrados, e cada decisão com a justificativa e o indicador de manifestação desfavorável vigente.
- A regra da justificativa lê o indicador da mesma linha que a versão protege, sem depender da ordem das leituras.

### Negativas

- O indicador de manifestação desfavorável é derivado e fica gravado no item; a gravação da manifestação recalcula-o sobre as vigentes, e um teste confere que ele bate com os eventos depois de cada registro e substituição.
- Toda manifestação provoca conflito numa decisão, numa edição ou num aceite que estejam em curso no mesmo item, e vice-versa; quem perde relê e refaz.
- O ciclo e o indicador entram no contrato da consulta do item e dos atos, mudança incompatível aceita em pré-produção.

### Neutras

- A numeração do ciclo conta os reinícios a partir de item já solicitado, e não as edições.

## Confirmação

- Testes de domínio do ciclo (reinício a partir de cada situação, edição sem reinício), da manifestação (parecer, texto, substituição, coexistência, recusa fora de solicitação e com ciclo divergente) e da regra da justificativa diante de manifestação desfavorável.
- Testes de integração, com transações reais pelo host, de manifestação e decisão concorrentes nas duas ordens, para tipo de documento e para fato, inclusive manifestação contra recomendação de ajuste seguida do aceite do administrador sem justificativa.
- Teste de consulta histórica, depois de gravar e recarregar, com manifestação substituída e justificativa da aprovação.
- Teste de aviso: cada titular da permissão de se manifestar recebe um único e-mail por solicitação.
- Testes de permissão: quem não tem a permissão de se manifestar é recusado, e quem se manifesta não decide.

## Prós e contras das opções

### A — Coleção dentro do item

- Bom, porque a manifestação já viria no carregamento do item.
- Ruim, porque substituir alteraria a coleção, contra a evidência só acrescentada, e o reinício teria de escolher entre apagar e arquivar o que pertence ao ciclo anterior.

### B — Registro próprio lido pela decisão, sem tocar o item

- Bom, porque a manifestação não mexe na linha do item.
- Ruim, porque a manifestação gravada entre a leitura e a gravação da decisão não altera a versão do item: a decisão seria gravada sem a justificativa exigida, e só uma trava pessimista ou uma releitura serializada resolveria.

### C — Registro próprio com o circuito na linha do item

- Bom, porque a disputa usa o controle de concorrência que a decisão já usa, e a evidência continua só acrescentada.
- Ruim, porque guarda no item um indicador derivado dos eventos e faz a manifestação competir com edições do item.

## Mais informações

- **Emenda** a [ADR-0153](0153-projecao-por-protecao.md), seção "Aprovação da proteção de dados como pré-condição": o circuito ganha o ciclo e o estado das manifestações, e o ato de aprovação passa a guardar a justificativa.
- **Detalha** a regra "Manifestação na proteção do catálogo", sobre os tipos de documento e os fatos do candidato, da [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md), sem contradizê-la.
- [ADR-0119](0119-padrao-concorrencia-otimista-handlers-wolverine.md): os endpoints de manifestação e de decisão exigem chave de idempotência, então o conflito é tratado no handler, com descarte das alterações, e responde 409 com nova tentativa possível.
- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md): a manifestação, como o ato, é evidência forense, fora do soft delete e sem chave estrangeira para o item.
- [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md): as recusas de forma da manifestação e da decisão saem num único lote.
- UNI-REQ-0205, UNI-REQ-0373 e UNI-REQ-0374; issue unifesspa-edu-br/uniplus-api#2366.
