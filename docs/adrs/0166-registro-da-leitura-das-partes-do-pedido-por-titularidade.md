---
status: "proposed"
date: "2026-11-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0166: Leitura das partes de um pedido de concessão registrada por titularidade, fonte que só existe no registro de acesso

## Contexto e enunciado do problema

A [ADR-0153](0153-projecao-por-protecao.md) manda registrar no tópico `autorizacao_registros_de_acesso`, antes da entrega, todo acesso permitido a dado a partir de `Pessoal`, e recusar a leitura com 503 quando o registro não é gravado. A mesma ADR deixa o titular fora desse escopo: ele acessa os próprios dados sem concessão ([ADR-0152](0152-governanca-das-concessoes-de-acesso.md)). A [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md) estendeu o registro por item às listagens de pedidos, concessões e revisões governadas por permissão (issue #2364) e deixou de fora a lista dos próprios pedidos (`fila=meus`), remetendo esse registro à issue #2389.

A lacuna é maior que a lista. Pedido de concessão tem duas partes: o solicitante e o beneficiário, este identificado pelo identificador do token ou, antes do aceite, pelo e-mail verificado. O dado de terceiro corre nos dois sentidos: o solicitante vê o nome e o e-mail do beneficiário, e o beneficiário vê a justificativa, texto livre escrito pelo solicitante. Hoje a leitura feita por uma parte não grava nada na fila dos próprios pedidos, no detalhe, na trilha nem no anexo do pedido.

A issue #2406 passou a registrar essas leituras quando a parte tem `autorizacao:pedidos:consultar`, como o Encarregado de dados (UNI-REQ-0372): a leitura é governada por essa permissão e registrada por ela. Quem não tem a permissão continua lendo sem registro, porque o ponto de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) só permite por concessão, e o acesso da parte não vem de concessão. Sem decisão permitida, não há de onde gerar o registro.

Em 13/11/2026 o líder técnico decidiu que a leitura feita pelas partes de um pedido de concessão, na fila dos próprios pedidos, no detalhe, na trilha e no anexo, é registrada no tópico `autorizacao_registros_de_acesso`, sem consumo por ora (UNI-REQ-0379), com a origem "titularidade" presente só no registro e sem autorizar nada, no schema `v1`. Esta ADR decide como representar esse registro sem fazer da titularidade uma fonte de concessão, o que contrariaria a ADR-0152.

## Drivers da decisão

- **Toda leitura de dado pessoal fica na trilha antes da entrega** (UNI-REQ-0203, UNI-REQ-0372), inclusive a feita por quem é parte, porque a parte vê dado da outra.
- **O token identifica e não concede; toda permissão é satisfeita por concessão gravada** ([ADR-0152](0152-governanca-das-concessoes-de-acesso.md), UNI-REQ-0184). O acesso do titular não é concessão e não pode virar uma.
- **O registro tem de distinguir o acesso do titular do acesso de terceiro** (UNI-REQ-0203), para que a futura consulta da proteção de dados (UNI-REQ-0379) separe os dois sem cruzar outra base.
- **O ponto de decisão só decide; quem grava aplica a política** ([ADR-0153](0153-projecao-por-protecao.md)). Mudar a ordem da decisão afeta todas as permissões de todos os módulos.
- **Contrato do tópico estável.** A `v1` só recebe mudança que não quebra o leitor da `v1` ([ADR-0153](0153-projecao-por-protecao.md), `contracts/README.md`); não há consumidor hoje.
- **Uma leitura, um registro.** A precedência entre a leitura pela permissão (issue #2406) e a leitura por titularidade não pode gerar dois registros nem nenhum.
- **Limites de código da [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md).** O desenho cabe em métodos de até 15 linhas e não aumenta a lista de parâmetros de tipos que já estão no limite.

## Opções consideradas

- **A — Titularidade no ponto de decisão.** Uma permissão do catálogo de permissões para os próprios pedidos, permitida pelo ponto de decisão quando o pedido é de quem pergunta, com a titularidade como fonte de concessão efetiva.
- **B — Registro sem decisão.** Uma operação nova grava a leitura da parte sem decisão permitida, com `fonteGrant` nulo, com `permissao` ausente (o que exige a `v2`) ou com um campo opcional novo para dizer a base do acesso.
- **C — Titularidade como fonte só do registro.** A leitura da parte gera um registro permitido cuja fonte (`fonteGrant`) é `titularidade` e cuja permissão é um código fixo da operação do titular, fora do catálogo de permissões e não concedível. Esse registro é montado só pelo verificador de acesso e nunca chega ao ponto de decisão.

## Resultado da decisão

**Escolhida:** "C — titularidade como fonte só do registro", porque é a única que registra a leitura da parte pelo mesmo caminho, no mesmo lote atômico e com a mesma recusa das demais leituras registradas, dizendo no próprio registro que o acesso veio da titularidade, sem criar permissão satisfeita fora de concessão (ADR-0152) e sem mudar o schema `v1`.

**Alcance.** São registradas as leituras feitas por uma parte do pedido de concessão, solicitante ou beneficiário (este também pelo e-mail verificado antes do aceite): cada pedido exibido na fila dos próprios pedidos, o detalhe, a trilha e cada leitura de anexo. Todo item exibido à parte é registrado, inclusive o pedido feito para si mesmo, sem filtrar só os que trazem dado de outra pessoa: o filtro exigiria decidir, item a item, se há terceiro, e o registro deixaria de ser previsível para quem consulta a trilha (UNI-REQ-0372). As leituras do titular nos outros módulos, como as próprias inscrições e os arquivos do candidato na Seleção, continuam fora do registro, como na ADR-0153.

**Representação no registro.** O registro por titularidade usa os campos que a `v1` já tem:

- `permissao` leva o código fixo `autorizacao:pedidos:ler-proprios`, que identifica a operação. O código não está no catálogo de permissões da [ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md), não entra em perfil, não se concede e não é aceito em pedido de concessão.
- `fonteGrant` vale `titularidade`. O valor é documentado na descrição do campo na `v1`, ao lado de `concessao`, `oidc_group_binding` e `permissao_excecional`; o campo continua sem enum fechado.
- `resultado` é `permitido`, e `permitido` é verdadeiro.
- `grantId`, `perfilId`, `versaoId` e `escopoUnidadeId` são nulos, porque não há concessão.
- O alvo (tipo de resource, identificador do pedido) e a sensibilidade são os mesmos que a leitura pela consulta dos pedidos gravaria; a sensibilidade é `pessoal`, como a de `autorizacao:pedidos:consultar`, porque a leitura devolve nome, texto livre ou anexo ([ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md)).
- O campo `origem`, que é o canal da requisição, não muda. "Titularidade" é valor de `fonteGrant`, não de `origem`.

**Solicitante e beneficiário não se distinguem no registro.** Há um único valor de fonte, `titularidade`. Quem leu está no ator (`atorEmissor` e `atorSubject`); o papel se deduz do pedido: o solicitante é sempre identificado pelo identificador do token, e qualquer outra parte que leu é o beneficiário. No pedido feito para si mesmo, o ator é as duas partes. Não há campo novo nem segundo valor de fonte.

**Só o verificador cria a titularidade, e ela nunca entra no ponto de decisão.** A porta do verificador de acesso ganha uma operação que registra a leitura da parte para um conjunto de pedidos exibidos. A implementação monta a concessão efetiva de titularidade, sem unidade nem escopo, e a decisão permitida que a carrega, e reaproveita a política de registro por resource e a gravação em lote que as listagens já usam. A concessão de titularidade nunca está nas concessões efetivas do sujeito. Se estivesse, não satisfaria nada: a escolha da concessão aplicável recusa explicitamente a fonte `titularidade`, qualquer que seja o escopo, e também a concessão sem unidade nem escopo institucional, então a falha é fechada em duas camadas.

**Precedência: uma leitura, um registro.** A parte que tem `autorizacao:pedidos:consultar` continua registrada por essa permissão, com a fonte da concessão que a deu (issue #2406). A titularidade vale só para quem não a tem. A pergunta pela consulta dos pedidos é consulta de capacidade: a resposta negativa não é recusa e não grava negativa. Nenhuma outra permissão sobre pedidos (decidir uma etapa, administrar concessões) muda a precedência, porque só a consulta dos pedidos governa a leitura da parte. Token sem identidade completa responde 401 sem registro, antes de qualquer gravação.

**Página vazia não grava nada.** A fila dos próprios pedidos sem item exibido não gera registro e não depende do tópico.

**Disponibilidade: o registro é obrigatório também para as partes.** Decisão do líder técnico de 13/11/2026: o comportamento é o mesmo de todas as leituras registradas. Se o registro não for gravado, a leitura da parte responde 503 sem dado, na fila dos próprios pedidos, no detalhe, na trilha e no anexo. A página só sai depois de gravados todos os registros dos itens exibidos, num lote que não fica pela metade nem duplica em nova tentativa na mesma execução ([ADR-0153](0153-projecao-por-protecao.md)). A alternativa considerada, entregar a leitura sem registro com o tópico fora, foi descartada porque deixaria lacuna na trilha justamente quando o registro falha e criaria uma segunda política de registro para a mesma consulta, o que a ADR-0162 já recusou.

**Contrato.** O schema continua na `v1`, e o cabeçalho `uniplus.versao-schema` continua `1`. Mudam só as descrições de `fonteGrant` (o valor `titularidade`, que só aparece no registro e não representa concessão) e de `permissao` (na leitura por titularidade, o código fixo da operação do titular). Nenhuma propriedade nova é serializada, e nenhum enum muda. O leitor da `v1` lê o registro novo sem alteração.

**Relação com outras decisões.** Esta ADR emenda o escopo da [ADR-0153](0153-projecao-por-protecao.md) ("O titular fica fora"): a exceção é a leitura feita pelas partes de um pedido de concessão. Emenda o vocabulário de fonte registrada da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): `titularidade` passa a ser valor de `fonteGrant` no registro, sem ser fonte de concessão efetiva; as três fontes possíveis da concessão efetiva e a regra da ADR-0152 de que toda permissão é satisfeita por concessão gravada não mudam. Cita a [ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md), cuja remissão à issue #2389 na regra "Leituras do Encarregado" é retirada quando esta ADR for aceita. Não contradiz a [ADR-0152](0152-governanca-das-concessoes-de-acesso.md) nem a [ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md): nenhuma permissão nova entra no catálogo de permissões, e nenhum acesso passa a ser satisfeito sem concessão.

## Consequências

### Positivas

- Toda leitura de pedido de concessão fica na trilha, por quem quer que leia, e a futura consulta da proteção de dados separa o acesso do titular do acesso de terceiro pelo valor de `fonteGrant`, sem cruzar a base da aplicação.
- A concessão efetiva, o tipo do registro e o catálogo de permissões não mudam, e o ponto de decisão só ganha a recusa explícita da fonte `titularidade`; a mudança fica na porta e na implementação do verificador de acesso, no vocabulário canônico da fonte e no módulo de autorização. Os outros módulos não são tocados.
- O schema continua na `v1`, sem campo novo e sem versão nova.
- A leitura da parte segue a mesma regra de lote atômico, idempotência por chave e recusa por indisponibilidade das demais leituras registradas: uma política só.

### Negativas

- **A lista dos próprios pedidos de qualquer pessoa passa a depender do tópico.** Com o tópico fora, toda pessoa autenticada que tenha ao menos um pedido, inclusive quem acompanha o primeiro pedido de acesso, recebe 503 na própria lista, no detalhe, na trilha e no anexo. Hoje essa leitura funciona com o broker fora. Só a lista vazia continua respondendo.
- Cresce o volume do tópico: um registro por pedido exibido a cada página da fila dos próprios pedidos, mais o detalhe, a trilha e o anexo, inclusive no pedido feito para si mesmo, que não expõe dado de outra pessoa.
- Passa a existir no registro um registro permitido cuja permissão não está no catálogo de permissões. A futura consulta da proteção de dados precisa conhecer o código fixo da operação do titular e o valor `titularidade` para não tratá-los como permissão desconhecida.
- O arquivo do vocabulário canônico, tocado para receber `titularidade`, tem de sair dentro dos limites da [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md); o mapeamento dos motivos de negativa que hoje passa deles é refatorado na mesma mudança.
- O UNI-REQ-0203 diz hoje que o candidato, ao acessar os próprios dados, não gera registro de acesso de terceiro, e não prevê o registro da leitura das partes do pedido de concessão. A implementação depende da emenda dele no portal de desenvolvedores.

### Neutras

- Não há dado a migrar: o registro existe só no tópico, a leitura passada não é reconstituída, e a base de pré-produção é recriada.
- Soft delete e retificação não se aplicam ao registro: ele é evidência só acrescentada, fora do soft delete, como a evidência forense da [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md). O pedido de concessão não tem exclusão lógica: em qualquer situação, inclusive as terminais, ele continua listado à parte e cada leitura é registrada.
- Concorrência: o registro reflete o pedido como foi lido. O aceite pelo beneficiário entre a leitura e a gravação não muda o registro, porque o papel se deduz do pedido e do ator, e o ator já era parte no instante da leitura.
- O registro não leva dado pessoal novo: o ator continua pelo subject opaco, e nome, e-mail e justificativa nunca entram no registro nem nos cabeçalhos.

## Confirmação

- Teste de arquitetura: só a implementação do verificador de acesso cria a fonte `titularidade` (além do mapeamento canônico do valor), nem o serviço de decisão nem a montagem das concessões efetivas do sujeito a criam, e a escolha da concessão aplicável só a cita para recusá-la. A decisão permitida só é criada pelo serviço de decisão e pelo verificador.
- Teste do catálogo de permissões: `autorizacao:pedidos:ler-proprios` não está no catálogo de permissões e é recusado em perfil e em pedido.
- Testes unitários da operação nova do verificador (registro permitido, fonte `titularidade`, campos da concessão nulos, sensibilidade `pessoal`) e do vocabulário canônico.
- Teste do schema `v1`: o lote com registro por titularidade é válido e não serializa propriedade não declarada.
- Testes da fila dos próprios pedidos, do detalhe, da trilha e do anexo: um registro por item exibido para a parte sem a consulta dos pedidos; nenhum registro de titularidade para a parte com a consulta (registro pela permissão, issue #2406); página vazia sem registro; 401 sem registro para identidade incompleta.
- Testes do Host com o registro falhando, no padrão dos de falha ao registrar as listagens: 503 sem dado na fila dos próprios pedidos, no detalhe, na trilha e no anexo, e nenhum registro parcial.

## Prós e contras das opções

### A — Titularidade no ponto de decisão

- Bom, porque a leitura da parte teria decisão permitida comum, e o registro seguiria o caminho das demais leituras sem operação nova.
- Ruim, porque contraria a ADR-0152 (accepted): uma permissão do catálogo de permissões satisfeita sem concessão gravada.
- Ruim, porque muda a ordem do ponto de decisão, que vale para todas as permissões de todos os módulos, e obriga o catálogo de permissões a ter permissão não concedível, com reflexo nas regras de composição de perfil e nos artefatos gerados a partir do catálogo de permissões.
- Ruim, porque na listagem a decisão seria cerimônia: o filtro da fila já restringe os pedidos a quem pergunta, e conferir a titularidade de verdade exigiria uma decisão por item.

### B — Registro sem decisão

- Bom, porque não toca o ponto de decisão.
- Ruim, porque com `fonteGrant` nulo e `resultado` permitido quebra o invariante documentado na `v1` (a fonte está presente se, e somente se, o acesso foi permitido) e deixa ambíguo de onde veio o acesso.
- Ruim, porque sem `permissao` exige a `v2`: o campo é obrigatório, e o leitor da `v1` quebraria.
- Ruim, porque com campo opcional novo obriga a mexer no tipo central do registro, cujo construtor já passa dos limites da ADR-0156.

### C — Titularidade como fonte só do registro

- Bom, porque registra pelo mesmo caminho, com a mesma recusa e no mesmo lote, e diz no registro que o acesso veio da titularidade.
- Bom, porque não cria permissão satisfeita sem concessão e mantém a `v1` sem campo novo.
- Bom, porque a falha é fechada por construção (a concessão de titularidade não alcança resource) e travada por teste de arquitetura.
- Ruim, porque introduz um registro permitido com permissão fora do catálogo de permissões, que a futura consulta precisa conhecer.

## Mais informações

- Issues: #2389 (esta decisão), #2406 (leitura das partes com a consulta dos pedidos, registrada pela permissão), #2364 (registro por item nas listagens governadas por permissão), #2373 (épica).
- UNI-REQ-0203 (registro antes da entrega; a emendar no portal de desenvolvedores para cobrir a leitura das partes do pedido de concessão), UNI-REQ-0372 (leituras do Encarregado de dados registradas), UNI-REQ-0184 (governança das concessões), UNI-REQ-0209 (registro do acesso permitido a partir de dado pessoal) e UNI-REQ-0379 (consulta, retenção e controle de acesso dos registros, incremento planejado).
- **Emenda a [ADR-0153](0153-projecao-por-protecao.md):** o escopo deixa de excluir a leitura feita pelas partes de um pedido de concessão, que é registrada com a fonte `titularidade`.
- **Emenda a [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md):** o vocabulário da fonte registrada ganha `titularidade`, que existe só no registro e não é fonte de concessão efetiva.
- **[ADR-0162](0162-separacao-das-atribuicoes-de-governanca.md):** quando esta ADR for aceita, a regra "Leituras do Encarregado" deixa de remeter a fila dos próprios pedidos à issue #2389 e passa a dizer que a parte sem a consulta dos pedidos tem a leitura registrada por titularidade, como decidido aqui.
- Contrato: `contracts/kafka/registro-de-acesso/v1.schema.json` e `contracts/README.md`.
