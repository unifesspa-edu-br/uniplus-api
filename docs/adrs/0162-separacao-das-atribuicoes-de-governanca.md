---
status: "proposed"
date: "2026-11-11"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Encarregados de dados da Unifesspa"
informed:
  - "Equipe Uni+"
---

# ADR-0162: Separação das atribuições de governança entre gestor de acessos, Representante do controlador e Encarregado de dados

## Contexto e enunciado do problema

A [ADR-0152](0152-governanca-das-concessoes-de-acesso.md) pôs a segunda aprovação dos pedidos com dado pessoal, a revisão periódica e a consulta da trilha com o Encarregado de dados. A [ADR-0153](0153-projecao-por-protecao.md) deu ao mesmo papel a aprovação da proteção do catálogo (tipos de documento e fatos), e a [ADR-0157](0157-perfis-de-acesso-versionados-e-aprovados.md), a aprovação das versões protegidas de perfil, a decisão protegida da janela de migração e a liberação da reclassificação. O perfil de sistema que reúne essas permissões é um só.

As orientações dos Encarregados de dados da Unifesspa apontam o conflito desse desenho: o Encarregado, na LGPD (art. 41), orienta, recebe comunicações e acompanha o tratamento, e não deve ser quem decide o próprio tratamento que fiscaliza. Quem decide, em nome da instituição, é o controlador (art. 5º, VI), por quem o representa. Hoje o mesmo perfil decide e fiscaliza, e a mesma pessoa pode acumular as duas funções. A épica unifesspa-edu-br/uniplus-api#2373 pede a separação; esta ADR registra a decisão que as issues #2362 a #2368 implementam.

O problema é dividir as atribuições entre três papéis, de modo que quem fiscaliza enxergue tudo sem decidir nada, que a decisão sobre dado pessoal continue com uma segunda pessoa distinta do gestor, e que a separação valha em todos os caminhos: pedido, migração em lote, revisão periódica, catálogo e instalação.

## Drivers da decisão

- **Fiscalizar não é decidir.** O Encarregado de dados consulta, se manifesta e solicita providências, sem aprovar, recusar ou revogar (LGPD, art. 41, § 2º).
- **Decisão pelo controlador.** A segunda etapa, as versões protegidas, a janela e a proteção do catálogo são decididas por quem representa a instituição (LGPD, art. 5º, VI, e art. 23).
- **Incompatibilidade efetiva.** A separação não pode depender de a mesma pessoa não receber os dois perfis por descuido; o sistema recusa a acumulação em todo caminho de concessão.
- **Sem autoaprovação.** Ninguém decide a própria concessão nem a segunda etapa em que é beneficiário (UNI-REQ-0184).
- **Rastreabilidade da ciência.** Quem recebe acesso a dado protegido declara ciência das obrigações, em texto identificável, antes da decisão do gestor.
- **Pré-produção.** Sem produção, a mudança não carrega compatibilidade com nomes, dados ou contratos anteriores.

## Opções consideradas

- **A**: Manter o perfil único e separar as funções por norma interna, sem mudança no sistema.
- **B**: Transferir ao Encarregado de dados só a parte consultiva, deixando a decisão com o gestor de acessos (fim da segunda etapa).
- **C**: Três papéis no sistema: o gestor de acessos opera, o Representante do controlador decide a parte de proteção de dados e o Encarregado de dados consulta, se manifesta e solicita, com a acumulação de Representante e Encarregado impedida.

## Resultado da decisão

**Escolhida:** "C — três papéis, com a decisão no Representante do controlador e a fiscalização no Encarregado de dados", porque preserva a segunda aprovação por pessoa distinta do gestor, tira o Encarregado da posição de decidir o que fiscaliza e torna a separação verificável pelo sistema, e não só por norma.

### Papéis

Os papéis são nomeados no masculino genérico, pela função, nunca pelo gênero de quem os ocupa.

| Papel | Perfil de sistema | Atribuição |
|---|---|---|
| **Gestor de acessos** | perfil 1 (inalterado) | Mantém os perfis, aprova a adequação funcional, revoga, publica o termo de responsabilidade e decide, na revisão periódica, os itens de quem é Representante. |
| **Representante do controlador** | perfil 2, que mantém o ID e deixa de se chamar proteção de dados | Decide a segunda etapa dos pedidos protegidos, as versões protegidas de perfil, a etapa protegida da janela de migração, a liberação da reclassificação e a proteção do catálogo; conduz a revisão periódica; revoga. |
| **Encarregado de dados** | perfil 3, novo e imutável | Consulta pedidos, concessões, revisões, versões, janela e catálogo; registra manifestação técnica na proteção do catálogo; solicita providência de retirada ou revisão de concessão. Não aprova, não recusa e não revoga. |

O tipo de governança passa a ter três valores explícitos: gestor de acessos, Representante do controlador (código `representante-do-controlador`, no lugar do tipo de proteção de dados) e Encarregado de dados (código `encarregado-de-dados`, ID `019a0000-0000-7000-8000-000000000003`).

**Composição.** O gestor mantém as permissões atuais e recebe `autorizacao:termos:publicar`. O Representante reúne `autorizacao:concessoes:administrar`, `autorizacao:pedidos:aprovar-protecao-de-dados`, `autorizacao:perfis:aprovar-protecao-de-dados`, `autorizacao:revisoes:conduzir`, `autorizacao:permissoes:consultar-catalogo` e `configuracao:protecao-do-catalogo:aprovar`. O Encarregado reúne `autorizacao:permissoes:consultar-catalogo`, `autorizacao:pedidos:consultar`, `autorizacao:concessoes:consultar`, `autorizacao:revisoes:consultar`, `autorizacao:perfis:consultar-versoes`, `autorizacao:providencias:solicitar`, `configuracao:protecao-do-catalogo:consultar` e `configuracao:protecao-do-catalogo:manifestar`. As permissões novas e `configuracao:protecao-do-catalogo:aprovar` são reservadas à governança: perfil comum não as recebe. A base legal das permissões pessoais de governança passa a ser o art. 7º, II, combinado com o art. 23 da LGPD (issues #2363 e #2364). `configuracao:protecao-do-catalogo:aprovar` passa de `Interno` a `Pessoal`, porque o histórico das decisões e os atos que ela abre devolvem o nome de quem agiu e a justificativa; assim, a mesma leitura é registrada para o Representante e para o Encarregado (issue #2364).

**Nomes preservados.** A segunda etapa continua identificada pelo que confere, e não por quem a decide: a família `*:aprovar-protecao-de-dados`, a etapa `protecao_de_dados`, a situação `aguardando_protecao_de_dados`, a permissão `autorizacao:revisoes:rever-protecao-de-dados` e a rota `fila-da-protecao-de-dados` não mudam. Os códigos ficam; mudam só os textos de apresentação, e o papel que os exerce passa a ser o Representante.

### Regras

- **Segunda etapa.** Pedido de perfil com permissão Pessoal, Identificador ou Sensível passa pelo gestor e, depois, por um Representante. Pedido sem essa etapa termina no gestor. Os três perfis de governança são protegidos e seguem a regra (UNI-REQ-0184; issue #2363).
- **Dispensas.** A dispensa `designacao_de_protecao_de_dados` passa a servir só à designação do primeiro Representante, quando nenhuma outra pessoa tem concessão vigente desse perfil; havendo Representante vigente, a designação de outro passa pela segunda etapa decidida por um Representante diferente do solicitante. A verificação "não há Representante vigente" e a gravação da concessão que dela resulta acontecem sob uma trava transacional única do papel de Representante, tomada antes da trava da pessoa por todo caminho que concede, aprova a segunda etapa ou revoga concessão de Representante, e também pela instalação; a migração em lote pela janela não toma essa trava, porque nunca concede nem revoga o perfil do Representante; assim, duas designações simultâneas de pessoas distintas não aplicam ambas a dispensa: a segunda, ao obter a trava, encontra o primeiro Representante vigente e segue para a segunda etapa. A dispensa `beneficiario_ja_tem_protecao_de_dados` deixa de existir: o pedido protegido de quem é Representante passa pela segunda etapa decidida por outro Representante. A remoção vale em todos os caminhos que aplicam a regra: criação e decisão de pedido e migração em lote pela janela, inclusive a concessão retida.
- **Titular e substituto.** O sistema não tem ato de substituição. É pré-requisito operacional manter no mínimo dois Representantes com concessão vigente, titular e substituto, para que cada um decida o pedido protegido do outro; o primeiro entra pela dispensa de designação e o segundo é decidido por ele.
- **Prazo.** As concessões de Representante e de Encarregado admitem prazo opcional; a de gestor continua sem prazo. Concessão vencida não conta como vigente para nenhuma regra desta ADR.
- **Incompatibilidade.** Uma pessoa não tem, ao mesmo tempo, concessões vigentes de Representante e de Encarregado, em qualquer ordem. Gestor e Representante podem ser a mesma pessoa, e continua valendo que as duas etapas de um mesmo pedido são de pessoas diferentes. A incompatibilidade é conferida, sem trava, na criação do pedido quando o beneficiário é o próprio solicitante e no aceite do beneficiário quando o pedido foi feito para outra pessoa, porque só então a identidade dele é conhecida; e, sob trava transacional por emissor e `sub` da pessoa, na aprovação que concede, que é a conferência definitiva. A instalação toma a mesma trava das pessoas que instala, para manter a ordem das travas, mas nunca recusa por conflito: ela só concede o perfil de gestor, compatível com os dois papéis; a migração em lote pela janela fica de fora, porque perfil de governança não tem versão e a janela nunca cria nem revoga concessão desses perfis. Concessão vencida não impede nova designação (issue #2364).
- **Termo de responsabilidade.** O gestor publica versões imutáveis do termo, com texto, resumo SHA-256 sobre bytes definidos (texto normalizado e codificação fixa), ato que o aprova e autor. A criação de pedido protegido fixa a versão vigente e é recusada sem termo publicado. Só o beneficiário aceita, autenticado; o aceite registra instante, pedido, versão e resumo. A ordem é documento verificado, depois aceite, depois gestor: o pedido protegido aguarda o beneficiário (`aguardando_beneficiario`) antes da fila do gestor. O cliente informa a versão que apresentou; o servidor a compara, na mesma transação da criação ou do aceite, com a vigente ou a fixada e responde conflito quando divergem. A vigência do termo é um registro único com controle de concorrência: a publicação troca a versão vigente sob trava exclusiva desse registro, e a criação do pedido lê a vigente sob trava compartilhada do mesmo registro até o fim da transação. Por isso a criação concorrente com uma publicação ou termina antes dela, fixando a versão anterior, que continua aplicável, ou depois dela, e então a versão apresentada diverge da vigente e a resposta é conflito, exigindo reapresentação. O aceite compara com a versão fixada no pedido, que é imutável, e não depende da publicação. Versão fixada continua aplicável após nova publicação, e nova publicação não derruba concessão existente (issue #2365).
- **Manifestação na proteção do catálogo.** Quem tem `configuracao:protecao-do-catalogo:manifestar` registra, em item com aprovação solicitada, parecer adequada, recomenda ajuste ou inadequada, com texto obrigatório nos dois últimos. A manifestação vale por ciclo de aprovação, é substituível pela mesma pessoa sem apagar a anterior e não bloqueia a decisão. O Representante que aprova ou recomenda ajuste diante de manifestação desfavorável vigente justifica, e a justificativa fica no ato da decisão. A escrita disputa o mesmo controle de concorrência da decisão do item (issue #2366).
- **Providência.** Quem tem `autorizacao:providencias:solicitar` pede, com fundamento, a retirada ou a revisão de uma concessão. Gestores e Representantes respondem com motivação ou atendem pela revogação vinculada, que usa o fluxo de revogação existente, com o mínimo de dois gestores. Os estados gravados são `aberta`, `respondida` e `atendida`; `prejudicada` é derivada na leitura quando a concessão deixou de estar ativa por outro caminho (issue #2367).
- **Leituras do Encarregado.** As consultas usam as mesmas projeções de quem administra, sem ampliar a visibilidade além da do gestor, e passam pelo ponto de decisão com registro de acesso. Consulta que devolve nome, texto livre ou anexo é no mínimo `Pessoal`, como na [ADR-0153](0153-projecao-por-protecao.md), e a falha no registro recusa a leitura (issue #2364). As listagens de pedidos, concessões e revisões governadas por permissão gravam um registro por item exibido, como a listagem de inscrições da ADR-0153, para qualquer pessoa que liste, porque a mesma consulta não tem duas políticas de registro. A lista dos próprios pedidos (`fila=meus`) fica de fora: ela é aberta a qualquer pessoa autenticada por titularidade, sem permissão de catálogo, e o ponto de decisão ainda não tem decisão permitida por titularidade da qual gerar o registro. Esse registro é tratado na issue #2389. As filas de decisão continuam exigindo a permissão da etapa: a consulta não as abre. `autorizacao:perfis:consultar-versoes` cobre os perfis, as versões e seus documentos, a janela de migração do perfil, a contenção, o detalhe da reclassificação e as divergências da governança; não cobre as filas de versões, de janelas e de reclassificações.
- **Avisos.** Os destinatários da segunda etapa são resolvidos pelo perfil de Representante com concessão vigente; manifestação e providência avisam seus próprios destinatários, sem texto livre nem dado pessoal (issue #2368).

### Revisão de acesso e segunda etapa do pedido

São atos distintos, e nenhum substitui o outro:

- A **segunda etapa do pedido** é prospectiva: decide, uma vez, se uma concessão protegida passa a valer. É do Representante e acontece antes de a concessão existir na decisão de acesso.
- A **revisão periódica de acesso** é retrospectiva: confirma ou revoga, item a item, concessões protegidas já vigentes. O Representante a conduz e decide os itens; os itens de quem é Representante são decididos pelos gestores. A revisão continua organizada por beneficiário.
- A **solicitação de revisão** feita pelo Encarregado por providência não é a revisão periódica: é um pedido dirigido a gestores e Representantes sobre uma concessão, que tramita pelos estados da providência.

**Instante de referência.** A condição de Representante do beneficiário de um item é avaliada no **instante da decisão do item**, e não na abertura da revisão. Na abertura, o item é criado sem fixar quem decide. Ao decidir, o sistema lê se o beneficiário tem concessão vigente de Representante naquele instante: se tem, só um gestor decide; se não tem, só um Representante decide. A designação concedida, revogada ou vencida durante a revisão aberta muda o decisor a partir dali, e a decisão grava o tipo de governança com que foi tomada.

**Concorrência.** Todo caminho trava os recursos numa ordem única, e nenhum trava um recurso anterior depois de um posterior: (1) a revisão, quando a operação é sobre um item; (2) o perfil, cuja linha, no perfil de Representante, é a trava do papel; (3) o pedido; (4) a pessoa, por emissor e `sub` (a trava da incompatibilidade), e, havendo várias, em ordem de emissor e `sub`; (5) a linha da concessão, ou a inserção dela; (6) a trava dos gestores, que só a contagem do mínimo de dois gestores toma. A decisão do item segue revisão, papel, pessoa do beneficiário e concessão. A aprovação que concede segue papel (no pedido de Representante), perfil, pedido e pessoa do beneficiário, lida com o pedido já travado porque só então ela é definitiva. A revogação segue papel (na concessão de Representante), pessoa (na concessão de governança), concessão e, se a concessão conta como gestor, a trava dos gestores. A instalação segue perfil de gestor, pessoas dos gestores e trava dos gestores, o que inverte a ordem anterior, em que a trava dos gestores vinha antes do perfil. Todo caminho que concede ou revoga concessão de governança toma a trava da pessoa antes da linha da concessão, de modo que a condição lida na decisão não muda até o fim da transação. O vencimento não precisa de trava: é conferido contra o instante da decisão. Duas decisões do mesmo item seguem o controle otimista da [ADR-0119](0119-padrao-concorrencia-otimista-handlers-wolverine.md): a primeira grava, a segunda recebe conflito e, ao reler, encontra o item decidido.

### Limitações registradas

- **Auditoria.** Os registros de acesso já são publicados no tópico Kafka, e a falha de publicação recusa a leitura ([ADR-0153](0153-projecao-por-protecao.md)). Retenção, controle de acesso e consulta desses registros ficam para incremento de prioridade baixa: até lá, o Encarregado não consulta no Uni+ quem acessou um dado, e a resposta depende de extração operacional. A proteção da trilha contra escrita direta no banco continua fora desta decisão, como na ADR-0157.
- **Assinatura do aceite.** O aceite do termo é ato eletrônico autenticado pelo login institucional, com a versão e o resumo do texto apresentado: equivale à assinatura eletrônica simples, e não à avançada nem à qualificada (Lei nº 14.063/2020, art. 4º). O resumo prova qual texto foi apresentado, não que foi lido. A força da autenticação é a do provedor de identidade, ainda sem multifator lido do token ([ADR-0152](0152-governanca-das-concessoes-de-acesso.md)). Se a Procuradoria exigir assinatura avançada, ela exige desenho próprio antes da produção.

### Migração

Não há produção. A base de HML é recriada nesta mudança, sem migração preservadora, sem revogação de concessões legadas, sem tratamento de pedidos antigos e sem camada de compatibilidade de nomes, contratos ou códigos. As migrations já aplicadas ficam como estão.

### Incrementos

1. Junto, em HML: papéis e composição (#2363), perfil do Encarregado e incompatibilidade (#2364) e termo com a tela de aceite (#2365). A #2363 trata dos valores do gestor e do Representante no tipo de governança; a #2364 acrescenta o valor do Encarregado e o perfil 3, com sua semente e composição. A conferência da reclassificação integrada (#2362) precede a migração.
2. Em seguida: manifestação na proteção do catálogo (#2366), providências (#2367) e avisos (#2368).
3. Posteriores: manifestação do Encarregado sobre versões de perfil protegido e consulta aos registros de acesso.

### Emendas

**À ADR-0152**, seções "Resultado da decisão" (papéis, fluxo, regras, instalação), "Consequências", "Confirmação" e "Mais informações":

- A aprovação da adequação à proteção de dados, a revisão periódica e a revogação pela segunda etapa passam do Encarregado de dados ao Representante do controlador; o Encarregado consulta, se manifesta e solicita.
- A regra "o pedido em que o Encarregado é beneficiário passa só pelo gestor" é revogada: o pedido protegido de um Representante passa por outro Representante. A designação do primeiro Representante usa a dispensa de designação.
- A regra "pedido que o Encarregado faz para outra pessoa espera a substituição formal" passa a valer para o Representante: o pedido que ele faz é decidido por outro Representante.
- A revisão periódica dos acessos do Representante fica com os gestores, com o decisor avaliado no instante da decisão do item.
- Revogam a qualquer momento o gestor e o Representante; o Encarregado não revoga.
- O beneficiário de pedido protegido aceita o termo antes da decisão do gestor.

**À ADR-0153**, seções "Aprovação da proteção de dados como pré-condição" e "Acesso auditado":

- A aprovação da classificação, da finalidade e da hipótese legal de tipos de documento e fatos é do Representante; o Encarregado registra manifestação técnica, que não bloqueia a decisão e, desfavorável, exige justificativa gravada no ato. O rebaixamento de classificação de coluna fixa registrado no PR segue a mesma divisão.
- A consulta da proteção de dados ao armazenamento consultável dos registros de acesso é a do Encarregado, e fica no incremento de prioridade baixa.

**À ADR-0157**, seções "Aprovação de versão", "Janela de migração", "Contenção", "Emendas à ADR-0152" e o parágrafo dos perfis de governança:

- Onde se lê proteção de dados como aprovador (versão protegida, etapa protegida da janela, retomada de perfil protegido, liberação e revisão da reclassificação), lê-se Representante do controlador.
- A dispensa `beneficiario_ja_tem_protecao_de_dados` deixa de continuar valendo, também na migração em lote.
- Os perfis de governança passam a ser três e deixam de reunir só permissões da autorização: admitem permissões de outros módulos desde que reservadas à governança, e recusam as não reservadas. Perfil comum recusa permissão reservada. O perfil do Encarregado também não se edita, desativa, reativa, versiona, migra pela janela nem reclassifica.

## Consequências

### Positivas

- Quem fiscaliza não decide o que fiscaliza, e a segunda aprovação continua com pessoa distinta do gestor.
- A acumulação de Representante e Encarregado é impossível por construção, inclusive sob concorrência.
- O Encarregado ganha consulta ampla, manifestação e providência, todas registradas, sem poder de decisão.
- A ciência do beneficiário fica registrada com o texto exato apresentado.

### Negativas

- Enquanto houver um só Representante, o pedido protegido dele fica sem quem decida a segunda etapa; por isso titular e substituto são pré-requisito operacional.
- O pedido protegido ganha uma etapa (aceite) antes do gestor.
- Toda concessão e revogação de governança disputa a trava da pessoa, e a de Representante também a trava do papel, e a decisão do item da revisão passa a ler a condição do beneficiário a cada vez.
- Até o incremento de auditoria, o Encarregado não responde pelo sistema quem acessou um dado.

### Neutras

- Os nomes técnicos da segunda etapa continuam dizendo "proteção de dados", pelo que a etapa confere.

## Confirmação

- Testes de composição dos três perfis, de aceite de permissão reservada de outro módulo em governança e de recusa da não reservada, e de recusa de permissão reservada em perfil comum.
- Testes de incompatibilidade nas duas ordens, na criação, no aceite e na aprovação, com transações concorrentes reais, com concessão ausente e vencida, e na instalação na ordem gestores, Representante, Encarregado, sem recusa na instalação.
- Teste de registro por item nas listagens e de recusa das filas de decisão a quem só consulta.
- Testes da segunda etapa sem a dispensa removida: pedido, decisão, lote, concessão retida e janela aberta.
- Testes da revisão com concessão, revogação e vencimento da designação de Representante durante a revisão aberta.
- Testes do termo nos três casos de aceite, de publicação concorrente com a criação do pedido (com transações reais: criação antes da publicação fixa a versão anterior; depois dela, responde conflito) e de resumo igual ao texto apresentado.
- Teste de duas designações simultâneas de Representante, de pessoas distintas, sem Representante vigente: só uma aplica a dispensa de designação, e a outra vai para a segunda etapa.
- Teste de perfil 3 isolado: consulta sem decidir, revogar ou aprovar, e recusa da leitura quando o registro não é entregue.

## Prós e contras das opções

### A — Perfil único com separação por norma

- Bom, porque não muda o sistema.
- Ruim, porque o sistema continua permitindo que quem fiscaliza decida, e nada impede a acumulação.

### B — Fim da segunda etapa, Encarregado só consultivo

- Bom, porque simplifica o fluxo.
- Ruim, porque o gestor passaria a decidir sozinho o acesso a dado pessoal, perdendo a segunda pessoa que a ADR-0152 exige.

### C — Três papéis com incompatibilidade

- Bom, porque separa decisão e fiscalização e mantém a dupla aprovação.
- Ruim, porque exige um perfil novo, o termo, a trava por pessoa e atos institucionais antes da produção.

## Mais informações

- **Emenda** a [ADR-0152](0152-governanca-das-concessoes-de-acesso.md), a [ADR-0153](0153-projecao-por-protecao.md) e a [ADR-0157](0157-perfis-de-acesso-versionados-e-aprovados.md), nas seções indicadas acima.
- [ADR-0119](0119-padrao-concorrencia-otimista-handlers-wolverine.md): conflito de concorrência otimista na decisão do item e na providência.
- [ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md): as permissões novas entram no catálogo em código.
- Épica unifesspa-edu-br/uniplus-api#2373; issues #2361 a #2368. Coordenação com #2317 e #2339.
- UNI-REQ-0184 (governança das concessões), cuja atualização para os novos papéis é tratada em unifesspa-edu-br/uniplus-developers#456.
- A entrada em produção depende dos atos institucionais (indicação do Representante do controlador, texto do termo de responsabilidade e o ato que o aprova, validação jurídica do nível de assinatura do aceite), acompanhados em unifesspa-edu-br/uniplus-developers#457. Eles não bloqueiam a implementação nem a HML.
