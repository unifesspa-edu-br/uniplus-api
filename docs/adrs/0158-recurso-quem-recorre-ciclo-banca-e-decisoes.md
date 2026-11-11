---
status: "proposed"
date: "2026-11-10"
decision-makers:
  - "Tech Lead"
consulted:
  - "PO"
informed:
  - "Equipe Uni+"
---

# ADR-0158: O recurso abre pela publicação confirmada de um ato, declara quem pode recorrer, segue o ciclo do item e é decidido por banca designada em documento único

## Contexto e enunciado do problema

A [ADR-0145](0145-recurso-por-etapa.md) deu janelas de recurso à fase e à etapa, com prazo, suspensividade e instância superior, e admitiu duas origens para o prazo: a publicação do preliminar e a ciência individual do candidato. Ela não diz quem pode recorrer, como o recurso é identificado, o que acontece com os recursos quando o preliminar é substituído, quem decide, como decide, nem como a decisão chega ao resultado definitivo.

Em 09/11/2026 o líder técnico e o PO definiram o desenho do recurso para qualquer fase ou etapa, e o PO confirmou os requisitos em 10/11/2026 (UNI-REQ-0269 a UNI-REQ-0291, exceto UNI-REQ-0281, UNI-REQ-0282 e UNI-REQ-0283, ainda a confirmar por escrito). Uma das confirmações muda a ADR-0145: não existe decisão comunicada só ao candidato, então todo recurso se abre pela publicação de um ato (UNI-REQ-0269), e a ciência individual deixa de ser aceita na configuração do recurso (UNI-REQ-0080, UNI-REQ-0115).

A [ADR-0157](0157-perfis-de-acesso-versionados-e-aprovados.md) trata do acesso por perfil versionado, não da designação de quem analisa recursos. As ADRs [0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) e [0105](0105-modulo-publicacoes-registro-central-dos-atos.md) tratam a correção de ato publicado como relação entre atos, mas não preveem um novo ciclo do mesmo item. A [ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md) dá à fase a regra de recurso e não diz quem recorre.

## Drivers da decisão

- O edital é a fonte da regra: quem recorre e como a banca delibera variam por edital, e o sistema não fixa nada disso por tipo de fase ou de processo (UNI-REQ-0270, UNI-REQ-0275).
- Ninguém chega ao resultado definitivo sem ter tido a oportunidade de recorrer contra o preliminar que valeu (UNI-REQ-0282).
- O prazo corre de um instante oficial, igual para todos e verificável depois (UNI-REQ-0115).
- Quem analisa recurso vê dado pessoal, inclusive sensível, e por isso é designado formalmente (UNI-REQ-0274).
- A assinatura digital é feita fora do sistema, e o sistema não a verifica (UNI-REQ-0157, UNI-REQ-0278).
- Auditoria: nenhum recurso, voto ou documento é apagado ou sobrescrito ([ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md)).

## Opções consideradas

- **A.** Regras fixas por tipo de fase: o sistema deduz quem recorre e como a banca decide pelo tipo da fase ou do processo, e a retificação corrige o preliminar existente.
- **B.** Tudo declarado no edital e congelado com o processo: a janela declara quem recorre; a banca e a regra de deliberação são configuração do edital; a retificação depois do preliminar abre um novo ciclo do item; as decisões saem num documento único e o definitivo é composto do preliminar com as decisões.
- **C.** Como B, mas com a deliberação, a assinatura e o documento fora do sistema, que só recebe o resultado final de cada recurso.

## Resultado da decisão

**Escolhida:** "B", porque é a única que exprime cada edital como ele é escrito, mantém a trilha do voto até o definitivo e preserva o direito de recorrer quando o preliminar muda.

**Abertura do recurso pela publicação confirmada (UNI-REQ-0269, UNI-REQ-0080, UNI-REQ-0115).** Todo recurso se abre pela publicação de um ato da fase ou da etapa. A abertura é a janela declarada no edital mais o ato âncora confirmado. Ato âncora confirmado é o ato do processo cuja publicação foi confirmada no sistema pelo registro da publicação em Publicações; ato pendente, ou só registrado sem a publicação confirmada, não abre janela. O prazo conta do instante de publicação declarado pelo ato, nunca do instante em que o registro foi gravado. Se a confirmação chega no início da contagem ou depois dele, a publicação não aceita recursos: o ato é publicado de novo, com nova data e o prazo inteiro (PO, 10/11/2026). Esse "confirmado" não é o da [ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md), em que confirmar o envio do arquivo é pré-requisito de publicar e não a publicação, nem o da [ADR-0133](0133-divulgacao-do-certame-materializada-no-registro-do-ato.md), que trata da abertura do certame com ato normativo para fins de divulgação; aqui é a publicação do ato âncora, já registrada. A ciência individual sai do recurso: a configuração do recurso não a aceita, e a capacidade fica sem uso enquanto não houver decisão comunicada só ao candidato. Na etapa com nota do ENEM deixa de existir recurso individual contado da ciência; o recurso cabível é o da fase, contra a publicação dela (UNI-REQ-0080).

**Quem pode recorrer, por janela (UNI-REQ-0270).** Cada janela declara uma de três opções, congelada com o processo: todos os que participaram do ato (o gabarito preliminar, por exemplo); cada candidato contra o próprio resultado (notas e classificação); ou só quem teve resultado desfavorável (homologação das inscrições, análise de banca). A publicação do edital é recusada se uma janela não declarar a opção. Participar do ato e ter resultado nele são coisas distintas: nas três opções só recorre quem participou do ato, e a terceira exige ainda que o resultado dessa pessoa no ato seja desfavorável, pelo vínculo do motivo com o lado favorável ou desfavorável (UNI-REQ-0283). O recurso de quem não se enquadra é recusado com erro nomeado.

**Identidade do recurso (UNI-REQ-0269, UNI-REQ-0272).** O recurso é identificado pela inscrição e pela abertura. Há no máximo um por par; o envio repetido no prazo é nova versão do mesmo recurso, todas guardadas, e vale a última enviada no prazo. Não há desistência. Depois do fim do prazo, nada é aceito nem alterado. A concorrência entre dois envios da mesma inscrição na mesma abertura é resolvida pela unicidade do par e pela versão do recurso: um envio grava, o outro é recusado por conflito e pode ser reenviado. Respostas e anexos ficam no recurso, não na inscrição (UNI-REQ-0271).

**Novo ciclo do item (UNI-REQ-0282, UNI-REQ-0157, UNI-REQ-0160).** Quando uma retificação do edital, depois do preliminar, muda a regra ou as inscrições da fase, o novo preliminar é a publicação original de um novo ciclo do mesmo item do edital: leva o número do ciclo e a referência ao preliminar que substitui. Não é correção do preliminar anterior, e a publicação única por item passa a valer por ciclo. O novo preliminar abre uma nova abertura, portanto um novo prazo e novos recursos. Os recursos do ciclo anterior ficam registrados e sem efeito: não são apagados, não são julgados para o definitivo, e o definitivo espera só os do ciclo vigente. A correção do mesmo preliminar continua sendo relação entre atos, como nas ADRs 0103 e 0105; a correção que não muda o resultado de ninguém não reabre o prazo, e a que muda segue pela retificação, com novo ciclo (UNI-REQ-0160). O mecanismo do ciclo vale para qualquer item do edital. O gatilho, hoje, é só o que o UNI-REQ-0282 cobre, a homologação das inscrições; a generalização dos gatilhos fica para a uniplus-developers#445. O UNI-REQ-0157 ainda diz que a retificação não muda de ciclo a matéria já aberta, e o UNI-REQ-0282 diz o contrário para esse caso; esta ADR segue o UNI-REQ-0282, e o ajuste do texto do UNI-REQ-0157 fica na mesma issue.

**Banca e deliberação configuráveis (UNI-REQ-0273 a UNI-REQ-0277, UNI-REQ-0279).** Todo recurso previsto exige uma banca de análise de recursos prevista na fase ou na etapa; sem ela o edital não é publicado. O edital define, por banca, integrantes, suplentes, quórum, critério (maioria simples, maioria absoluta ou unanimidade), desempate (voto da presidência ou nova votação), encerramento (todos votam, ou a presidência encerra ao atingir o quórum) e o que vale sem unanimidade; o sistema não impõe nenhuma dessas regras. A designação tem exatamente o número previsto, pode ter uma pessoa só e recusa quem tem inscrição enviada e não cancelada no processo. O voto pertence à vaga de integrante, não à pessoa: o suplente só vota quando a substituição está registrada, e então vota pela vaga. Cada integrante vota com a própria conta institucional, e um não vota pelo outro. A análise começa só depois do fim do prazo. A designação é ato próprio do processo, registrado pela unidade responsável pelo processo ou pela fase, e não concessão de perfil: o acesso aos recursos vem dela, enquanto a consulta por outros servidores segue a permissão de cada um (UNI-REQ-0291, [ADR-0157](0157-perfis-de-acesso-versionados-e-aprovados.md)). Pessoa externa à Unifesspa só atua com conta institucional, no prazo do acesso, com designação e termo de confidencialidade anexados ao pedido.

**Documento único das decisões (UNI-REQ-0278, UNI-REQ-0288, UNI-REQ-0290).** As decisões de uma janela saem num documento único, com o número de inscrição e o resultado de cada recurso, sem motivo nem nome, assinado por todos os que votaram. Enquanto não houver integração com a assinatura gov.br, a assinatura é feita fora do sistema; o documento assinado é anexado e conferido. A conferência é humana e registrada, com quem conferiu e quando; o sistema não valida assinatura (UNI-REQ-0157). O documento é arquivado, não publicado, e fica à parte dos documentos do candidato. Havendo recursos, o definitivo é recusado sem o documento anexado e conferido; sem recursos, não há documento.

**Definitivo composto (UNI-REQ-0280, UNI-REQ-0132).** O definitivo é o preliminar do ciclo vigente com as decisões dos recursos aplicadas. No recurso contra o próprio resultado, só muda a situação de quem teve recurso deferido. No recurso contra ato comum a todos, como o gabarito, o deferimento altera o resultado de todos os que o ato alcança, tenham ou não recorrido. O julgamento não altera nenhuma inscrição; a decisão entra só no definitivo.

## Consequências

### Positivas

- O edital se exprime sem fase artificial nem regra escondida em código, e a mesma mecânica serve a qualquer fase ou etapa.
- Ninguém perde o direito de recorrer quando o preliminar é substituído por retificação.
- O caminho do voto ao definitivo é auditável: versões do recurso, votos por vaga, documento conferido e composição do definitivo.
- O prazo só corre de publicação oficial confirmada, igual para todos.

### Negativas

- A publicação do edital ganha mais conferências: opção de quem recorre por janela, banca prevista e regra de deliberação completa.
- O definitivo com recursos depende de um passo manual, fora do sistema, até haver integração com a assinatura gov.br.
- A ciência individual vira capacidade sem uso; o código que a aceita na configuração do recurso precisa ser retirado (uniplus-api#2307).
- A publicação confirmada tarde obriga a republicar o ato, com novo prazo inteiro.

### Neutras

- Fica proibido: deduzir quem recorre pelo tipo da fase ou do processo; julgar recurso antes do fim do prazo; apagar recurso, versão, voto ou documento; publicar o documento das decisões ou mostrá-lo ao candidato.
- Pré-produção: configurações de recurso já gravadas com âncora em ciência individual não são migradas; são recusadas na próxima publicação ou retificação e refeitas pelo administrador.

## Confirmação

- Domínio: a publicação do edital é recusada com erro nomeado sem opção de quem recorre, sem banca ou com regra de deliberação incompleta, e com janela ancorada em ciência individual.
- Domínio: recurso fora da opção da janela, fora do prazo, ou contra ato com publicação não confirmada é recusado; publicação confirmada no início da contagem ou depois não aceita recursos.
- Domínio: o definitivo com recursos é recusado sem documento conferido; o definitivo do novo ciclo não espera os recursos do ciclo substituído.
- Recursos, versões, votos e documento das decisões são entidades forenses, append-only e fora do soft delete ([ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md)).
- O desenho cabe nos limites da [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md): a regra de deliberação é um avaliador por critério, desempate e encerramento, e não uma árvore de condições num só método.

## Prós e contras das opções

### A. Regras fixas por tipo de fase

- Bom, porque a configuração do edital fica menor.
- Ruim, porque contradiz o UNI-REQ-0270 e o UNI-REQ-0275, e a correção do preliminar existente deixaria recurso julgado contra regra que já não vale.

### B. Tudo declarado no edital e congelado

- Bom, porque cada edital é exprimível, e o ciclo e o documento preservam o recurso e a auditoria.
- Ruim, porque aumenta as conferências da publicação e mantém o passo manual da assinatura.

### C. Deliberação fora do sistema

- Bom, porque não exige modelar a regra de deliberação.
- Ruim, porque perde o voto por conta e por vaga (UNI-REQ-0277) e a visibilidade dos votos por permissão (UNI-REQ-0291).

## Mais informações

- Emenda a [ADR-0145](0145-recurso-por-etapa.md): a ciência individual sai do recurso, e a janela declara quem pode recorrer.
- Emenda a [ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md): a regra de recurso da fase também declara quem recorre e a banca.
- Emenda as ADRs [0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) e [0105](0105-modulo-publicacoes-registro-central-dos-atos.md): ao lado da correção como relação entre atos, há o novo ciclo do item, com publicação original própria e referência à publicação substituída.
- Emenda a [ADR-0157](0157-perfis-de-acesso-versionados-e-aprovados.md): a designação da banca é ato do processo, distinto da concessão de perfil.
- Relacionadas: [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md), [ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md), [ADR-0133](0133-divulgacao-do-certame-materializada-no-registro-do-ato.md), [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md).
- Requisitos: UNI-REQ-0080, UNI-REQ-0115, UNI-REQ-0157, UNI-REQ-0160, UNI-REQ-0269 a UNI-REQ-0291.
- Pendências: gatilhos do novo ciclo para além da homologação das inscrições e ajuste do UNI-REQ-0157 (uniplus-developers#445); confirmação por escrito do UNI-REQ-0282; retirada da ciência individual da configuração do recurso (uniplus-api#2307).
- Issue: uniplus-api#2306.
