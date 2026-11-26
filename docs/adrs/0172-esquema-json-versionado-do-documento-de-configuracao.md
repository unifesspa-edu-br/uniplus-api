---
status: "accepted"
date: "2026-11-23"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0172: O documento de configuração do processo é descrito por um esquema JSON versionado

## Contexto e enunciado do problema

A configuração de um processo seletivo precisa sair de um processo e entrar em outro, e de uma versão do sistema para outra, por um contrato público, estável e validável fora da API: quem importa, quem exporta e quem escreve um rascunho à mão precisam saber o que é válido sem ler o código. O envelope canônico da publicação ([ADR-0109](0109-envelope-canonico-v2-do-congelamento.md)) serve ao congelamento e à prova de integridade, e não a esse papel: ele carrega identificadores internos e a forma dele é decidida pela publicação.

A [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md) define o registro de esquemas, que também guarda esquema JSON 2020-12, e a [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) trata a oferta de curso como cadastro citado pelo processo. Falta decidir como o documento de configuração é descrito, referenciado e evoluído.

## Drivers da decisão

- O documento circula entre processos e versões; o que o descreve não pode depender de identificador do banco.
- Editar o texto de um item (rótulo, nome) não pode virar retirada e inclusão do item nas retificações.
- O contrato precisa poder crescer sem quebrar quem já exporta, e o que quebra tem de ser detectado por teste, não por revisão.
- Dado pessoal e dado sensível não entram no documento.

## Opções consideradas

- Esquema JSON 2020-12 versionado no repositório, dividido por assunto, com referências por código e por chave local.
- Reaproveitar o envelope canônico como contrato de troca.
- Esquema único, em um só arquivo, sem versionamento por pasta.

## Resultado da decisão

**Escolhida:** "Esquema JSON 2020-12 versionado no repositório, dividido por assunto, com referências por código e por chave local", porque separa o contrato de troca do envelope de publicação e o torna verificável por teste.

- **Forma:** os esquemas ficam em `contracts/selecao/configuracao/vN/`, um arquivo por assunto, com `$id` absoluto e `$ref` relativo e só local. O arquivo `documento` é a raiz; as definições compartilhadas ficam em arquivos próprios. O esquema descreve a forma (tipo, formato, enumeração, tamanho, obrigatoriedade e restrições estruturais entre campos vizinhos); a regra de negócio é do domínio, e por isso um rascunho incompleto é um documento válido.
- **Identidade:** catálogos e unidades são citados pelo código estável, nunca por identificador do banco, e os campos de código de catálogo recusam a forma de identificador único (UUID). Fase, etapa, item de avaliação, nível de atendimento, banca e exigência têm chave local única no respectivo escopo, usada pelas referências; a parte identificada por códigos de catálogo mantém a identidade natural. A pessoa é citada pelo identificador da conta dela no Uni+, texto opaco, nunca por dado pessoal; o esquema não distingue um identificador de conta de outro texto, e a recusa do código que não é conta do cadastro é do domínio. O nome vem do cadastro na emissão.
- **Só leitura:** o descritivo que acompanha o código (nomes, siglas, rótulos dos valores de enum, dados do arquivo do modelo e identificadores) e o que é derivado ficam marcados `readOnly`; no documento congelado, o descritivo registra os valores da publicação; a importação os ignora, e o leitor do documento congelado ignora os descritivos de contexto: a referência é o código ou a chave, e o servidor recalcula o derivado.
- **Resolução:** a importação e o salvamento do rascunho resolvem cada código citado uma vez, contra o cadastro vivo, e dali decidem o identificador do snapshot. Código que não existe é apontado como diagnóstico ao salvar, na posição do documento, e recusado na publicação e na retificação. Na retificação, código igual ao já congelado herda a cópia congelada.
- **Evolução:** a compatibilidade é para trás dentro da versão: documento válido na `v1` continua válido. Entram na versão a propriedade opcional nova, o valor novo de enumeração e o limite relaxado. Criam a versão seguinte (outra pasta, outro `$id`, outro `versaoFormato`) remover ou renomear propriedade, tornar propriedade obrigatória, estreitar tipo, formato, limite ou enumeração e mudar o significado de um campo.
- **Congelamento:** a versão congela na primeira tag que a publica, e a cópia dos arquivos passa a `publicadas/vN/`. Dali em diante um teste compara o esquema atual com cada cópia publicada e recusa a mudança incompatível que o comparador reconhece; o que ele não examina fica para a revisão.
- **Registro:** o esquema é registrado no registro de esquemas com regra de compatibilidade desligada, porque a versão atual do registro recusa a segunda versão de um esquema 2020-12 sob regra de compatibilidade; a garantia vive no teste acima.

> **Emenda (2026-11-17) — a ordem de uma lista é a posição do item.**
>
> A ordem de várias listas do documento era um número declarado ao lado da posição do item: duas fontes para a mesma informação, e recusas de ordem repetida que só existiam por causa disso. Quando a ordem de uma lista é a sequência dos itens, **ela é a posição do item na lista enviada, e o documento não traz número de ordem**. Quem lê atribui a ordem pela posição de origem, começando em 1, sem renumerar quando alguma parte é recusada; a recusa que depende da ordem aponta o item.
>
> Reordenar é mover o item, e a chave dele não muda. Deixam de existir as recusas de ordem repetida vindas do documento. Cada lista adota o critério na entrega que a lê; a das fases do cronograma já o adota.
>
> **Emenda (2026-11-17) — a publicação do esquema no contrato REST.**
>
> A OpenAPI do módulo publica o esquema como componentes, derivados na geração dos mesmos arquivos, sem cópia. A raiz é `DocumentoDaConfiguracao`; cada arquivo e cada definição alcançados viram um componente, com nome fixado por regra (prefixo, arquivo, definição). As referências relativas passam a apontar o componente, e `$id` e `$schema` saem. Os arquivos continuam sendo a fonte e o que a API usa para validar; os clientes geram tipos a partir da OpenAPI.
>
> **Confirmação:** teste de contrato em que cada componente do documento na OpenAPI publicada é o trecho correspondente dos arquivos, com as referências trocadas pela regra, sem componente sobrando ou faltando.
>
> **Consequências:** a OpenAPI do módulo cresce o tamanho do esquema. Os geradores de tipo ignoram as condicionais (`if`/`then`/`else`), então o tipo gerado é mais largo que o esquema, e a validação da forma é a da API. A versão seguinte do formato decide o nome dos componentes dela.
>
> **Emenda proposta (2026-11-17) — forma dos parâmetros.**
>
> A forma dos parâmetros vive somente no esquema do documento. O catálogo guarda apenas o que a versão da regra declara de si. Mudança na forma dos parâmetros exige versão do documento, não da regra.
>
> **Confirmação:** teste que compara os códigos das regras de eliminação e desempate no catálogo e no esquema do documento e verifica que a declaração de cada uma está vazia.
>
> **Emenda (2026-11-19) — os limites de tamanho do documento.**
>
> O limite de valores, objetos e listas (10.000) vale para o documento declarado, depois de retirados os campos só de leitura que a exportação acrescenta; assim, o documento que o rascunho guardou volta inteiro depois de exportado. O texto recebido tem um teto próprio e maior de valores (100.000), conferido enquanto o texto é percorrido e antes de a árvore ser montada, para que a leitura não consuma memória sem limite. Os dois limites recusam com o mesmo código `DocumentoDaConfiguracao.AcimaDoLimite` e a mesma resposta de conteúdo grande demais. O limite de bytes do documento declarado, que o texto recebido pode ultrapassar quando traz os descritivos da exportação, fica para decisão própria.
>
> **Emenda (2026-11-20) — o recurso no documento: efeito suspensivo, guarda da vaga e prazo da 2ª instância, dentro da `v1`.**
>
> O recurso da fase e o da etapa usam as mesmas propriedades no esquema. Em vez do efeito suspensivo com valor e unidade por instância, o recurso passa a declarar a escolha do efeito suspensivo na 1ª instância, a guarda da vaga e o prazo para recorrer em 2ª instância, como decide a [ADR-0177](0177-efeito-suspensivo-por-periodo-segunda-instancia-e-guarda-da-vaga.md). No esquema:
>
> - **Propriedades.** Saem as quatro propriedades do efeito suspensivo com valor e unidade por instância. Entram, todas opcionais no esquema, `efeitoSuspensivo` (três opções: `PARA_TODOS_OS_RECURSOS`, `SEM_EFEITO_SUSPENSIVO` e `UNIDADE_DECIDE_EM_CADA_RECURSO`, conforme a emenda de 26/11/2026 da ADR-0177), `guardaDaVaga` (guardar até o resultado definitivo ou não guardar), `prazoSegundaInstanciaValor` e `prazoSegundaInstanciaUnidade`. A 2ª instância sempre tem efeito suspensivo, e por isso não há propriedade para escolhê-lo. As opções são os códigos de contrato dos enums do domínio ([ADR-0176](0176-codigo-de-contrato-dos-enums-declarado-por-atributo-e-lido-por-catalogo-no-kernel.md)). Fase e etapa continuam recusando propriedade desconhecida: o documento que ainda traz uma propriedade retirada é recusado com o caminho dela.
> - **O que o esquema recusa e o que o domínio decide.** O esquema recusa código fora da enumeração, valor nulo em qualquer dessas propriedades e mais de quatro casas decimais nos prazos. Não exige efeito suspensivo nem prazo da 2ª instância, e não impõe valor mínimo ao prazo: a falta do efeito, a falta do prazo da 2ª instância quando o recurso admite instância superior, o prazo da 2ª instância sem instância superior, o prazo menor ou igual a zero e a guarda "não guarda" com instância superior são recusas do domínio, com código próprio. A guarda ausente vale "até o resultado definitivo".
> - **Dias corridos.** A unidade dos dois prazos, o de interposição e o da 2ª instância, passa a listar horas, dias corridos (`dias`) e dias úteis; acrescentar valor à enumeração é mudança compatível. O domínio continua sem aceitar dias corridos: o documento que os traz deixa de ser recusado como erro de formato e passa a ser recusado pelo domínio, com o código próprio de prazo em dias corridos. É a única mudança de comportamento do prazo de interposição no documento: a contagem do prazo não muda, e o valor zero ou negativo já chegava ao domínio. A lista de unidades que a pessoa escolhe continua sem dias corridos.
> - **Dentro da `v1`, sem versão nova.** A regra de evolução protege o documento válido numa versão congelada; retirar propriedade de versão congelada criaria a `v2`. A `v1` ainda não congelou: não existe a pasta `publicadas/` e nenhuma tag do repositório contém a `v1`. Pela regra de congelamento desta ADR, a mudança é feita na própria `v1`, como na emenda de 17/11 sobre a ordem das listas, e não há documento de versão congelada a preservar. A regra de prazo de recurso do catálogo mantém o código e a versão. O exemplo em `contracts/selecao/configuracao/exemplos/` acompanha o contrato novo. A partir da primeira tag que contiver a `v1`, vale a regra de evolução: acrescentar opção ao efeito suspensivo é compatível; retirar ou renomear propriedade, não.
>
> **Confirmação:** o teste de paridade confere as opções de `efeitoSuspensivo`, `guardaDaVaga` e das unidades de prazo com os valores dos enums do domínio; o teste de contrato confere que o exemplo publicado valida contra o esquema e que as propriedades retiradas são recusadas como desconhecidas.
>
> **Emenda (2026-11-20) — a fase não declara a fase que conclui o ciclo de recurso, dentro da `v1`.**
>
> Cada fase publica o próprio resultado definitivo, e o ciclo de recurso fecha dentro dela, como decide a [ADR-0177](0177-efeito-suspensivo-por-periodo-segunda-instancia-e-guarda-da-vaga.md). Sai da fase do cronograma a propriedade `faseConcluinteChave`, e a fase que ainda a traz é recusada como propriedade desconhecida, com o caminho. A mudança é feita na própria `v1` pelo mesmo motivo da emenda anterior: a `v1` ainda não congelou. A exigência de que a fase com resultado preliminar publique também o definitivo é do domínio, conferida na publicação; o esquema não a expressa.
>
> **Confirmação:** o teste de contrato deve conferir que o exemplo publicado valida sem a propriedade e que a fase que a traz é recusada.

## Emenda (2026-11-23) — chave, campos só de leitura e nomes da `v1`, dentro da `v1`

Todas as mudanças abaixo são feitas na própria `v1`, pelo mesmo motivo das emendas de 20/11: a `v1` ainda não congelou, não existe a pasta `publicadas/` e nenhuma etiqueta a contém. A partir da primeira etiqueta, vale a regra de evolução.

**1. Chave local.** As partes com chave são fase, etapa, item de avaliação, nível de atendimento, banca (da fase e da etapa, única no documento inteiro) e exigência de documento. Formulário, modelo, publicação prevista, procedimento de recurso e regra de comprovação ficam sem chave; a identificação do formulário segue a ADR-0171. O documento de troca não traz número de ordem: a ordem é a posição na lista (emenda de 17/11).

Quem forma a chave:

- No documento importado, a chave vem no documento; o sistema não atribui chave a parte que chega sem ela.
- Nas gravações por dimensão (`PUT`), o servidor forma a chave uma vez, quando a parte é criada, e ela não muda mais: mudar o texto, a ordem ou os dados da parte não muda a chave. A parte já gravada é reconhecida pela identidade que o pedido traz: o tipo de fase na fase; o id na etapa, no item e na banca; a chave, opcional, no nível e na exigência. A parte reconhecida mantém a chave; as demais recebem a chave formada pelo servidor.
- No documento do rascunho (criação e substituição integral), a chave vem no documento, como na importação: a tela forma a chave da parte nova pela regra abaixo, uma vez, ao criar a parte, e a guarda; a API guarda a chave como veio e recusa a parte sem chave (422, no ponteiro da chave que falta).

Regra de formação, a mesma no servidor e na tela:

1. A chave é o prefixo da parte (`fase`, `etapa`, `item`, `nivel`, `banca`, `exigencia`), `_` e um texto.
2. O texto é o código do catálogo (tipo de fase, tipo de etapa, tipo de banca, tipo de documento); no item de avaliação e no nível de atendimento, é o rótulo.
3. O texto vai sem acento e em minúsculas; tudo o que não é letra de `a` a `z` ou dígito vira `_`, sem `_` repetido nem nas pontas.
4. Se o texto já é o prefixo ou começa por prefixo e `_`, o prefixo não se repete (`BANCA_HETEROIDENTIFICACAO` forma `banca_heteroidentificacao`); começar pelas mesmas letras sem o `_` não conta (`BANCARIA` forma `banca_bancaria`).
5. Se o texto fica vazio depois da limpeza, a chave é só o prefixo (`item`).
6. Se a chave já existe, recebe o menor sufixo livre `_2`, `_3`…. Conta-se o conjunto das partes do mesmo tipo e escopo (fases, etapas e exigências no processo; itens e níveis na etapa; bancas da fase e da etapa juntas no documento inteiro) e toda referência `*Chave` presente no documento, inclusive a que cita parte já excluída. Na gravação por dimensão, as chaves em uso são as das partes reconhecidas do mesmo escopo; nas bancas, são as das fases e das etapas de todo o documento. As referências por chave só existem no documento de troca.
7. Acima de 64 caracteres, corta na última palavra inteira que cabe em 64 caracteres, contando o sufixo.
8. O formato final é `^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$`, com até 64 caracteres.

**2. Identificadores.** A exportação traz `id` só de leitura do processo, das fases, etapas, itens de avaliação, bancas e exigências. A importação os ignora e cria novos identificadores; as referências entre partes usam as chaves. O nível de atendimento não declara `id` no documento de troca.

**3. Campos só de leitura de contexto.** O documento da configuração e o documento congelado da versão publicada trazem, ao lado de cada referência, campos só de leitura que dão contexto a quem lê:

- de cada valor de enum, o rótulo (ADR-0176);
- da unidade, o nome e a sigla ao lado do código;
- o nome do tipo de fase, da categoria julgada, do tipo de etapa, do tipo de banca, do tipo de documento, do modelo de formulário de origem e do tipo de ato, ao lado do código;
- na exigência, os dados do arquivo do modelo de documento;
- os identificadores (`id`) do processo e das partes indicadas no item 2.

Onde ficam:

- Na `v1`, cada campo só de leitura é uma propriedade solta, declarada ao lado do campo que descreve, com `readOnly`, no padrão de `unidadeResponsavelNome`; o rótulo do valor de enum é `{campo}Rotulo`.
- No documento congelado, os rótulos dos valores de enum ficam no subobjeto `contexto` do objeto que grava o valor (ADR-0176).

Regras comuns:

- No documento congelado, esses campos registram os valores que o processo guarda na publicação: entram nos bytes e no resumo criptográfico da versão, e a mudança posterior do cadastro ou do rótulo não muda a versão publicada (ADR-0061).
- A exportação da versão publicada deve converter o documento congelado para a `v1`, sem ler cadastro vivo, com os campos só de leitura da publicação. A exportação do rascunho traz esses campos dos cadastros vivos no momento da exportação. O documento congelado e a `v1` usam os mesmos nomes; um teste valida contra a `v1` os três envelopes de referência dos testes, depois de convertidos para o formato `v1`, e a exportação. O teste acompanha o mesmo conjunto de mudanças.
- A configuração usa o código ou a chave como referência. O leitor do documento congelado ignora o contexto de rótulos; os nomes copiados ao salvar (tipo de fase, categoria julgada, modelo de origem, tipo de ato) são lidos e conferidos. O leitor conserva os identificadores necessários à reconstituição; a importação ignora os campos só de leitura.
- A retificação compara pelo que se grava: diferença só em campo só de leitura não é mudança.

**4. Nomes da `v1`.** Os nomes abaixo são os definitivos, os mesmos do documento congelado:

| Arquivo e propriedade da `v1` | Nome anterior | Nome definitivo |
|---|---|---|
| `etapas.schema.json`, recurso da etapa | `ancora` | `eventoInicialDoPrazo` |
| `cronograma.schema.json` e `etapas.schema.json` | `produtos` | `publicacoesPrevistas` |
| `comuns.schema.json`, publicação que inicia o prazo do recurso | `atoAncoraCodigo` | `publicacaoDeReferenciaAtoCodigo` |
| `cronograma.schema.json`, fase | `faseCanonicaCodigo` | `tipoFaseCodigo` |
| `etapas.schema.json` | `regraConsolidacao` | `regraDeApuracaoDaAvaliacao` |
| `restricao-de-valor.schema.json`, limite de data | `ancora` | `referenciaDaData` |
| `restricao-de-valor.schema.json` | `fatos` | `codigosDosDados` |
| `cascata-de-remanejamento.schema.json` | `fallbackCodigo` | `destinoFinalCodigo` |
| `exigencias.schema.json` | `fatosComprovados` | `dadosComprovados` |
| `exigencias.schema.json` | `condicaoGatilho` | `condicoes` |
| `coleta.schema.json` e `item-do-formulario.schema.json` | `fatoCodigo` | `codigoDoDado` |
| `coleta.schema.json` | `regrasDerivacao` | `calculosDosDados` |
| `formulario.schema.json` | `etapas` | `paginas` |
| `formulario.schema.json` e `item-do-formulario.schema.json` | `etapaCodigo` | `paginaCodigo` |
| `item-do-formulario.schema.json` | `precondicao` | `exibicao` |
| `coleta.schema.json` | `referenciaTemporalFatos` | `referenciaTemporalDosDados` |
| `documento.schema.json` | `bonusRegional` | `fada` |
| arquivo do esquema | `bonus-regional.schema.json` | `fada.schema.json` |
| `fada.schema.json` | `fatoElegibilidadeCodigo` | `codigoDoDadoDeElegibilidade` |
| `fada.schema.json` | `baseLegalBonusRegionalCodigo` | `baseLegalCodigo` |
| `predicado.schema.json` | `fato` | `codigoDoDado` |

O documento congelado e a resposta da versão vigente usam os mesmos nomes, inclusive os nomes "dado" (por exemplo, `dadosDosFormularios`, `calculosDosDados`, `dadosCalculados` e `codigoDoDado`); nenhum nome fora do padrão fica em nenhum dos três. FADA é o Fator de Atenuação das Desigualdades de Acesso; no documento congelado, o bloco é `fada`, e `baseLegalBonusRegionalId` passa a `baseLegalId`, ao lado de `baseLegalCodigo`. Os títulos e as descrições de `fada.schema.json` usam o nome por extenso, sem "bônus". O `nome` do processo fica na raiz do documento congelado e é `identificacao.nome` na `v1` (ADR-0109, emenda 7). O `tipoFaseOrigemId` é do documento congelado (identificador da cópia do tipo de fase); a `v1` cita o tipo de fase pelo código (`tipoFaseCodigo`) e não leva o identificador. Esses nomes são os adotados na `v1`. A descrição do campo de referência da fase em `coleta.schema.json` também sai do verbo "ancora" ("fase do cronograma que serve de referência").

**5. Prazo de emissão da exigência.** Em `exigencias.schema.json`, `idadeMaximaEmissao` (na exigência e em `$defs`) sai e entram `prazoDeEmissao` e `validade`, com a forma definida para o prazo de emissão: escolha (`semPrazo` ou `comPrazo`), valor, unidade, referência (com `faseChave` para início ou fim de fase) e efeito fora do prazo. O documento que ainda traz `idadeMaximaEmissao` é recusado como propriedade desconhecida, com o caminho.

**6. Motivos de decisão e prova presencial.** A `v1` declara as duas partes; elas devem ter os mesmos nomes do documento congelado (ADR-0109):

| Arquivo e propriedade da `v1` | Nome anterior | Nome definitivo |
|---|---|---|
| `documento.schema.json` | `motivos` | `motivosDeDecisao` |
| `motivo.schema.json` | `tipoCodigo` | `tipoDeDecisaoCodigo` |

Os demais nomes ficam como estão (`faseChave`, `etapaChave`, `codigo`, `descricao`, `subclassificacao`, `sentido`, `justificativaDaInclusao`, `versaoDoEditalDeEntrada`, `temProvaPresencial`, `municipios`, `codigoIbge` e `uf`).

Campos só de leitura novos, pela regra do item 3:

- no município de prova, `nome`, o nome do município no Geo;
- no motivo, `sentidoRotulo`.

Os caminhos dos erros dos motivos acompanham o nome novo (`motivosDeDecisao[i]`). O limite de municípios continua fora do esquema: é configuração da API, com o padrão de 100 municípios (UNI-REQ-0407). A importação do resultado fica como está nesta emenda; a mudança dela está retida para decisão posterior.

**7. Código de cadastro inexistente no rascunho.** No documento do rascunho, ao salvar e ao importar, o código de cadastro que não existe (tipo de etapa, tipo de banca, tipo de documento e os demais catálogos citados) não recusa o salvamento: é apontado como diagnóstico, na posição do documento, e o rascunho é salvo (200). A recusa (422) vem só na publicação e na retificação. A forma do documento continua recusando o salvamento inteiro (422), como a uniplus-api#2523 descreve.

**Confirmação:** o teste de contrato deve conferir que o exemplo publicado valida com os nomes definitivos e com `prazoDeEmissao` e `validade`; que a chave formada segue a regra (com a mesma tabela de exemplos nos testes do servidor e da tela) e o padrão (aceita `etapa_prova_objetiva_2`; recusa `prova-objetiva`, `Etapa`, `1etapa`, `etapa__x`, `etapa_` e 65 caracteres); e que os campos só de leitura vêm no documento congelado e na exportação, têm os descritivos ignorados pelo leitor e os campos só de leitura ignorados pela importação, e a retificação que difere só neles não é mudança. O teste deve validar contra a `v1` os três envelopes de referência convertidos e a exportação da versão publicada, no mesmo conjunto de mudanças.

## Consequências

### Positivas

- Quem importa, exporta ou escreve o documento valida contra um contrato público, sem a API.
- Mudança incompatível é detectada por teste antes de chegar à publicação.
- O documento não carrega dado pessoal. Os identificadores só de leitura do processo e das partes que os declaram vêm na exportação e são ignorados na importação.

### Negativas

- Todo catálogo citado precisa de código único entre os itens vivos e de leitura por código; o que ainda não tem passa a ser entrega prévia.
- O esquema conta caracteres Unicode e o domínio conta unidades UTF-16: em casos extremos o esquema aceita o que o domínio recusa, e a recusa final é do domínio.

### Neutras

- O envelope canônico de publicação continua como está; esquema e envelope têm funções distintas e nomes que devem coincidir nos conceitos que compartilham.

## Confirmação

- Teste de contrato: os exemplos publicados validam contra o esquema, o rascunho incompleto é aceito, e tipo errado, propriedade desconhecida e identificador interno no lugar do código são recusados com o caminho.
- Teste de compatibilidade: o esquema atual contra cada cópia presente em `publicadas/`, com pares sintéticos para cada regra do comparador. A presença das cópias depois da primeira tag é conferida na revisão dessa tag.
- Testes de paridade: cada enumeração do esquema tem os valores do vocabulário do domínio, e os limites de tamanho listados batem com os que o codec aplica (iguais onde vêm de coluna do domínio, nunca maiores onde o esquema é mais estreito).

## Prós e contras das opções

### Esquema por assunto, versionado, com referência por código e chave local

- Bom, porque separa o contrato de troca do envelope, evita identificador interno e se verifica por teste.
- Ruim, porque exige código estável e leitura por código em cada catálogo citado.

### Reaproveitar o envelope canônico

- Bom, porque não cria um segundo formato.
- Ruim, porque o envelope carrega identificador interno e muda conforme a publicação; vincular a importação a ele travaria os dois.

### Esquema único sem versionamento por pasta

- Bom, porque é um arquivo só.
- Ruim, porque passaria de mil linhas, e uma mudança incompatível não teria onde nascer sem quebrar quem já exporta.

## Mais informações

- [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md), [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) e [ADR-0109](0109-envelope-canonico-v2-do-congelamento.md).
- Identidade e proteção de dados: [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md), [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md) e [ADR-0153](0153-projecao-por-protecao.md).

## Emenda (2026-11-25) — a exigência cita o modelo de documento enviado pelo identificador

O modelo de documento é um arquivo enviado ao próprio rascunho, confirmado e imutável. Sua integridade é conferida pelo resumo SHA-256. Ele não é um cadastro e não tem código estável para ser citado.

A exigência cita o modelo pelo identificador devolvido no envio, na propriedade `modeloId` de `exigencias.schema.json`. Essa é a única referência gravável por identificador a um registro fora do documento. A citação vale somente no rascunho ou no processo dono do envio.

Ao salvar, se a citação não resolve ou se o modelo ainda não foi confirmado, o rascunho é salvo com diagnóstico no ponteiro `/documentosExigidos/exigencias/{i}/modeloId`. A publicação é recusada enquanto a causa persistir. Ao importar em outro rascunho, o salvamento aponta o modelo como inexistente; o arquivo deve ser enviado novamente ao novo dono.

A exportação descreve cada modelo resolvido com o nome do arquivo, o formato e o resumo. O identificador também se chama `modeloId` no documento congelado e na gravação por dimensão.

**Confirmação:** o teste de contrato do esquema admite como referência por identificador somente o caminho `exigencias.schema.json/exigencia/modeloId`, exige igualdade com esse caminho e falha se a propriedade mudar de lugar.

**Consequências:** qualquer outra referência por identificador continua proibida e exige nova emenda e mudança do teste.

## Emenda (2026-11-25) — consequências do descumprimento

Pela [ADR-0179](0179-consequencia-do-descumprimento-declarada-na-parte-do-processo.md), o esquema declara `consequenciaDoDescumprimento` na exigência e `consequenciaDoDescumprimento` no grupo de comprovação, `consequenciaDaAusencia` na banca, `consequenciaDaFaltaDeEnvio` no formulário e `abrePrazoParaReenvio` na exigência e no grupo. Até a primeira publicação em produção, o esquema é editado no lugar, sem versão nova.

## Emenda (2026-11-26) — o cadastro de reserva por chamada na `v1`

A `v1` ganha a propriedade `cadastroDeReserva` em `documento.schema.json`, na própria `v1`, pelo mesmo motivo das emendas anteriores: a `v1` ainda não congelou. A propriedade é uma lista de itens definidos em `cadastro-de-reserva.schema.json`, com o mesmo nome do bloco do documento congelado (ADR-0109, emenda 10).

- `numeroDaChamada`, como `pessoasDaReservaPorVaga`, não é exigido pelo esquema: a chamada sem número é recusada pelo domínio. `pessoasDaReservaPorVaga` informa quantas pessoas da reserva a chamada convoca por vaga. `participacaoDaReserva` é a opção do edital para a reserva da chamada (`ANTECIPA_A_HABILITACAO` ou `ASSUME_NA_MESMA_CHAMADA`, UNI-REQ-0505), com `participacaoDaReservaRotulo` só de leitura na exportação.
- O esquema declara só o teto de 2.147.483.647, o limite do inteiro gravado. A presença e o mínimo do número, a repetição de chamada e a exigência da opção quando há pessoas da reserva, que a quantidade zero recusa, são regra de negócio: a API os recusa com os erros de domínio, todos de uma vez, no caminho do item (`cadastroDeReserva[i].numeroDaChamada`, `cadastroDeReserva[i].pessoasDaReservaPorVaga`, `cadastroDeReserva[i].participacaoDaReserva`).
- A propriedade ausente equivale à lista vazia: o processo sem cadastro de reserva.
- Rascunho: ao salvar e importar, as recusas do cadastro de reserva e da manifestação não impedem o salvamento; aparecem como diagnóstico no ponteiro do item, pelas mesmas regras de domínio da gravação por dimensão; a recusa (422) vem na publicação/retificação. A exportação do rascunho traz `participacaoDaReservaRotulo` com o rótulo atual.

**Confirmação:** o teste de contrato valida o exemplo publicado e os três envelopes de referência convertidos contra a `v1`, que agora inclui o cadastro de reserva.

## Emenda (2026-11-26) — a manifestação de interesse na `v1`

A `v1` ganha a propriedade `manifestacaoDeInteresse` em `documento.schema.json`, na própria `v1`, pelo mesmo motivo das emendas anteriores: a `v1` ainda não congelou. A propriedade é um texto, definido em `manifestacao-de-interesse.schema.json`, com o mesmo nome do bloco do documento congelado (ADR-0109, emenda 10).

- O valor é `UMA_VEZ` ou `CONFIRMA_A_CADA_CHAMADA` (UNI-REQ-0503). Os dois preveem a manifestação e a exigem; o segundo exige ainda a confirmação a cada chamada. Não há propriedade separada para "prevista", "exigida" ou "confirmação a cada chamada", nem para o prazo da manifestação, que é da publicação de cada chamada.
- A propriedade ausente é a manifestação não prevista. Não existe valor para "não prevista", e a ausência não impede a publicação.
- O código fora da lista é recusado pelo esquema na forma e, pelo domínio, com o erro `uniplus.selecao.manifestacao_de_interesse.codigo_desconhecido`, no caminho `manifestacaoDeInteresse` e sem repetir o valor.
- Rascunho: ao salvar e importar, a recusa da manifestação não impede o salvamento; aparece como diagnóstico no ponteiro `/manifestacaoDeInteresse`, pela mesma regra de domínio da gravação por dimensão; a recusa (422) vem na publicação/retificação.

**Confirmação:** o teste de contrato valida o exemplo publicado e os três envelopes de referência convertidos contra a `v1`, que agora inclui a manifestação de interesse, e o teste de paridade confere que a lista do esquema é a do catálogo do domínio.
