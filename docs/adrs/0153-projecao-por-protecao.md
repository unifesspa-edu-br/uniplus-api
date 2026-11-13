---
status: "proposed"
date: "2026-10-31"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Proteção de dados da Unifesspa (DPO) — consulta a enviar"
informed:
  - "Equipe Uni+"
---

# ADR-0153: Projeção por proteção — um DTO classificado, permissão por natureza e classificação, e acesso auditado

## Contexto e enunciado do problema

A [ADR-0081](0081-lgpd-by-design-dto-por-permissao.md) fez da projeção por permissão o controle primário de proteção do dado pessoal, com um DTO específico por permissão e o mascaramento na serialização como defesa secundária. Esse desenho supõe que os campos de cada resposta são conhecidos em compilação.

A inscrição não é assim. As respostas do formulário são fatos cadastrados em tempo de execução, cuja classificação e hipótese legal vêm do catálogo ([ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md)), e cada arquivo enviado tem a classificação do seu tipo de documento (UNI-REQ-0183). A ADR-0136 deixou isso pendente na seção "Proteção dos valores": como a projeção e a decisão de acesso alcançam fatos cuja classificação não está num DTO escrito à mão.

A consulta administrativa da inscrição (#2168) devolve respostas e arquivos de classes diferentes, e quem consulta pode ter permissão para uma classe e não para outra. Com cinco classes (`Publico`, `Interno`, `Pessoal`, `Identificador`, `Sensivel`) e duas naturezas (dado e arquivo), um DTO por combinação de permissões não se sustenta. Hoje, uma única permissão de consulta da inscrição cobre todo o dado, o arquivo não sensível de qualquer classe sai pela permissão de documentos pessoais, e a listagem registra a decisão sobre o processo, não sobre a inscrição.

O problema é garantir que cada resposta administrativa exponha só os itens que a permissão de quem consulta autoriza, item a item, para dado e arquivo, com o acesso ao dado protegido auditado, num mecanismo único que todo endpoint administrativo use.

## Drivers da decisão

- O dado que a pessoa não pode ver é omitido, seja sensível, seja pessoal.
- Um mecanismo único, integrado à permissão, sem endpoint nem DTO por permissão.
- O arquivo (documento enviado) tem permissão própria, separada da do dado (campo e resposta).
- O dado protegido segue a regra de acesso auditado do arquivo.
- Fatos cadastrados em tempo de execução são protegidos sem código novo.
- Os próximos módulos que mostram dado da pessoa (Ingresso, concessão de benefício e de bolsa, concurso) usam o mesmo mecanismo.
- Falha fechada: o que não tem classificação, permissão ou base legal não sai.

## Opções consideradas

- **A**: Serialização que omite ou mascara conforme o solicitante, como controle primário (opção A da ADR-0081).
- **B**: Um DTO por permissão (decisão vigente da ADR-0081).
- **C**: Filtro genérico por papel sobre uma entidade única de saída (opção C da ADR-0081).
- **D**: Projeção por proteção: um DTO administrativo cuja classificação é declarada por atributo e por item; um projetor central que, depois de decidido o acesso ao resource, decide cada item pelo par (natureza, classificação) no ponto de decisão único; serialização como defesa secundária, que omite.

## Resultado da decisão

**Escolhida:** "D — projeção por proteção", porque é a única que alcança os fatos cadastrados em tempo de execução sem multiplicar DTOs e mantém a decisão por permissão e contexto ([ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md)) como controle primário.

Não é a opção C: a decisão é por permissão e contexto, pedida ao ponto de decisão único, e o tipo de saída só é construído pelo projetor. Não é a opção A: o serializador continua sendo defesa secundária.

### Escopo

Vale para as rotas administrativas, inclusive exportação e relatório. O titular fica fora: acessa os próprios dados sem concessão ([ADR-0152](0152-governanca-das-concessoes-de-acesso.md)). A exceção é a leitura feita pelas partes de um pedido de concessão, registrada com a fonte `titularidade` desde a [ADR-0166](0166-registro-da-leitura-das-partes-do-pedido-por-titularidade.md).

### Vocabulário

No Uni+, **recurso** é termo de negócio: o recurso contra uma decisão, o recurso de acessibilidade, o recurso da etapa. O alvo técnico de uma decisão de acesso (o processo, a inscrição, o arquivo, o cadastro) é o **resource**, e o código de permissão segue o formato `{módulo}:{resource}:{ação}`. A decisão sem alvo próprio, inclusive a tomada por unidade, usa o tipo fixo `operacao`.

### Dois níveis de decisão

> O acesso ao resource é decidido antes da busca. A visibilidade de cada item é decidida antes da montagem da resposta.

1. **Acesso ao resource.** O endpoint barra no processo. O handler lê só o vínculo da inscrição (processo, situação, versão do formulário e cursos, sem decifrar nada); inscrição inexistente, de outro processo ou em rascunho responde sempre 404, e o registro segue a tabela do resource inexistente (seção "Acesso auditado"). Com o vínculo confirmado, decide `selecao:inscricoes:consultar` sobre a inscrição; sem permissão, responde 404 e nada do conteúdo é carregado.
2. **Visibilidade do item.** Com o acesso concedido, a inscrição é carregada, e cada item só entra na resposta se a permissão do seu par (natureza, classificação) for concedida. A projeção é feita no handler, sobre o que foi carregado, sem consulta condicional por classe.

A garantia é de **saída**: nada sai sem a permissão do par, e onde há máscara explícita, como o CPF, sai a forma mascarada. Esta decisão não promete que o item deixa de ser lido ou decifrado: o dado em claro na memória da aplicação é aceito. Fora da resposta, o canal de vazamento é log e telemetria, cobertos pelo mascaramento de log já em vigor.

O item omitido sai inteiro, sem indicador nem contagem. A decisão é sempre pedida ao ponto de decisão único, nunca lida das permissões efetivas que servem à interface.

**Forma da resposta.** Os campos fixos da entidade são propriedades tipadas, omitidas quando não permitidas; as respostas do formulário são itens tipados, todos da mesma forma, com rótulo e descrição congelados no processo e com `tipo` (como ler o valor) e `componente` (como exibir) sempre preenchidos pela API a partir do que o processo congelou. O identificador sai mascarado pela API, nunca em claro, com `podeRevelar` calculado por uma consulta de capacidade que não gera registro.

### Permissão por natureza e classificação

Cada módulo declara, no seu catálogo ([ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md)), uma permissão de consulta para cada par (natureza, classificação). A natureza é **dado** (campo e resposta) ou **arquivo** (documento enviado), vocabulário compartilhado declarado no próprio `[Permissao]`. Só a permissão que declara natureza participa da projeção; as ações (importar notas, gerar envio ao INEP) continuam decididas no endpoint.

Na Seleção:

| Natureza | Classificação | Permissão |
|---|---|---|
| — | acesso ao resource | `selecao:inscricoes:consultar` (passa a sensibilidade `Interno`) |
| Dado | Público | `selecao:dados-publicos:consultar` |
| Dado | Interno | `selecao:dados-internos:consultar` |
| Dado | Pessoal | `selecao:dados-pessoais:consultar` |
| Dado | Identificador | `selecao:dados-identificadores:consultar` (o valor mascarado) |
| — | revelar o valor completo | `selecao:dados-identificadores:revelar` (ação, natureza `Nenhuma`, decidida no endpoint com a do par (dado, identificador); a pedido; substitui `selecao:inscricoes:revelar-cpf`) |
| — | emitir o comprovante de inscrição | `selecao:inscricoes:emitir-comprovante` (ação, natureza `Nenhuma`, classificação `Sensivel` porque o comprovante reúne dado sensível decifrado; decidida no endpoint junto com `inscricoes:consultar`, também para a inscrição cancelada) |
| Dado | Sensível | `selecao:dados-sensiveis:consultar` |
| Arquivo | Público | `selecao:documentos-publicos:consultar` |
| Arquivo | Interno | `selecao:documentos-internos:consultar` |
| Arquivo | Pessoal | `selecao:documentos-pessoais:consultar` |
| Arquivo | Identificador | `selecao:documentos-identificadores:consultar` (#2151) |
| Arquivo | Sensível | `selecao:documentos-sensiveis:consultar` |

- `selecao:inscricoes:consultar` passa a liberar só o acesso ao resource, sem nenhum item. A situação e as ofertas escolhidas são dado `Publico` e saem com `dados-publicos`, como qualquer outro; não há regra fixa para classe baixa no código. **Exceção, o `id` da inscrição:** é o identificador técnico do resource, e não dado do candidato. Sai sempre que o acesso ao resource é concedido, na listagem e no detalhe, sem decisão de classe e sem registro próprio, porque é com ele que a pessoa abre o que já pode abrir. Nenhum dado de classe sai pelo acesso ao resource; a coluna `selecao.inscricoes.id` segue `Publico` no retrato da classificação. O processo (`id` e nome) é dado do processo, de leitura aberta depois da publicação, e também sai para quem tem o acesso ao resource. No detalhe também saem sempre, sem decisão de classe, `podeEmitirComprovante`, que é capacidade de quem consulta, e `numeroDoUltimoComprovante`, que é metadado da ordem de emissão sem dado do candidato.
- **Dado e arquivo são sempre permissões à parte:** a cópia de um documento real nunca sai por permissão de dado nem pela de acesso ao resource.
- **Nome:** `selecao:<natureza>-<classificação>:<ação>`. `revelar` marca a entrega a pedido, um registro por vez.
- **Por módulo, não por resource** (`dados-sensiveis`, não `inscricao-dados-sensiveis`): o alcance vem da permissão do resource, da unidade e, para a banca, da designação. Se surgir caso real de ver o sensível num resource e não em outro, a unicidade passa a aceitar o par por resource, declarado como exceção.
- **Unicidade:** em cada módulo, um par aponta para no máximo uma permissão de consulta. Par sem permissão significa que ninguém vê o item, e ele é omitido. A conferência roda na subida e entra na lista da ADR-0151.
- **Classificação não resolvida vale como `Sensivel`.**
- O arquivo `Publico` existe para a obra pública exigida pelo processo, como o artigo publicado. Mostrá-lo a quem não é servidor é publicação, ato do processo com rota própria, fora do projetor administrativo.
- A avaliação cega fica atendida por construção: quem recebe `dados-publicos` e `documentos-publicos`, sem `dados-pessoais` e sem `dados-identificadores:consultar`, não vê nome nem CPF mascarado.

### De onde vem a classificação

Um único resolvedor responde qual é a proteção de cada item:

| Item | Classificação | Conhecida em |
|---|---|---|
| Coluna fixa | declarada no modelo do EF | compilação |
| Fato coletado | a do fato, congelada na publicação | publicação |
| Fato derivado | a do derivado, congelada na publicação ([ADR-0154](0154-classificacao-da-modalidade-e-dos-opt-ins.md)) | publicação |
| Arquivo | a da exigência, congelada do tipo de documento (UNI-REQ-0183) | publicação |
| Valor derivado no DTO | declarada onde o valor nasce, com a transformação | compilação |

- **Valor derivado:** o identificador mascarado (o CPF no padrão `***.999.999-**`, os demais com máscara total) mantém a classe `Identificador` e é decidido pelo par (dado, `Identificador`), com `dados-identificadores:consultar`; já não sai por `dados-pessoais`. A fonte sem transformação nunca sai.
- **Classificação no modelo:** declarada por extensão Fluent na configuração do EF, o único lugar que alcança de forma uniforme records da Infrastructure, owned types e shadow properties, no mesmo padrão da [ADR-0124](0124-comentarios-schema-postgresql-via-hascomment-ef-core.md). Toda entidade declara uma classificação padrão, e a propriedade só é anotada quando difere. Os membros de auditoria (`CreatedBy`, `UpdatedBy`, `DeletedBy`) são classificados `Pessoal` uma vez, pela interface ou pela base. O owned type com dado pessoal declara a própria classificação, sem herdar a do dono. Coluna sem classificação resolvida reprova a conferência.
- **Inscrição:** padrão `Interno`; situação e ofertas `Publico` (o `id` sai pelo acesso ao resource); o índice cego do CPF `Identificador`; o dono da inscrição, quem a cancelou e o motivo do cancelamento `Pessoal`.
- **Teste de retrato:** o mapa coluna → classificação de todo o modelo fica num arquivo versionado, e qualquer mudança reprova o teste até o arquivo ser atualizado, de modo que todo rebaixamento aparece no diff. Rebaixar a classificação de coluna fixa exige aprovação da proteção de dados, registrada no PR.
- **O DTO administrativo declara a própria classificação**, por atributo na propriedade de coluna fixa e por item em cada resposta, documento e valor derivado. Uma conferência na subida garante que a propriedade lida direto da fonte não fica abaixo da classificação dela.
- Classificar, expor e cifrar são decisões diferentes: a cifra em repouso continua sendo da [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md).

### Base legal do item

A base legal do item de fato é a do próprio fato, congelada na publicação, como a exigência já congela a do tipo de documento. A base legal justifica tratar aquele dado para aquela finalidade; a permissão só exige que ela exista quando devolve dado sensível. Item `Sensivel` sem base legal congelada não sai.

### Revelação a pedido

Revelar é consulta de dado protegido, com a mesma decisão e o mesmo registro. `selecao:dados-identificadores:revelar` vale para qualquer campo classificado como `Identificador` (CPF, número do RG, do passaporte, do registro nacional migratório), sempre a pedido e um registro por vez, como manda a ADR-0152. **Revelar exige também a permissão da classe em que o item aparece** (para o identificador, `dados-identificadores:consultar`): quem só pode revelar, sem ver o item, não revela. O detalhe traz só a forma mascarada, para quem tem `consultar`; o valor real nunca sai sem pedido.

**Item do identificador.** Aparece, mascarado, só para quem tem a permissão da classe, e leva `podeRevelar: true` quando a pessoa também tem a de revelar; sem a da classe o item é omitido, mesmo para quem pode revelar. O `podeRevelar` vem de uma consulta de capacidade, sem registro. Resposta ausente ou vazia não gera item, para ninguém, e em grupo repetível há um item por ocorrência respondida.

**Ordem na rota.** A revelação não exige `inscricoes:consultar`. Valida o vínculo da inscrição com o processo, decide a permissão da classe e depois `revelar` na unidade dona, e só então procura a resposta do campo; sem qualquer das duas, a resposta existente e a ausente dão o mesmo 404. A recusa por falta da permissão da classe registra só a negativa dela, e nada é registrado como revelado.

Não há caso especial: o campo é referido pelo código do fato, mais a ocorrência quando está em grupo, numa operação única (`POST …/inscricoes/{inscricaoId}/campos/{fatoCodigo}/revelacao`), que substitui a revelação do CPF. O código do fato na rota não é dado pessoal ([ADR-0019](0019-proibir-pii-em-path-segments-de-url.md)). Todo dado da pessoa é fato coletado, e a inscrição guarda só o que é da entidade: o CPF fica só na resposta `CPF`, cifrado uma vez; o índice cego do CPF fica na inscrição, para a unicidade e o cruzamento com o INEP, e nunca é projetado.

### Acesso auditado

O acesso ao dado protegido segue a regra do arquivo: fica registrado, sem o conteúdo, no registro de decisão de acesso.

- **O registro significa "acesso autorizado à classe naquela inscrição", e não "dado entregue".** Para não denunciar o dado, o detalhe pede sempre as decisões de todas as classes de dado, inclusive `Identificador`, e de todas as classes de arquivo, sem olhar o conteúdo.
- **O projetor registra só a decisão permitida.** A negativa do projetor vira omissão silenciosa, porque quem perguntou foi o sistema, e registrá-la produziria falsas tentativas. Continuam registradas com os dois resultados a decisão de acesso ao resource, que é a tentativa real, e a revelação, que é pedido explícito.
- **O valor completo fica fora do "sempre":** o `podeRevelar` vem da consulta de capacidade, que não registra; a decisão de revelar só é pedida quando o valor é pedido. O registro da revelação leva o código do fato e a ocorrência.
- Ver os metadados do arquivo na lista (resource = inscrição) e abrir o arquivo (resource = arquivo) são registros distintos.
- **Listagem:** um registro por inscrição exibida. A decisão é uma só, sobre o processo, e dela saem os registros, cada um com a inscrição. Cada registro leva a decisão de classificação mais alta efetivamente exercida na linha (da mais alta para a mais baixa entre as pedidas: `dados-sensiveis`, `dados-identificadores`, `dados-pessoais`, `dados-internos`, `dados-publicos`; a classe do identificador entra pelo CPF da linha); `inscricoes:consultar` só quando a linha não exerceu classificação nenhuma. No detalhe vale o mesmo: o acesso ao resource registra o acesso, e cada decisão de classificação permitida registra o que foi exercido.
- **A política de registro está num tipo só, e o ponto de decisão só decide.** Quem avalia o acesso não grava; quem grava aplica a política, que recebe as decisões avaliadas e a intenção de quem perguntou. O pedido que pode ser recusado grava a decisão que o autorizou (a primeira, havendo alternativas) ou, se nenhuma autorizou, as negativas, em qualquer sensibilidade. A decisão permitida só grava a partir de `Pessoal`, para que a operação do dia a dia não dependa do broker. A etapa de aprovação grava todas as permissões que habilitaram a aprovação. A projeção grava toda classe permitida exercida, de qualquer sensibilidade, e nunca a negada. A listagem grava uma entrada por inscrição exibida. A consulta de capacidade nunca grava, e o token que não identifica o solicitante não gera registro, só o `401`. O registro leva o `resultado` (`permitido`, `negado` ou `impedido`) no valor canônico.
- **Toda negativa por falta de permissão é registrada no ponto que recusa o pedido**, em qualquer módulo e sensibilidade: o filtro de borda e a decisão do handler que responde 403 ou 404. Com alternativas (a permissão principal ou a de "em alguma unidade"), se uma permite nenhuma negativa é publicada, e se todas negam publica-se a de cada uma. A alternativa que exige concessão em unidade e só encontrou concessão global é negada com o motivo `concessao_em_unidade_exigida`. O impedimento por inscrição também é publicado. A pergunta de capacidade (escolher a visão, montar o catálogo, `podeRevelar`, a classe omitida na projeção) não é negativa e não grava; quando o resultado dela leva à recusa do pedido, publicam-se as negativas que a fundamentam, como no catálogo de proteção. O token sem identidade completa não gera registro, só o `401`.
- **Resource inexistente.** O processo da rota que não existe não gera registro: sem unidade, não há sobre o que decidir. Com o processo existente, a inscrição inexistente, de outro processo ou em rascunho, pedida sem a permissão da rota (`inscricoes:consultar` no detalhe; na revelação, a da classe e `revelar`, e só a primeira negada é registrada) na unidade dona, publica a negativa com o motivo real e **sem o id do resource**, e a resposta é o mesmo 404; a ausência do id é o que permite à trilha reconhecer a sondagem. Com a permissão da rota, é link errado e nada é publicado. O documento da inscrição inexistente continua sem registro: a permissão da rota depende da classificação do documento, que não existe.
- **Falha ao publicar uma negativa responde 503**, como a do permitido sensível, e nenhuma negativa se perde da trilha. Com o broker fora, quem tem permissão abaixo de `Pessoal` continua trabalhando, e só quem é recusado vê "serviço indisponível" em vez de "acesso negado".
- **Falha ao gravar o registro recusa a requisição**, nunca vira omissão. Na listagem, a página só sai depois de gravados todos os registros, num lote que não fica pela metade nem duplica em nova tentativa. A gravação durável registra também o ator.
- **O registro vai para um tópico Kafka próprio, fora da base da aplicação**, publicado de forma síncrona: a requisição só segue depois de o broker confirmar a escrita replicada (`acks=all`, produtor idempotente), e sem confirmação a requisição é recusada. A credencial da aplicação só escreve no tópico. A base da aplicação não guarda a trilha, para que quem alcança a base não alcance o registro e para que a trilha não dispute a base com o negócio. O outbox transacional ([ADR-0004](0004-outbox-transacional-via-wolverine.md)) não serve aqui, porque grava primeiro na base da aplicação. Cada registro leva uma chave derivada do acesso, que deixa o consumo idempotente ([ADR-0014](0014-kafka-como-bus-assincrono-inter-modulos.md)).
- **A chave da mensagem é a execução do servidor** (uma por requisição HTTP), de modo que todos os lotes de uma requisição caem na mesma partição, na ordem de publicação. Entre requisições não há garantia de ordem: o instante é informação temporal aproximada, e nenhum consumidor previsto precisa dessa ordem. O `requestId` não serve de chave porque o cliente o controla. Um lote com execuções diferentes é erro de programação e lança antes de produzir.
- **Cabeçalhos `uniplus.*`** (e `content-type` e `traceparent`): tipo, versão do schema, execução, instante de produção, client OIDC, quantidade, resultado (`permitido`, `negado`, `impedido` ou `misto`) e sensibilidade máxima do lote. Nunca levam o ator, o nome, o e-mail, o CPF, o `requestId` do cliente nem o identificador de inscrição ou o código do fato, porque as ferramentas de inspeção do broker os exibem sem a proteção do conteúdo.
- **O valor tem schema versionado** (JSON Schema draft 2020-12, em `contracts/kafka/registro-de-acesso/v1.schema.json`), que admite propriedades adicionais. Só entra mudança compatível para trás na `v1` (campo opcional novo; motivo de negativa novo atualiza o `enum` de `motivoNegativa`); mudança incompatível cria a `v2` e muda o cabeçalho de versão. O registry é o Apicurio Registry (Apache 2.0, já na stack local), por ser a infraestrutura só de software open source e gratuito; o registro do schema nele é trabalho à parte, para a API não depender do registry para subir.
- **O armazenamento consultável é um serviço à parte**, que consome o tópico e guarda os registros numa base própria; a consulta da proteção de dados ("quem acessou esta inscrição") lê dele. É decisão própria, posterior a esta.
- Isso muda o contrato do registro: hoje ele não condiciona o veredito, registra toda decisão e não sabe gerar vários registros de uma decisão. A mudança é a da #2162, que **bloqueia** o projetor.
- O detalhe responde com `Cache-Control: no-store` e não é pré-carregado pela interface a partir da listagem.

### Serialização como defesa secundária

Um modificador do serializador lê a classificação declarada no DTO e as decisões já tomadas na requisição, e **omite** o campo classificado sem decisão permitida, sem pedir a decisão de novo. O alcance é global e por tipo: o modificador está instalado em toda serialização, e só age em tipo que declara classificação, por `[ProtegidoComo]` ou por derivar de `ItemClassificado`. Esses tipos ficam no namespace convencionado `*.DTOs.Administrativos` de cada módulo, e um teste de arquitetura confere a convenção. As respostas, que hoje trafegam como dicionário, viram itens que carregam natureza e classificação, porque o modificador não filtra entradas de dicionário. A defesa na serialização não alcança exportação nem *streaming*; esses caminhos passam pelo projetor.

### Lugar do mecanismo

- **`shared`:** o vocabulário de natureza e a conferência de unicidade; a classificação no modelo, a conferência do modelo e o teste de retrato, para todo `DbContext`; o contrato do item classificado (natureza, classificação, base legal); o núcleo do projetor (decide por par, omite, registra); a porta pela qual cada módulo responde qual é a sua permissão para cada par; a serialização defensiva.
- **Módulo:** o resolvedor dos próprios itens, a implementação da porta com o enum de permissões dele e a montagem do DTO. Um módulo novo escreve só esses dois.
- **Item lido de outro módulo por reader ([ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md)):** o reader entrega o item classificado, e o consumidor o projeta pela própria permissão. A base legal é a do uso pelo consumidor, registrada para a finalidade dele, e não a congelada na Seleção.

### Exportação e relatório

Passam pelo projetor, na aplicação. View de banco está descartada: o banco não decide permissão nem registra acesso, e o dado cifrado não se lê no banco. A "visão controlada" da emenda 1.3 da ADR-0081 é lida como esta projeção.

### Ponto de extensão: banca avaliadora

A banca não é desenhada aqui, mas a base não a bloqueia:

- o resource da banca é a avaliação da inscrição na etapa, com só os fatos e documentos que a etapa declara analisar; o recorte vem do resource, e as permissões de classificação continuam por módulo;
- o escopo é a designação da banca (processo, fase, etapa e código da banca), fato da Seleção conferido a cada acesso por verificação de contexto declarada na permissão; revogar a designação corta o acesso na hora;
- a concessão de quem integra a banca é na unidade da fase ou da etapa da banca, conforme a [ADR-0159](0159-acao-interna-da-fase-decidida-na-unidade-responsavel-pela-fase.md), e a banca da própria fase entra no mesmo recorte;
- o motor de autorização não conhece escopo de negócio: a concessão diz o que a pessoa pode fazer, em que unidade e até quando, e o escopo de negócio entra pela verificação do módulo. Por isso a chamada e o processo deixam de ser escopo de concessão no motor, em frente própria: o escopo de negócio da concessão excepcional da [ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md) (processo, fase, banca) passa à designação, e a concessão excepcional fica com unidade e tipo de resource.

### Aprovação da proteção de dados como pré-condição

Todo tipo de documento e todo fato do catálogo, em qualquer classificação, passa pela aprovação da proteção de dados na criação e quando muda a classificação, a hipótese legal ou a finalidade, inclusive o catálogo semeado. No fato de sistema, a classificação, a hipótese legal e a finalidade deixam de ser fixas na semente: a semente dá só o valor inicial, e a mudança passa pela tela e pela mesma aprovação; a definição que o código do sistema calcula ou lê continua travada. O projetor respeita a classificação como está, então esse fluxo é o que impede, por exemplo, um laudo cadastrado como `Interno` por engano. Ele entra **no mesmo deploy** do projetor. As permissões sobre dado pessoal, identificador ou sensível seguem a dupla aprovação da ADR-0152.

### Contrato

Sem produção nem integrador, as mudanças de contrato (respostas como itens tipados, identificador mascarado, itens omitidos, revelação por campo no lugar da revelação do CPF, `selecao:inscricoes:revelar-cpf` fora do catálogo, o `sub` de quem abriu a retificação fora da resposta) entram na versão `v1` do contrato, sem incremento de versão, registradas no PR. A política de mudança incompatível do princípio 5 da [ADR-0022](0022-contrato-rest-canonico-umbrella.md) e da [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) passa a valer com o primeiro integrador.

## Consequências

### Positivas

- Um fato novo no catálogo é protegido pela sua classificação, sem DTO nem código novo.
- Dado e arquivo de cada classe têm permissão própria, e a avaliação cega sai por construção.
- O acesso ao dado protegido é auditável por inscrição, inclusive na listagem.
- O núcleo serve aos próximos módulos, que escrevem só o resolvedor e a porta.

### Negativas

- Onze permissões de consulta na Seleção, e os perfis precisam combiná-las.
- A listagem grava um registro por inscrição exibida.
- O dado lido e decifrado continua na memória da aplicação; a garantia é só de saída.
- A classificação da coluna fica longe da classe; as conferências do modelo e do DTO cobrem a divergência.
- O projetor depende da #2162, da classificação no modelo e do fluxo de aprovação da proteção de dados.

### Neutras

- A cifra em repouso continua com a ADR-0121, e a forma das respostas da inscrição com a [ADR-0141](0141-respostas-da-inscricao-em-tabela.md).

## Confirmação

- Conferência na subida: unicidade do par (natureza, classificação) em cada módulo, em todas as classes.
- Conferência do modelo: toda coluna mapeada resolve classificação; teste de retrato do mapa coluna → classificação.
- Conferência do DTO: a propriedade lida direto não fica abaixo da fonte; o valor derivado vale pela classificação própria, com a transformação declarada.
- Teste do acesso ao resource: inscrição de outro processo, inexistente ou em rascunho responde 404; sem a permissão da rota a negativa é registrada sem o id do resource, com ela nada é registrado; na recusa, nada do conteúdo é carregado.
- Teste de exposição (BOPLA) do mecanismo: sem a permissão do par, o item não aparece; com ela, aparece.
- Teste da avaliação cega: com `dados-publicos` e sem `dados-pessoais` nem `dados-identificadores:consultar`, detalhe e listagem não trazem nome nem CPF mascarado, e o fato e o documento `Publico` aparecem.
- Teste da base legal: item `Sensivel` sem base legal congelada não sai.
- Teste do contrato: todo item traz `tipo` e `componente`; o identificador sai mascarado, e o cálculo do `podeRevelar` não grava registro.
- Teste do registro: no detalhe, as decisões de todas as classes, inclusive a do dado `Identificador`, são pedidas e só a permitida é registrada; a revelação registra os dois resultados e o código do fato, e quem só pode revelar não revela; a listagem grava um registro por inscrição com a decisão mais alta exercida; a falha de gravação recusa a requisição.
- Teste da mensagem: a chave é a execução, os cabeçalhos têm os valores esperados e nenhum leva dado do ator, e o lote serializado valida contra o schema versionado, com o mesmo conjunto de propriedades.

## Prós e contras das opções

### A — Serialização como controle primário

- Bom, porque é um ponto só.
- Ruim, porque é contornada por exportação, *streaming* ou outra serialização, como a ADR-0081 já registrou.

### B — DTO por permissão

- Bom, porque o que não é projetado não vaza.
- Ruim, porque não alcança fatos cadastrados em tempo de execução e exige um DTO por combinação de permissões.

### C — Filtro por papel sobre entidade única

- Bom, porque há um só tipo de saída.
- Ruim, porque o papel não acompanha a granularidade da permissão e do contexto.

### D — Projeção por proteção

- Bom, porque decide item a item pela permissão, com a classificação que o próprio dado carrega, e serve a todo módulo.
- Ruim, porque exige classificar todo o modelo, mudar o contrato do registro e manter mais permissões.

Descartados também, dentro da opção escolhida: a permissão de acesso ao resource liberando a classe baixa (seria regra fixa fora do catálogo); permissão por resource (multiplica permissões sem caso real); view de banco para relatório (o banco não decide nem registra); um registro por página com a lista de inscrições (não responde quem viu uma inscrição e muda o contrato do registro); registrar a negativa do projetor marcada como sondagem (guarda o que ninguém consulta); classificação por marcador de inclusão no modelo (falha aberta).

## Mais informações

- **Emenda a [ADR-0081](0081-lgpd-by-design-dto-por-permissao.md):** o DTO por permissão é substituído pelo DTO único classificado, com o projetor como controle primário; a defesa secundária passa de mascarar a omitir; a "visão controlada" da emenda 1.3 é a projeção na aplicação.
- **Emenda a [ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md):** o `[Permissao]` declara a natureza, e a unicidade do par entra nas conferências da subida.
- **Responde à pendência da [ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md)** ("Proteção dos valores"): o fato cadastrado em tempo de execução é projetado pela classificação e pela base legal congeladas no processo. O armazenamento e a cifra seguem a ADR-0141 e a ADR-0121.
- **Emenda à governança da [ADR-0136](0136-catalogo-de-fatos-do-candidato-administravel.md)**: a classificação do fato de sistema deixa de ser fixa na semente e passa à aprovação da proteção de dados.
- [ADR-0152](0152-governanca-das-concessoes-de-acesso.md) não muda: a revelação a pedido segue a regra do CPF dela, agora para todo identificador.
- [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): ponto de decisão único e verificações de contexto; a chamada, listada lá entre os escopos da concessão, sai do motor pela banca acima.
- [ADR-0084](0084-concessao-excepcional-e-atuacao-institucional-server-side.md): a concessão excepcional deixa de ser escopada por processo, chamada ou banca; esse escopo passa à designação, e a concessão fica com unidade e tipo de resource.
- [ADR-0082](0082-nome-social-publico-nome-civil-pessoal.md): o nome social e o nome civil são `Pessoal` na consulta administrativa; o que a ADR-0082 chama de público é a forma de identificação nos pontos de publicação, que não passa pelo projetor.
- [ADR-0154](0154-classificacao-da-modalidade-e-dos-opt-ins.md): classificação da modalidade e congelamento da classificação do derivado.
- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) e [ADR-0028](0028-versionamento-per-resource-content-negotiation.md): contrato sem incremento de versão enquanto não há integrador.
- Issues: #2162 (registro durável, decidir sem registrar a negativa, consulta de capacidade sem registro, registros em lote), #2151 (permissões dos arquivos `Publico`, `Interno` e `Identificador`), #2168 (detalhe da inscrição), #2169 (padronização do tipo de resource).
- UNI-REQ-0130, UNI-REQ-0183 e UNI-REQ-0184.
- Emendada pela [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md): a aprovação da proteção do catálogo é do Representante do controlador, com manifestação técnica não bloqueante do Encarregado de dados; a consulta dos registros de acesso pela proteção de dados é do Encarregado e fica para incremento posterior.

## Emenda: manifestação técnica na proteção do catálogo de tipos de documento e de fatos do candidato (#2366)

A [ADR-0163](0163-manifestacao-tecnica-por-ciclo-na-protecao-do-catalogo.md) emenda a seção "Aprovação da proteção de dados como pré-condição". O circuito de aprovação de cada tipo de documento e de cada fato ganha o número do ciclo, que avança a cada reinício de item já solicitado, e o estado das manifestações técnicas do ciclo. O ato da aprovação passa a guardar a justificativa, que é obrigatória na aprovação e no ajuste diante de manifestação desfavorável vigente.

## Emenda: leitura das partes de um pedido de concessão registrada por titularidade (#2389)

A [ADR-0166](0166-registro-da-leitura-das-partes-do-pedido-por-titularidade.md) emenda a seção "Escopo". A leitura feita pelo solicitante ou pelo beneficiário de um pedido de concessão (a fila dos próprios pedidos, o detalhe, a trilha e o anexo) deixa de ficar fora do registro de acesso: cada item exibido gera um registro permitido com a fonte (`fonteGrant`) `titularidade` e a permissão `autorizacao:pedidos:ler-proprios`, código fixo fora do catálogo de permissões e não concedível, e a falha ao gravar responde 503 sem dado, como nas demais leituras registradas. A parte que tem `autorizacao:pedidos:consultar` continua registrada por essa permissão. As demais leituras do titular, como as próprias inscrições, continuam fora. O schema do tópico continua na `v1`.
