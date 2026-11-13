---
status: "proposed"
date: "2026-11-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Encarregados de dados da Unifesspa"
informed:
  - "Equipe Uni+"
---

# ADR-0164: Tramitação da providência sobre concessão com estados gravados, prejudicada derivada e revogação vinculada

## Contexto e enunciado do problema

A [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md) deu ao Encarregado de dados a permissão `autorizacao:providencias:solicitar`: ele pede, com fundamento, a retirada ou a revisão de uma concessão, e os gestores de acessos e os Representantes do controlador respondem com motivação ou atendem pela revogação vinculada. A ADR-0162 fixou os estados gravados (`aberta`, `respondida` e `atendida`) e a situação derivada `prejudicada`, mas não decidiu como a providência se grava, como convive com as outras formas de encerrar a concessão nem onde entra na ordem única de travas. A issue unifesspa-edu-br/uniplus-api#2367 implementa a providência; os requisitos são UNI-REQ-0375 (solicitação com fundamento, fila e avisos) e UNI-REQ-0376 (resposta, atendimento, prejudicada, precedência e idempotência).

A concessão pode deixar de valer por quatro caminhos além da providência: a revogação direta, a revogação pela revisão periódica, a revogação pela janela de migração e o vencimento da vigência, que nunca é gravado ([`Concessao.EstaAtiva`](../../src/autorizacao/Unifesspa.UniPlus.Autorizacao.Domain/Entities/Concessao.cs)). Se a providência gravasse o efeito desses caminhos, cada um deles teria de conhecê-la, e o vencimento exigiria uma rotina só para isso. Se a providência não olhasse para eles, uma resposta poderia ser gravada sobre uma concessão já revogada.

O problema é decidir o modelo de estado da providência, a precedência entre resposta, atendimento e encerramento da concessão por outro caminho, e as travas que tornam essa precedência verdadeira sob concorrência, sem mudar os caminhos de revogação que já existem.

## Drivers da decisão

- **Fiscalizar não é decidir.** O Encarregado pede; quem responde e quem revoga é a gestão de acessos ou o Representante, pelo fluxo de revogação que já existe, com o mínimo de dois gestores ([ADR-0152](0152-governanca-das-concessoes-de-acesso.md)).
- **Sem autodecisão.** Quem solicitou e o beneficiário da concessão não respondem nem atendem (UNI-REQ-0184, UNI-REQ-0376).
- **Vence quem grava primeiro.** A resposta só vale sobre providência aberta e concessão ativa no instante em que é gravada.
- **Nada envelhece gravado.** O vencimento não tem ato nem rotina; estado que depende do relógio é derivado na leitura, como a atividade da concessão e a situação da revisão.
- **Ordem única de travas.** Nenhum caminho novo pode travar um recurso anterior depois de um posterior na ordem da ADR-0162.
- **Repetição segura.** Os endpoints de escrita têm `Idempotency-Key` ([ADR-0027](0027-idempotency-key-store-postgresql.md)) e o conflito de concorrência segue a [ADR-0119](0119-padrao-concorrencia-otimista-handlers-wolverine.md).

## Opções consideradas

- **A**: Gravar todos os estados, inclusive `prejudicada`, com cada caminho de revogação marcando as providências abertas da concessão e uma rotina periódica marcando as de concessão vencida.
- **B**: Gravar só os atos da providência (`aberta`, `respondida`, `atendida`), derivar `prejudicada` na leitura e travar a concessão antes de gravar qualquer ato da providência.
- **C**: Gravar só os atos da providência, derivar `prejudicada`, e confiar apenas no controle otimista da providência, sem travar a concessão na resposta.

## Resultado da decisão

**Escolhida:** "B — atos gravados, `prejudicada` derivada e a concessão travada antes de qualquer ato da providência", porque é a única opção em que a precedência vale sob concorrência sem que a revogação direta, a da revisão, a da janela e o vencimento precisem conhecer a providência.

**Agregado.** A providência é um agregado próprio do módulo de autorização, com uma única concessão como alvo, o tipo (`retirada` ou `revisao`), o fundamento, quem solicitou (com o e-mail verificado do token, só para os avisos) e quando, e, uma única vez, o desfecho: quem respondeu ou atendeu, quando e com que motivo. Ela não se edita, não tem soft delete e não se exclui: é registro de auditoria, como a revisão periódica. Há no máximo uma providência aberta por concessão; respondida ou atendida, uma nova pode ser aberta enquanto a concessão estiver ativa. A abertura exige concessão ativa (UNI-REQ-0375).

**Estados.** O estado gravado é `aberta`, `respondida` ou `atendida`, e só os atos da própria providência o mudam. A situação exposta acrescenta `prejudicada`: providência `aberta` cuja concessão não está ativa no instante da leitura, pelo critério de `Concessao.EstaAtiva`, seja por revogação feita por outro caminho, seja por vencimento. Nada grava `prejudicada`, não há rotina de vencimento, e a leitura informa o primeiro fato que encerrou a concessão, com o instante: o vencimento, se o fim da vigência chegou até a revogação (a revogação direta aceita concessão já vencida, e no empate a concessão já não valia), senão a revogação. A revogação feita por outro caminho não atende a providência: só a revogação vinculada a leva a `atendida` (UNI-REQ-0376).

**Atos.** Responder exige providência `aberta`, concessão ativa no instante do ato, quem responde diferente de quem solicitou e do beneficiário, e motivo. Atender exige o mesmo e segue a revogação comum (UNI-REQ-0376 com UNI-REQ-0186): quem atende digita o e-mail do beneficiário, e a concessão só é revogada se ele for o da concessão, com a mesma recusa da revogação comum; a revogação confere o mínimo de dois gestores de acessos; a revogação e o desfecho da providência são gravados juntos, ou nenhum dos dois. As recusas por estado saem antes das de dados: providência já respondida ou atendida, depois concessão que deixou de estar ativa, depois quem responde impedido; o e-mail de confirmação e o motivo são conferidos depois e acumulam, como pede a ADR-0125.

**Travas e precedência.** Todo ato que grava a providência trava antes a linha da concessão e lê a providência e o instante depois dessa trava. A abertura e a resposta tomam a trava compartilhada da concessão: a revogação concorrente, por qualquer caminho, espera ou é esperada, e quem chega depois lê o que o outro confirmou. O atendimento toma as travas da revogação direta, na ordem da ADR-0162 (papel de Representante quando a concessão é dele, pessoa do beneficiário quando o perfil é de governança, e a linha da concessão em modo exclusivo) e, quando a concessão conta como gestor, a trava dos gestores. A linha da providência é a última a ser travada, pelo próprio `UPDATE` da gravação, e nenhum caminho trava a concessão depois da providência; por isso a providência não cria ciclo com a revisão, a janela ou a revogação direta. Duas respostas simultâneas passam pela trava compartilhada e disputam a linha da providência pelo controle otimista: uma grava, e a outra recebe o conflito de concorrência da ADR-0119; a que chega depois da primeira confirmada recebe a recusa de providência já respondida. A abertura simultânea de duas providências para a mesma concessão esbarra no índice único da providência aberta, e a segunda recebe a recusa de providência já aberta. O vencimento não precisa de trava: é conferido contra o instante lido depois da trava.

**Idempotência e avisos.** Abertura, resposta e atendimento exigem `Idempotency-Key`; a resposta de sucesso devolve a providência, e a repetição com a mesma chave devolve a mesma resposta sem executar de novo. O conflito de concorrência é capturado no handler, com o rastreamento descartado, porque o endpoint tem chave (ADR-0119). Os avisos por e-mail saem pelo outbox na mesma transação do ato, depois de a gravação ser aceita, e a escolha de quem recebe cada aviso fica num ponto só do código. A abertura avisa todos os gestores de acessos e Representantes do controlador, inclusive quem solicitou, se também administra concessões (UNI-REQ-0375); a única exceção, por decisão explícita do líder técnico, é o beneficiário da concessão, que não é avisado da abertura. A resposta e o atendimento avisam quem solicitou, pelo e-mail verificado gravado na abertura, mesmo que ele já não tenha a permissão de solicitar. A revogação pelo atendimento avisa também o beneficiário, por decisão do líder técnico: a mensagem informa que a concessão foi revogada e orienta, se ele tiver motivos para manter o perfil de acesso, a fazer nova solicitação comprovando a necessidade, sem dizer quem pediu a providência nem quem fez a remoção. Nenhum aviso leva fundamento, motivo, nome de pessoa, `sub` ou e-mail de outra pessoa (UNI-REQ-0375).

**Fila prioritária.** As providências abertas ficam no topo da fila de trabalho de quem administra concessões, antes dos demais itens, e entre elas a ordem é a de abertura (UNI-REQ-0375). A API entrega essa fila como recurso próprio, a mais antiga primeiro, ordenada pelo instante gravado na abertura e, no empate, pelo identificador, com o par na âncora do cursor, e a web a consome antes das demais filas de trabalho; a API não mistura numa só lista itens de recursos diferentes.

**Trilha.** A trilha da concessão passa a mostrar a solicitação, a resposta e o atendimento de cada providência, com autor e texto, e o vencimento da concessão como evento do sistema, derivado da vigência, mesmo quando houve revogação depois; a revogação vinculada aparece também como revogação da concessão. Cada providência exposta na trilha é registrada no registro de acesso pela mesma permissão que dá a leitura das providências, e quem não tem nenhuma delas não as vê ali.

**Relação com outras decisões.** Esta ADR estende a regra "Providência" e a ordem de travas da ADR-0162 sem alterar o que ali está: a providência entra depois da linha da concessão e antes da trava dos gestores na ordem lógica, e a linha dela é travada por último na gravação. Não contradiz a ADR-0152 (quem revoga e o mínimo de dois gestores), a ADR-0119 (conflito otimista com captura quando há chave), a [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md) (validação acumulada no domínio), a [ADR-0153](0153-projecao-por-protecao.md) (leitura de dado pessoal registrada item a item) nem a [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) (a providência não tem soft delete).

## Consequências

### Positivas

- A revogação direta, a da revisão, a da janela e o vencimento não mudam: nenhum deles conhece a providência.
- A situação `prejudicada` nunca fica desatualizada, porque não é gravada, e não há rotina a operar.
- A precedência vale sob concorrência real: a resposta nunca é gravada sobre uma concessão já revogada ou vencida.
- O atendimento herda do fluxo de revogação o mínimo de dois gestores e as travas da governança.

### Negativas

- A leitura da providência sempre consulta a concessão, e o filtro por situação precisa reproduzir no banco o critério de `Concessao.EstaAtiva`, que passa a existir em mais um lugar.
- A resposta segura a trava compartilhada da concessão até o fim da transação, o que atrasa a revogação concorrente da mesma concessão pelo tempo de uma gravação.
- Uma providência aberta sobre concessão encerrada fica `aberta` no banco para sempre; quem consulta o banco direto precisa aplicar a mesma derivação.

### Neutras

- A segunda resposta recebe recusa de estado ou conflito de concorrência conforme o instante em que leu a providência; os dois são recusas, e nenhum grava.

## Confirmação

- Testes de domínio das transições, da ordem das recusas, da validação acumulada na abertura e da situação derivada para concessão revogada, vencida e ativa.
- Testes no host, com transações reais: revogação externa antes da resposta (a resposta é recusada e a providência aparece como prejudicada); duas respostas simultâneas (uma grava e a outra recebe conflito de concorrência); vencimento durante a tramitação (a providência aparece como prejudicada e a linha não é regravada, e a resposta que esperava a trava é recusada); repetição do atendimento com a mesma chave (mesma resposta, uma revogação e um aviso de cada tipo). Cada teste falha com a mutação que retira a trava ou o controle que ele protege.
- Testes de autorização (sem a permissão, 403), de impedimento de quem solicitou e do beneficiário, do mínimo de dois gestores no atendimento e da recusa do atendimento com e-mail de confirmação divergente, com o mesmo erro da revogação comum.
- Testes dos destinatários: a abertura avisa todos os gestores e Representantes, inclusive quem solicitou, e não avisa o beneficiário; a resposta e o atendimento avisam quem solicitou mesmo sem a permissão de solicitar; o e-mail ao beneficiário na revogação pelo atendimento não contém nome, `sub` nem e-mail de quem solicitou nem de quem atendeu (asserção explícita).
- Teste de persistência do índice único da providência aberta e da restrição que exige o desfecho completo.

## Prós e contras das opções

### A — Todos os estados gravados

- Bom, porque a consulta direta ao banco mostra a situação sem derivar.
- Ruim, porque cada caminho de revogação precisaria travar e atualizar as providências da concessão, e o vencimento exigiria uma rotina que pode atrasar ou falhar, deixando a situação gravada errada até rodar.

### B — Atos gravados, prejudicada derivada e concessão travada antes do ato

- Bom, porque os outros caminhos ficam intactos e a precedência é garantida pela trava da concessão, que todos eles já tomam.
- Ruim, porque a leitura depende da concessão e a resposta segura uma trava compartilhada.

### C — Atos gravados, prejudicada derivada e só o controle otimista

- Bom, porque a resposta não trava nada além da própria providência.
- Ruim, porque a revogação por outro caminho não toca a providência: a resposta lida antes da revogação seria gravada depois dela, e a concessão revogada ficaria com providência respondida, o que contradiz a precedência decidida.

## Mais informações

- **Estende** a [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md), regra "Providência" e parágrafo de concorrência, sem emendá-la.
- [ADR-0152](0152-governanca-das-concessoes-de-acesso.md): quem revoga e o mínimo de dois gestores de acessos.
- [ADR-0119](0119-padrao-concorrencia-otimista-handlers-wolverine.md) e [ADR-0027](0027-idempotency-key-store-postgresql.md): conflito de concorrência com chave de idempotência.
- [ADR-0004](0004-outbox-transacional-via-wolverine.md) e [ADR-0005](0005-cascading-messages-para-drenagem-de-domain-events.md): os avisos saem pelo outbox na transação do ato.
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) e [ADR-0089](0089-navegacao-bidirecional-cursor-keyset-reverso.md): a fila é paginada por cursor, em ordem de abertura.
- UNI-REQ-0375, UNI-REQ-0376 e UNI-REQ-0186 (confirmação do e-mail na revogação); issue unifesspa-edu-br/uniplus-api#2367, épica unifesspa-edu-br/uniplus-api#2373. Os avisos aos novos papéis em geral são da issue unifesspa-edu-br/uniplus-api#2368.
