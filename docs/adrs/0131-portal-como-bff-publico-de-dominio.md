---
status: "accepted"
date: "2026-09-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Backend (CTIC)"
  - "Frontend (CTIC)"
  - "Infraestrutura (CTIC)"
informed:
  - "Equipe Uni+"
---

# ADR-0131: O Portal do candidato consome um contrato público de Seleção, não o envelope congelado

## Contexto e enunciado do problema

O `portal-api` existe como deployable autônomo desde a criação do seu esqueleto: tem `Program.cs`, `Dockerfile`, chart Helm e banco próprios. O que ele nunca teve foi domínio — até hoje expõe um endpoint de teste e os endpoints compartilhados de sessão e perfil. A [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) o manteve fora do monolito e com banco próprio, mas **não disse o que ele faz**. O checkpoint do spike que originou aquela topologia deixou a lacuna registrada por escrito: *"Portal: recomendado separado (BFF público; sem acesso in-process aos schemas internos). (Confirmar no ADR.)"* — e a confirmação nunca foi escrita.

A pergunta deixa de ser teórica agora que o portal ganha conteúdo. A primeira entrega é uma vitrine de processos seletivos publicados e a página de um processo, com as suas publicações e o edital em PDF.

Um processo publicado congela a sua configuração numa `VersaoConfiguracao` cujo conteúdo é um envelope canônico de vinte e oito blocos, que cresce a cada incremento do domínio. Esse envelope carrega o critério de classificação, os critérios de desempate, o bônus regional, a árvore de satisfação documental, o grafo de dependência entre fatos e o resultado do motor de conformidade — tudo o que o cidadão **não** precisa ler para se inscrever, e boa parte do que a instituição não tem interesse em publicar num endereço anônimo.

A questão a decidir é **de onde o portal tira o que mostra**. O caminho de menor esforço seria abrir a leitura administrativa do processo a uma identidade de serviço e deixar o Portal montar a página a partir do envelope. É esse caminho que esta ADR recusa, e o que ela põe no lugar define, por consequência, o que o Portal é.

## Drivers da decisão

- **Não publicar o modelo interno.** O que atravessa a fronteira pública tem de ser um contrato desenhado para ser público, não uma projeção acidental do que o domínio guarda.
- **Um significado, um lugar.** O envelope canônico não pode ser interpretado por dois codebases que nada obriga a evoluir juntos.
- **Fronteiras de módulo preservadas** — o Portal não acessa schema interno, nem in-process, nem por rede.
- **Sem privilégio administrativo para ler o que é público.** A leitura administrativa do processo em rascunho é restrita por decisão anterior, e ampliá-la para um consumidor público seria revogá-la de lado.
- **Simplicidade operacional** na infraestrutura on-premises da Unifesspa, com equipe pequena e sem SRE dedicado.
- **Preparar a inscrição sem construí-la agora.** A frente seguinte traz estado do candidato e superfície autenticada.

## Opções consideradas

- **A — O Portal mantém projeção própria, alimentada por evento.** Consome o evento de publicação, guarda no seu banco e hidrata consultando a leitura administrativa do processo com credencial de serviço.
- **B — Seleção publica um contrato de leitura próprio, projetado em tempo de consulta; o Portal compõe.**
- **C — Seleção materializa a projeção pública no instante da publicação**, na mesma transação que congela a configuração; a leitura pública vira uma consulta direta à projeção.

## Resultado da decisão

**Escolhida: "B — Seleção publica um contrato de leitura próprio; o Portal compõe"**, porque é a única das três que mantém o significado do envelope num único codebase sem dar privilégio administrativo a um consumidor público.

### O módulo Seleção passa a ser um Open Host Service

Seleção publica um contrato de leitura estável para o certame publicado, com uma camada de tradução que mapeia a configuração congelada para uma linguagem de integração. **Tipo interno não aparece no wire.** O contrato é composto por tipos declarados campo a campo; não é um recorte de subárvore do envelope, nem um filtro sobre documento JSON.

Essa forma não é preferência de estilo. Um contrato montado por filtro sobre o documento congelado devolve o campo novo por omissão: basta o envelope ganhar um bloco para ele atravessar a fronteira sem que ninguém decida. Com tipos declarados, a fronteira é a assinatura do tipo, e o campo novo só passa quando alguém o escreve.

É o mesmo princípio que a [ADR-0081](0081-lgpd-by-design-dto-por-permissao.md) já fixou para respostas que carregam dado pessoal — *"o que não é projetado não pode vazar: o controle vive no formato da resposta, não num passo opcional de saída"*. Aqui ele governa configuração de certame em vez de dado de pessoa, mas a mecânica de proteção é a mesma, e convém que seja: um único modo de proteger a fronteira, aplicado em todo lugar.

A leitura é anônima e **pode ser alcançada diretamente** — é um Open Host Service, e o seu propósito é servir consumidores, não apenas um. Isso alinha o certame publicado com o que o sistema já pratica: o registro de atos é anônimo por decisão da [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md), os catálogos de configuração são anônimos, e a renderização do formulário de inscrição já é pública. A proteção vem do contrato — tipos declarados e ausência de oráculo de existência —, não de obscuridade de rede.

Quanto ao controle de taxa: a [ADR-0093](0093-rate-limiting-na-borda-para-reference-data-publico.md) decidiu tratá-lo na borda, mas o seu alcance declarado são os catálogos de referência. **Esta ADR estende esse alcance** ao contrato público do certame e ao acervo de documentos publicados — que são, juntos, a primeira superfície anônima de alto valor do sistema, com pico previsível na abertura de inscrições.

A escrita permanece administrativa, declarada na própria action, como já ocorre no contrato de renderização do formulário.

### O Portal é um Backend for Frontend, e só isso

Ele compõe o contrato de leitura de Seleção com a consulta unificada de atos de Publicações, aplica política de cache, adapta a resposta à tela e absorve indisponibilidade das origens. O documento do edital **não** passa por ele: o contrato público carrega o endereço do acervo, e o navegador o busca direto (ADR-0132). Ele **não** guarda regra de negócio, **não** escreve em banco de domínio e **não** decide o que pertence ao domínio — a deriva de fronteira é o modo conhecido de morte desse padrão.

O Portal existe, portanto, pela experiência que serve e pelo que vem depois dela — estado do candidato e superfície autenticada na frente de inscrição —, **não por desempenho**: nesta entrega a agregação são duas origens, e cache de conteúdo anônimo pertence à borda. É um **Portal por experiência**: o `portal-api` serve o portal do candidato, e não as aplicações administrativas; um backend que serve todas as telas volta a ser o backend de propósito geral que este padrão existe para evitar.

Decorrem daí quatro consequências que precisam estar decididas, e não descobertas na implementação:

- **O contrato público tem versão própria, e ela entra na chave de cache e no identificador de conteúdo.** O hash da configuração congelada identifica o envelope, não a projeção: acrescentar um campo ao contrato público não muda aquele hash, e um cache endereçado só por ele continuaria servindo a resposta antiga depois do deploy. Chave e identificador de conteúdo compõem **versão da projeção com hash da configuração**.

  **Mas o selo sozinho não cumpre a garantia de que uma retificação não fica encoberta.** Ele é conhecido pela origem, e o endereço da página não muda quando o certame é retificado — de modo que uma representação já guardada na borda ou no navegador seria servida sem que ninguém consultasse a origem. Por isso a resposta pública do detalhe é emitida com **revalidação obrigatória**: o cliente pode guardar, mas precisa confirmar antes de usar, e é na confirmação que o selo faz o seu trabalho. Validade sem revalidação, nesta rota, contradiz a decisão.
- **O cursor de paginação não trafega entre fronteiras, e a chave que o cifra não é compartilhada.** O cursor é opaco, cifrado e carrega o recurso a que pertence justamente para impedir reuso entre coleções ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)). Compartilhar a chave com o Portal lhe daria a capacidade de cunhar cursor para qualquer coleção de Seleção. O Portal emite cursor próprio e guarda a correspondência com o de origem no seu cache.
- **A indisponibilidade das origens tem respostas distintas.** Ausência do registro do ato é transitória por construção e **degrada**: a linha do tempo vem vazia com aviso neutro, e essa resposta não é cacheada — como também não o é a resposta em que o documento do edital ainda está sendo copiado para o acervo ([ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md)). A regra vale para todo estado transitório: o selo do cache compõe versão da projeção e hash da configuração, e nenhum dos dois muda quando a pendência se resolve. Ausência do contrato de Seleção **não** degrada: o Portal não serve conteúdo vencido, porque a configuração publicada tem efeito jurídico e uma retificação não pode ser encoberta por cache; responde indisponibilidade temporária, sinalizando quando reencaminhar.
- **A prontidão do Portal não pode depender do que a vitrine não usa.** O processo agrega hoje, sob a mesma sonda, verificações de banco, cache, object storage, mensageria e provedor de identidade. Uma vitrine anônima que não usa nenhum dos três últimos sairia do ar com qualquer um deles. A sonda de prontidão do Portal passa a cobrir apenas as dependências do que ele serve.

### O que torna um processo visível

Um processo aparece no portal quando tem versão de configuração vigente, e isso **é** equivalente a ter ato publicado: a versão vigente carrega, congelados, o identificador e o hash do ato que a criou, referenciados por valor conforme [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md). O portal não inventa um segundo critério de publicidade paralelo ao da ADR-0105.

A consequência prática é boa: a vitrine e os dados do edital não dependem da drenagem do outbox. Só a linha do tempo de atos depende, e é ela que degrada.

## Consequências

### Positivas

- O envelope canônico permanece conhecido em um só lugar. Um bloco novo no domínio não vaza para o público por omissão.
- O Portal nunca recebe privilégio administrativo, e a leitura administrativa do processo em rascunho continua restrita, como a decisão original previa.
- O contrato público serve a qualquer consumidor futuro — o sítio institucional, um painel de transparência, um agregador externo — sem integração sob medida para cada um.
- A frente de inscrição encontra a fronteira pronta: quando houver estado de candidato, não será preciso reabrir a topologia.

### Negativas

- **Troca-se disponibilidade por simplicidade.** Com projeção própria no Portal, a vitrine responderia com o monolito indisponível. Compondo sob demanda, não responde. Aceita-se pelo volume atual e pela redução de peças móveis, mas é o principal candidato a reversão.
- A listagem pública paga o custo de projetar a configuração congelada a cada página consultada.
- Ordenar a vitrine por prazo exige alcançar dado que vive dentro do documento congelado, e não em coluna — o que torna a paginação por chave a parte mais cara da primeira entrega.
- **A superfície pública do sistema cresce em dois lugares, não em um.** As rotas novas de Seleção somam-se às superfícies anônimas que já existem, e o acervo de documentos públicos passa a ser servido pela borda — decidido em [ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md). O controle de taxa da [ADR-0093](0093-rate-limiting-na-borda-para-reference-data-publico.md) deixa de ser trabalho de acabamento.
- Nesta entrega o Portal acrescenta pouco além de composição e cache. A justificativa é a experiência e o que vem depois, não ganho de desempenho.

### Neutras

- A opção **C** permanece disponível e é a evolução natural se a paginação sobre o documento congelado se mostrar cara. Eliminaria o custo de projeção por consulta e tornaria impossível, por construção, um bloco novo escapar para o público. Não foi escolhida agora por exigir migration e uma segunda escrita no caminho de publicação.
- **O caminho de reversão pré-autorizado não é a opção A.** Se o critério mudar de simplicidade para disponibilidade, o movimento compatível com esta decisão é o Portal **persistir o contrato público que já consome** — a linguagem publicada, não o envelope — mantendo-o atualizado e servindo-o quando a origem estiver fora. Isso não decodifica envelope nem pede privilégio administrativo, e por isso não revoga esta ADR. A opção A, que hidrata a partir da leitura administrativa, permanece vedada enquanto esta decisão vigorar.

## Prós e contras das opções

### A — Projeção própria no Portal, alimentada por evento

- **Bom**: a vitrine sobrevive à indisponibilidade do monolito; leitura sem custo de projeção.
- **Ruim**: o Portal passa a interpretar o envelope canônico, duplicando o significado de vinte e oito blocos sem acoplamento de compilação; exige credencial de serviço sobre uma leitura administrativa deliberadamente restrita; acrescenta consumidor, fila morta, atraso de propagação, carga inicial dos processos publicados antes do consumidor existir e defasagem na retificação.
- **Decisivo**: mesmo assim a hidratação continuaria sendo chamada síncrona ao monolito. Paga-se o custo da projeção e mantém-se o acoplamento.

### B — Contrato público projetado em tempo de consulta (escolhida)

- **Bom**: um único lugar conhece o envelope; nenhum privilégio administrativo atravessa a fronteira; o contrato serve outros consumidores; nenhuma peça de infraestrutura nova.
- **Ruim**: projeta a cada consulta; a vitrine cai com o monolito; a ordenação por prazo exige alcançar dado dentro do documento congelado.

### C — Projeção materializada na publicação

- **Bom**: leitura barata; ordenação e paginação triviais; a lista do que é público passa a ser escrita por código no momento da publicação, o que torna impossível um bloco novo escapar por omissão.
- **Ruim**: migration e segunda escrita no caminho de publicação, que é transacional e já é o ponto mais sensível do módulo; recarga necessária ao evoluir o contrato.
- **Por que não agora**: o custo não se paga no volume atual. Torna-se a escolha certa assim que a paginação sobre o documento congelado mostrar o seu preço.

## Confirmação

- **Fronteira de tipo:** um teste compara as chaves do envelope canônico contra a lista conhecida e **falha quando um bloco novo aparece**, obrigando a decisão explícita "público ou interno" a cada incremento do domínio.
- **Fronteira de módulo:** nenhum projeto do Portal referencia projeto de Seleção. Essa checagem não enxerga acoplamento por rede, e por isso vale o que está no parágrafo seguinte.
- **Contrato próprio, não emprestado:** o documento de contrato do Portal é gerado a partir dos seus próprios tipos. Onde um tipo do Portal tiver o mesmo nome de um de outro módulo, a verificação de contrato que já existe exige que as duas formas sejam idênticas — ela não proíbe o nome repetido, obriga a coincidência. Divergir de forma sob o mesmo nome quebra o gate; ter forma própria sob nome próprio é o caminho normal.
- **Ausência de oráculo:** as rotas públicas respondem com o mesmo "não encontrado" para tudo que não seja "há versão vigente", sem distinguir processo inexistente de processo em rascunho.
- **Prontidão honesta:** a sonda de prontidão do Portal não reprova por dependência que a superfície pública não usa.

## Mais informações

Gatilhos de reversão, para que a revisão desta decisão não dependa de impressão:

- **Disponibilidade** — indisponibilidade do monolito derrubando a vitrine em janela de inscrição aberta.
- **Latência** — tempo de resposta da vitrine acima do orçamento no pico de abertura, com o controle de borda já em operação.

Ambos exigem medição: taxa de acerto do cache, latência por origem consultada e frequência de degradação da linha do tempo.

ADRs relacionadas:

- [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) — **refinada**: o Portal continua deployable autônomo com banco próprio; esta ADR diz o que ele faz.
- [ADR-0064](0064-convencao-roteamento-path-based-com-prefixo-modulo.md) — **refinada**: mantém-se o roteamento por prefixo de módulo. A opção rejeitada lá era um gateway agregador de infraestrutura, multiplexando todos os backends para todos os clientes; o que esta ADR adota é um backend de domínio com contrato próprio, servindo uma experiência, sem processo novo. A tabela de contexto daquela ADR está defasada — descreve cinco APIs separadas e um módulo que não existe mais.
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — fonte da verdade sobre publicidade do ato.
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — referência cross-módulo por valor.
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — o cursor opaco cujo alcance esta ADR restringe.
- [ADR-0093](0093-rate-limiting-na-borda-para-reference-data-publico.md) — controle de taxa na borda, agora com consumidor concreto e prazo.
- [ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md) — o armazenamento público de onde o edital é servido; é ela que dispensa o contrato público de emitir credencial ou redirecionar.
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) e [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — formato de erro e versionamento que o contrato público segue.

Origem dos padrões adotados:

- *Open Host Service* e *Published Language* — Evans, **Domain-Driven Design**, contexto estratégico; síntese em <https://quality.arc42.org/approaches/open-host-service>.
- *Backends for Frontends* — Newman, <https://samnewman.io/patterns/architectural/bff/>, com a ressalva de que interface web pura só justifica o padrão havendo agregação relevante no servidor.
- Deriva de fronteira como modo de falha do padrão — <https://akfpartners.com/growth-blog/backend-for-frontend>.
- Exposição excessiva no nível da propriedade — OWASP API Security Top 10 2023, <https://owasp.org/API-Security/editions/2023/en/0xa3-broken-object-property-level-authorization/>, que é a categoria exata do risco de servir o envelope canônico a chamador anônimo.
- Conteúdo público e anônimo pertencendo à borda — <https://zuplo.com/learning-center/api-gateway-caching>.

Esta ADR fecha a pendência registrada em `docs/spikes/monolito-modular-checkpoint.md`, cuja linha *"(Confirmar no ADR.)"* deve ser atualizada no mesmo trabalho.
