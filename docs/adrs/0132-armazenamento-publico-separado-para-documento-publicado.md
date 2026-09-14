---
status: "accepted"
date: "2026-09-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Backend (CTIC)"
  - "Infraestrutura (CTIC)"
  - "Encarregado de dados (DPO)"
informed:
  - "Equipe Uni+"
---

# ADR-0132: Documento tornado público vive em armazenamento separado, com leitura anônima e endereço imutável

## Contexto e enunciado do problema

O Uni+ guarda documentos num único bucket de object storage, compartilhado por todas as APIs. O acesso a qualquer objeto se dá exclusivamente por URL pré-assinada de curta duração, emitida pela aplicação a cada pedido.

Esse arranjo nasceu de um fluxo administrativo, em que quem lê já está autenticado e autorizado. O portal do candidato introduz um leitor diferente: **o cidadão anônimo**, que precisa baixar o edital de um processo seletivo publicado. E o edital é, por definição, documento tornado público — a [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) registra isso ao decidir que a leitura do ato é anônima.

Servir esse documento por URL pré-assinada tem quatro consequências ruins, e nenhuma delas é teórica:

1. **Faz a superfície de leitura depender da interface S3.** A URL pré-assinada aponta para o endereço que a assinou, e essa interface é a mesma que serve **todos** os objetos — inclusive os documentos do candidato que a frente de inscrição vai depositar ali, com quarentena e dados pessoais.

   O desenho de envio de arquivo já pressupõe essa interface alcançável pelo cliente: os bytes vão direto do navegador para o armazenamento, por URL pré-assinada de escrita emitida pela aplicação. Hoje ela está atrás de endereço privado; o destino é ser alcançável por rede privada virtual em homologação e ter endereço público em produção.

   O que esta ADR recusa é que essa porta, **quando existir**, também passe a servir leitura anônima — em vez de a leitura pública ter caminho próprio desde o começo.
2. **Impede cache.** A assinatura entra na URL e muda a cada emissão, de modo que borda, proxy e navegador tratam cada pedido como um recurso distinto. Conteúdo público e imutável, que é o caso canônico de cache de borda, passa a ser o único que não cacheia.
3. **Cria amplificação.** Cada visita mina uma credencial nova, válida por minutos e repassável a terceiros, num endereço sem autenticação a montante.
4. **Protege o que não precisa de proteção**, e ao fazê-lo dá a impressão de que o documento é restrito, o que ele não é.

Há ainda um problema de disposição dos objetos que o arranjo atual esconde. As chaves do documento do edital são derivadas do identificador do processo e do documento, e o objeto **confirmado** convive, sob o mesmo prefixo, com o objeto ainda **pendente de confirmação**. Confirmar o upload é pré-requisito de publicar, não o ato de publicar: entre um e outro existe um edital completo, conferido e ainda não oficial. Qualquer regra de acesso aplicada a esse prefixo alcançaria os dois.

A questão a decidir é **onde vive o documento que se tornou público, e como ele é lido**.

## Drivers da decisão

- **Separar o que é público do que é pessoal** — a frente de inscrição trará documento de candidato para o mesmo armazenamento, sob proteção da LGPD.
- **Não acrescentar a leitura pública à superfície que serve todos os objetos.** O envio de arquivo vai direto do cliente para o armazenamento e, quando essa porta for aberta, ela servirá todos os objetos; a leitura pública não pode entrar por ali.
- **Tornar o documento público cacheável** na borda, que é onde conteúdo anônimo pertence.
- **Não publicar antes da publicação.** Nenhuma regra de acesso pode alcançar documento cujo ato ainda não existe.
- **Preservar a imutabilidade do que foi publicado** — o ato é prova, e o passado documental não se muta.

## Opções consideradas

- **A — Manter URL pré-assinada**, publicando a interface S3 e emitindo credencial a cada leitura.
- **B — Abrir leitura anônima por prefixo** dentro do bucket compartilhado.
- **C — Bucket dedicado a documento público**, com leitura anônima de objeto e o documento entrando nele no ato da publicação.

## Resultado da decisão

**Escolhida: "C — bucket dedicado a documento público"**, porque é a única que separa fisicamente o documento público do documento pessoal e a única que torna o endereço estável o bastante para ser cacheado.

### Dois armazenamentos, com regras opostas

O armazenamento **privado permanece, e permanece como está**: nenhum acesso anônimo, leitura só por URL pré-assinada emitida pela aplicação. É onde continuam os documentos em elaboração e, sobretudo, os documentos do candidato que a frente de inscrição trará — comprovantes de renda, laudos, autodeclarações. Esse acervo é de dado pessoal, parte dele sensível, e a instituição responde por ele perante a LGPD.

Nada nesta ADR afrouxa aquele regime. Criar um lugar público **não** é abrir o que já existe: é tirar de lá o que nunca precisou estar sob aquela proteção, para que a proteção fique confinada ao que de fato a exige.

O armazenamento **público** é um bucket dedicado, com permissão anônima de **leitura de objeto e apenas isso** — sem listagem, sem escrita, sem remoção. Nada é gravado nele senão pelo caminho descrito abaixo.

A opção B foi recusada por um motivo operacional que vale registrar: a separação passaria a depender de acerto de padrão de chave a cada alteração de política, para sempre, num armazenamento que guardará dado pessoal. Um bucket dedicado transforma essa vigilância permanente numa decisão feita uma vez. E há um agravante que nenhum padrão de chave resolve: sob o arranjo atual, o objeto **confirmado** e o **pendente** convivem, e confirmar não é publicar.

Vale registrar que a separação não é invenção desta ADR. A [ADR-0009](0009-minio-como-object-storage.md) já prescrevia, como prática obrigatória, *"buckets separados por módulo e tipo de documento com políticas de retenção configuráveis"*. O bucket único que existe hoje é **desvio** dessa decisão, não linha de base — esta ADR retoma o que já estava prescrito e diz qual é o primeiro corte.

O princípio não é novo no projeto. A [ADR-0081](0081-lgpd-by-design-dto-por-permissao.md) já o adotou para respostas de API — *"o que não é projetado não pode vazar: o controle vive no formato da resposta, não num passo opcional de saída"*. Aqui ele vale para objetos: o que não está no acervo público não pode ser lido anonimamente, e a separação é física, não uma regra que alguém precisa lembrar de aplicar.

### O único caminho de escrita no acervo público é a publicação de um ato

Nada além de documento de ato normativo publicado entra no armazenamento público, e nada entra por outra porta que não seja esse movimento. Documento de candidato **não tem caminho** até lá — não por proibição escrita, mas porque nenhum código o leva.

Essa cláusula existe porque **publicar é irreversível na prática**: um endereço divulgado pode ter sido acessado, copiado e citado, e não há como desfazer isso com garantia. Logo o controle tem de estar na entrada, e ser verificável por teste, não por revisão de código.

### O objeto público nasce na publicação, não na confirmação

A cópia para o armazenamento público acontece **quando o processo é publicado**, e não quando o upload é confirmado. Essa é a cláusula que impede o documento de ficar acessível antes do ato que o oficializa; o movimento é descrito logo abaixo.

O movimento é assíncrono e durável, pelo mesmo mecanismo com que o registro do ato normativo já é feito depois do commit da publicação ([ADR-0108](0108-registro-do-ato-por-mensagem-duravel.md)): a publicação grava a sua própria transação e emite a mensagem; a cópia ocorre depois, com retentativa e fila morta. Copiar dentro da transação exigiria desfazer efeito externo em caso de falha, o que o armazenamento não oferece.

**A cópia e o registro do ato não são trabalhos independentes.** Duas mensagens pós-commit não têm ordem garantida — o próprio tratamento do registro em Publicações documenta que requisições sucessivas chegam fora de ordem e resolve isso insistindo. Se a cópia concluísse antes do registro, ou se o registro fosse parar na fila morta, o contrato passaria a divulgar o documento de um ato que ainda não existe, contrariando a cláusula acima. **O endereço só é divulgado quando os dois estados estão confirmados** — seja encadeando a cópia ao registro bem-sucedido, seja conferindo ambos antes de expor o campo.

A consequência é uma janela — curta, medida em segundos — em que o processo está publicado e o documento público ainda não está disponível. Ela é **a mesma janela** que já existe para o registro do ato, e recebe o mesmo tratamento: o contrato público só oferece o endereço do documento depois que a cópia concluiu, e a ausência é exibida como pendência de processamento, nunca como erro nem como "edital inexistente".

**Essa resposta não é guardada em cache, em nenhuma camada.** A distinção importa e é fácil de perder: a chave de cache do contrato público compõe a versão da projeção com o hash da configuração ([ADR-0131](0131-portal-como-bff-publico-de-dominio.md)), e **nenhum dos dois muda quando a cópia conclui**. Guardar a resposta pendente faria o portal continuar anunciando "documento em processamento" depois de o documento já estar disponível, até a validade longa expirar. A regra é a mesma que já vale para a linha do tempo ainda não registrada, e pelo mesmo motivo: estado transitório não entra em cache de conteúdo endereçado por conteúdo que não o representa.

### O endereço é derivado do conteúdo

A chave do objeto público é composta por identidade da publicação e **hash do documento** — na forma `editais/<ano>/<identificador da unidade>/<tipo do ato>/<identificador do processo>/<identificador do ato>/<hash>.pdf`. Todos os segmentos vêm de dado já congelado.

Incluir a identidade da publicação, e não apenas o hash, é o que **preserva a procedência**: dois certames que publiquem exatamente o mesmo arquivo produzem objetos distintos, cada um com os seus metadados de origem. Fosse a chave apenas o hash, a segunda publicação sobrescreveria a procedência da primeira, e a reconciliação do acervo sem consultar o banco deixaria de valer.

**O identificador do ato é o último desempate, e ele é necessário.** Os segmentos que descrevem o certame — ano, unidade, tipo e processo — não bastam, porque uma **retificação do mesmo processo pode reutilizar um documento já confirmado**, inclusive o documento do ato anterior: a conferência feita na retificação é de pertencimento e de estado, não de uso prévio. Nesse caso, todos os demais segmentos e o próprio hash coincidem, e nasce um ato distinto. Sem a identidade do ato na chave, a segunda cópia colidiria com a primeira e teria de sobrescrever a procedência ou omitir o ato novo — precisamente o que a separação por identidade existe para impedir.

Disso decorrem três propriedades:

- **Imutabilidade real.** O mesmo endereço nunca serve conteúdo diferente, o que autoriza cache perpétuo na borda e no navegador.
- **Retificação sem sobrescrita.** Um edital retificado ganha endereço próprio, e o anterior permanece acessível — como convém a um acervo em que o ato retificado continua existindo. Quando o conteúdo muda, é o hash que separa os dois; quando a retificação **reutiliza o documento do ato anterior**, o que separa é o identificador do ato. Vale registrar a distinção para que a implementação e os testes não tratem o hash como desempate suficiente da retificação: nesse caso ele é idêntico.
- **Ausência de dado pessoal.** Nenhum segmento da chave identifica uma pessoa, e nenhum carrega identificador natural — o que mantém o endereço em conformidade com a proibição de dado pessoal em caminho de URL ([ADR-0019](0019-proibir-pii-em-path-segments-de-url.md)). Os segmentos que identificam o certame, a unidade, o ato e o processo são deliberadamente parte do caminho, e são o que dá legibilidade e procedência ao acervo.

O endereço completo entra no contrato público de leitura do certame ([ADR-0131](0131-portal-como-bff-publico-de-dominio.md)) como um campo. **Não há endpoint de redirecionamento**, nem emissão de credencial, nem chamada de aplicação no caminho do download.

### O objeto carrega os próprios cabeçalhos

Como não há aplicação no caminho da leitura, **tudo que a resposta precisa dizer tem de estar gravado no objeto**: o tipo do conteúdo, a política de cache e o nome com que o arquivo se apresenta a quem o baixa. Sem esse último, um endereço derivado de hash entrega ao cidadão um arquivo cujo nome é o próprio hash.

Isso é possível porque o objeto público **é escrito pela aplicação**, na cópia da publicação — e não pelo cliente. A distinção importa: o envio original vem do navegador por URL pré-assinada, e o que esse envio consegue carimbar no objeto não é confiável (a biblioteca de acesso ao armazenamento, em todas as versões publicadas, não propaga os cabeçalhos declarados na assinatura de escrita). O acervo público não herda essa limitação, porque quem escreve nele é o servidor.

Junto dos cabeçalhos de apresentação, o objeto público carrega metadados de procedência — o ato que o publicou, o processo a que se refere e o hash do conteúdo —, que tornam o acervo reconciliável sem consultar o banco.

### O armazenamento público é servido pela borda

O acervo público é publicado por **nome próprio** no encaminhador de borda, com política de cache e de taxa — um caminho que existe só para ele. **É esse nome que o contrato público divulga**, e é por ele que o cidadão chega.

A permissão anônima de leitura vale para o objeto, mas **o acervo não é servido pela porta de dados do armazenamento**. Sem essa restrição, o desenho se anula sozinho: com a porta de dados pública em produção e a chave do objeto visível no endereço divulgado, qualquer um poderia buscar o arquivo direto, contornando o cache e o controle de taxa que esta decisão atribui à borda — e o pico de abertura de inscrições bateria no armazenamento, que é justamente o que se quis evitar. A leitura do acervo chega pela borda; a porta de dados continua servindo o que sempre serviu, incluindo o envio direto por endereço assinado.

A porta de dados do armazenamento segue caminho próprio e já tem destino definido: em homologação, alcançável por rede privada virtual; em produção, com endereço público. **É por isso que a separação decidida aqui importa mais, e não menos.** Quando essa porta for pública, o que protege o documento do candidato não terá nenhum componente de rede — será o bucket privado exigir assinatura em toda leitura. Um bucket que serve leitura anônima não pode ser o mesmo que guarda dado pessoal, e a distância entre as duas coisas precisa ser física.

## Consequências

### Positivas

- Documento pessoal e documento público deixam de coabitar. A permissão que libera um não alcança o outro.
- A leitura pública ganha caminho próprio, com política de cache e de taxa, em vez de ser acrescentada à superfície que serve todos os objetos.
- O edital passa a ser cacheável perpetuamente, inclusive por proxy e navegador — e o portal para de gerar credencial por visita.
- O caminho do download deixa de passar pela aplicação: nenhuma requisição de cidadão baixando edital consome processo de API.
- O acervo ganha endereços estáveis e citáveis, que podem ser referenciados por terceiros sem intermediação.

### Negativas

- O documento existe em duas cópias, uma privada e uma pública. É custo de armazenamento em troca de separação de domínio de acesso, e o documento em questão é pequeno.
- Surge uma janela entre publicar e o documento estar disponível publicamente. Curta, com o mesmo tratamento da janela já existente do registro do ato, mas real.
- Um objeto público, uma vez copiado, é de remoção delicada: alguém pode tê-lo citado. Remoção passa a ser ato deliberado, não efeito colateral de operação administrativa.
- Exige duas capacidades que a abstração de armazenamento não tem. Ela oferece hoje envio, leitura, leitura limitada, remoção, leitura de metadados e emissão de URL temporária — de leitura e **de escrita**, esta última justamente a que sustenta o envio direto do cliente. Faltam a **cópia entre buckets no servidor** (copiar por leitura e reenvio carregaria o documento inteiro para a memória do processo, e é recusado) e a **gravação de metadados e cabeçalhos** no objeto de destino.

### Neutras

- Não há dado a migrar: o sistema não está em produção, e os objetos existentes são de ensaio.
- Documento público nunca é removido por expiração. O ciclo de vida do armazenamento público é de acervo, não de cache.
- **Os dois armazenamentos têm ciclos de vida opostos, e a regra de um não se aplica ao outro.** O público é permanente, porque o ato é prova e o passado documental não se muta. O privado tem prazo: guarda dado pessoal, está sujeito a retenção por finalidade e precisa comportar eliminação a pedido do titular. Aplicar ao privado a irreversibilidade decidida aqui criaria um problema de conformidade, não resolveria um.

## Prós e contras das opções

### A — Manter URL pré-assinada

- **Bom**: nada a construir; um único armazenamento a operar.
- **Ruim**: obriga a publicar a interface que serve todos os objetos, inclusive os pessoais; impede cache; cria amplificação de credencial; põe a aplicação no caminho de cada download.

### B — Leitura anônima por prefixo no bucket compartilhado

- **Bom**: sem bucket novo; sem cópia.
- **Ruim**: a separação passa a depender de acerto de prefixo a cada alteração de política, num bucket que guardará dado pessoal; e o prefixo atual alcançaria o documento ainda não publicado, porque objeto pendente e objeto confirmado coabitam.

### C — Bucket dedicado, objeto entrando na publicação (escolhida)

- **Bom**: separação física; leitura pública com caminho próprio, cache e taxa; endereço imutável; nenhuma credencial emitida por visita; cabeçalhos de apresentação gravados por quem escreve o objeto — o servidor.
- **Ruim**: duplica o objeto; exige cópia entre buckets na abstração de armazenamento; introduz janela entre publicar e disponibilizar.

## Confirmação

- **Separação verificável:** o bucket público não contém objeto algum sob prefixo de documento de candidato, e a permissão anônima cobre leitura de objeto e nada além.
- **Caminho único de escrita:** o único trecho de código que grava no acervo público é o da publicação do ato; nenhum outro ponto do sistema tem o bucket público como destino de escrita.
- **Nada antes da publicação:** um processo com documento confirmado e ainda não publicado não tem objeto no armazenamento público.
- **Endereço imutável:** o mesmo endereço serve sempre o mesmo conteúdo; um edital retificado produz endereço distinto e não invalida o anterior.
- **Sem leitura anônima fora do acervo:** a interface que serve o armazenamento privado não devolve objeto a chamador anônimo, e o acervo público não permite listagem.
- **Leitura do acervo só pela borda:** o mesmo objeto é legível pelo nome que a borda publica e **recusado** quando buscado anonimamente pela porta de dados do armazenamento. Os dois ensaios andam em par: sem o segundo, uma instalação satisfaz todos os demais e ainda aceita a leitura direta, contornando o cache e o controle de taxa que esta decisão atribui à borda.
- **Pendência não se eterniza:** uma cópia que esgote as retentativas é sinalizada e reprocessável; um ato vigente não fica indefinidamente sem documento acessível.

## Mais informações

- [ADR-0009](0009-minio-como-object-storage.md) — escolheu o object storage sem tratar de domínio de acesso; esta ADR o particiona.
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — o ato publicado é documento tornado público; é dela que decorre o motivo de existir armazenamento público.
- [ADR-0131](0131-portal-como-bff-publico-de-dominio.md) — o contrato público que passa a carregar o endereço do documento, dispensando endpoint de redirecionamento.
- [ADR-0108](0108-registro-do-ato-por-mensagem-duravel.md) — o mecanismo durável pós-commit que a cópia reaproveita, e a janela que ela herda.
- [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) — a chave derivada de conteúdo satisfaz a proibição sem exceção.
- [ADR-0081](0081-lgpd-by-design-dto-por-permissao.md) — o princípio de que o controle vive no formato, e não num filtro de saída; esta ADR o aplica a objetos em vez de respostas.
- [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md) — cifragem de dado sensível em repouso decidida para persistência relacional; o tratamento equivalente para documento de candidato no armazenamento privado é frente própria, e esta ADR não a antecipa nem a dispensa.
- [ADR-0093](0093-rate-limiting-na-borda-para-reference-data-publico.md) — a borda que passa a servir também o acervo público.
