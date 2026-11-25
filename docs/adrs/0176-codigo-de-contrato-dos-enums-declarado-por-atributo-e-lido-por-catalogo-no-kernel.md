---
status: "accepted"
date: "2026-11-20"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
  - "Equipe Autorização"
  - "Equipe Web"
---

# ADR-0176: Código de contrato dos enums declarado por atributo e lido por catálogo no Kernel

## Contexto e enunciado do problema

Um valor de enum sai do processo de três formas: na resposta da API, gravado no banco e dentro do conteúdo congelado de um processo. Hoje cada enum resolve isso do seu jeito: classe de constantes com conversão escrita à mão, nome do membro escrito pelo conversor de JSON, valor especial para o texto que não se reconhece. O mesmo enum chega a ter duas grafias, e a resposta não traz o rótulo, que o web monta por conta própria.

As permissões já resolveram parte disso: o código, o nome e a descrição ficam em atributo no membro, conferidos na subida ([ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md)). O contrato REST e o OpenAPI ([ADR-0022](0022-contrato-rest-canonico-umbrella.md), [ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md)) pedem que o conjunto de valores de um campo seja publicado, e a validação acumula as recusas ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)).

## Drivers da decisão

- Um só lugar para o código de cada valor, que não muda quando o membro é renomeado.
- Todo valor tem rótulo, e a API o entrega junto do código.
- O contrato publica a lista de códigos sem manutenção paralela.
- Quem lê um código desconhecido decide o que fazer.
- Defeito de declaração derruba a subida.

## Opções consideradas

- Manter uma classe de códigos por enum, escrita à mão.
- Declarar o código por atributo no membro e lê-lo por um catálogo único do Kernel.
- Usar o atributo de nome do membro do conversor de JSON da plataforma.

## Resultado da decisão

**Escolhida:** "Declarar o código por atributo no membro e lê-lo por um catálogo único do Kernel", porque reúne código, rótulo e descrição num lugar, serve ao banco, à resposta e ao conteúdo congelado, e dá ao OpenAPI a lista de códigos sem repetição.

1. **O código é contrato.** Todo membro de enum que circula fora do processo declara código, rótulo e, se útil, descrição. O código é escrito à mão, nunca sai do nome do membro, e trocá-lo é mudar o contrato e o dado gravado. Por isso o atributo não leva "Json" no nome. A subida de cada executável confere a declaração de todos os projetos do Uni+ que ele alcança por referência, antes de iniciar o servidor e os consumidores, acusando todos os defeitos juntos, e todo membro tem rótulo.
2. **Quem lê decide.** O catálogo só traduz entre valor e código; não escolhe recusa, status HTTP nem valor substituto. O domínio dá a recusa acumulada ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)), a consulta decide filtro, valor padrão e 400, a leitura do banco trata linha inválida como defeito, e o leitor de conteúdo congelado decide a versão e a recusa. Tolerância a maiúsculas ou espaços só existe onde foi prometida, e explícita em quem lê. O conjunto que a pessoa pode escolher é regra do domínio.
3. **Sem valor especial de falha.** A ausência legítima vira tipo anulável; estado real fica como valor do enum.
4. **A resposta traz código e rótulo.** O enum vai como `{ codigo, rotulo }`, ou como `{ codigo, rotulo, descricao }` quando o campo precisa da descrição, e nunca sai cru. O OpenAPI publica por enum o componente `{Enum}Codigo`, com a lista de códigos, que o campo `codigo`, o pedido e o filtro referenciam; o formato com descrição tem o componente `{Enum}ComDescricao`. A explicação que vale para todos os códigos é declarada uma vez no enum e publicada no componente `{Enum}Codigo`, antes da explicação de cada código; os componentes de formato não a repetem. Dois tipos diferentes nunca dividem o nome de um componente: a geração do contrato falha. Cor e composição de texto ficam no web.
5. **Um código só por valor,** igual no banco, na resposta e no conteúdo congelado. Onde hoje há duas grafias, o código é padronizado.
6. **O teste fixa os pares.** Um teste por enum fixa os pares valor e código com valores escritos no próprio teste, e alterá-lo é alterar o contrato, com a mesma revisão de uma mudança em `contracts/`. O rótulo não é fixado em teste, porque é texto de tela.
7. **Diferença em relação à [ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md).** A leitura do código, do rótulo e da descrição vale para qualquer enum, o que é aceito aqui. As permissões continuam com a extensão própria da ADR-0151, que também carrega o escopo e a sensibilidade.
8. **As opções vêm da API.** A lista que a pessoa escolhe, em filtro ou em campo de escolha, vem de um vocabulário do módulo dono do enum, na ordem de exibição e restrita ao que o domínio deixa escolher; o conjunto que depende do estado de um recurso vem na resposta do recurso. O web não monta opção a partir do tipo gerado nem escreve rótulo. O rótulo não vai em extensão do OpenAPI, porque o gerador do web não o entrega em execução.
9. **Documento lido por máquina leva só o código.** Conteúdo congelado, colunas JSON, formulário desenhado pelo web e mensagens entre módulos e para fora gravam o enum pelo código declarado; `{ codigo, rotulo }` é forma de campo da resposta e não entra nesses documentos. O que é serializado por opções JSON (colunas JSON e mensagens de fila) usa o conversor de código, que fica fora das opções JSON da resposta HTTP. Os formatos com escritor próprio (envelope do conteúdo congelado, documento de configuração e mensagens Avro) escrevem o código declarado e leem pelo catálogo, sob a evolução de cada formato ([ADR-0109](0109-envelope-canonico-v2-do-congelamento.md), [ADR-0172](0172-esquema-json-versionado-do-documento-de-configuracao.md) e [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md)), e o leitor de cada um mantém a recusa que já tem.
10. **Pedido e consulta.** O pedido traz o código como texto; o domínio o lê pelo catálogo e recusa o código desconhecido com o código de erro do próprio campo, acumulado com as demais recusas ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)), sem repetir o valor recebido ([ADR-0023](0023-wire-formato-erro-rfc-9457.md)). O filtro de consulta recusa o código desconhecido com 400 e código de erro próprio, e o parâmetro ausente significa sem filtro. Os dois publicam os códigos aceitos pelo mesmo componente do enum.

## Consequências

### Positivas

- Código, rótulo e descrição ficam ao lado do membro, e o contrato publica a lista de códigos sem manutenção paralela.
- O web deixa de montar o texto de cada valor.
- Defeito de declaração derruba a subida, com o membro responsável nomeado.

### Negativas

- As classes de códigos existentes migram em issues próprias, por risco; até lá convivem dois padrões.
- A resposta de um campo de enum passa a ser um objeto, e não um texto, o que muda o contrato de quem migra.

## Confirmação

Testes de unidade do catálogo, do OpenAPI e dos pares de cada enum, a conferência na subida de cada executável e o contrato versionado em `contracts/`, que pede revisão quando muda.

## Mais informações

- Relacionadas: [ADR-0022](0022-contrato-rest-canonico-umbrella.md), [ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md), [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md), [ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md).
- Primeiro uso: a situação do acesso do perfil de acesso, no módulo Autorização.

## Emenda 1 (2026-11-20) — rótulo do operador conforme o eixo do recurso

O sentido de um operador depende de um eixo do recurso que o consulta: a cardinalidade do fato. "Igual a" sobre um único valor compara, e sobre um conjunto significa que o conjunto inclui o valor. Por isso o enum `Operador` declara dois rótulos: o de valor único no `CodigoDeContrato` e o de conjunto num atributo próprio do projeto de regras (`RotuloSobreConjunto`), lido por reflexão uma vez. A resposta do recurso, e não a tela, traz os operadores admitidos, no formato `{ codigo, rotulo }` do item 4, com o rótulo no sentido certo: o web não escolhe sentido. Os operadores admitidos vêm da regra pura `OperadoresDoFato`, a mesma que o validador do predicado usa, e são calculados na resposta, nunca gravados.

## Emenda 2 (2026-11-21) — vocabulário publicado

O enum com código de contrato que um cliente precisa listar é publicado como vocabulário, de um só jeito em todos os módulos:

1. A rota é `GET /api/{modulo}/vocabularios/{plural-do-enum}`. A rota nova usa o plural do nome do enum, sem preposição (ADR-0064). Rota publicada não muda de nome depois da primeira publicação em produção; antes dela, só muda por decisão própria, como `fases-canonicas` para `tipos-de-fase` (emenda 3, item 4). Tirar a preposição não é motivo para mudar: `naturezas-conceito`, `tipos-de-decisao` e `papeis-de-banca` ficam como estão.
2. Há um controller por assunto, `Vocabularios{DoAssunto}Controller`, com uma ação por vocabulário e a tag `Vocabularios`; não há rota genérica por nome.
3. Cada vocabulário tem consulta e manipulador próprios, chamados pelo barramento; o manipulador decide o conjunto escolhível por `CatalogoDe<T>.Valores()`.
4. A resposta é uma lista de `{ codigo, rotulo }` na ordem de declaração do enum, com teste da ordem; `{ codigo, rotulo, descricao }` só quando todo membro tem descrição.
5. O acesso é anônimo: a lista é fechada e não traz dado de ninguém; a proteção fica na ação que usa o código.
6. Não há cache.
7. O dono é o módulo do enum. O enum usado só por um módulo mora nesse módulo e é servido por ele. Todos os enums do projeto de regras compartilhado são servidos pela Configuração.

O campo de texto de um contrato que carrega o código de um desses enums é marcado com `AceitaCodigosDe<T>`, para o contrato apontar o vocabulário.

## Emenda (2026-11-22) — projeto de testes

A conferência das declarações dos enums alcançados ocorre na subida de cada executável. Não há teste que exija o alcance de todo enum: um tipo não referenciado por um executável não integra seu contrato. A conferência da montagem dos modelos é executada em `Unifesspa.UniPlus.Contratos.UnitTests`.

### Emenda: código da regra no catálogo

Em `RegraCatalogoDto`, `codigo` continua sendo texto, pois identifica a regra. Quando há enum de contrato para a regra, `rotulo` acompanha esse código. O campo `tipo` contém o código e o rótulo do tipo da regra.

## Emenda 3 (2026-11-23) — todo enum gravado declara o código; o rótulo vai ao lado, só para leitura, nos documentos

Decisões do líder técnico (23/11/2026).

1. **Todo enum que é gravado declara o código.** Todo enum do processo seletivo, dos documentos e da inscrição que é gravado no banco, no documento congelado ou no documento da configuração declara código e rótulo pelo atributo (item 1). A coluna do banco grava o código pelo conversor da convenção (`ConversaoDosEnumsDeContrato`); nenhuma coluna grava o número do membro. O item 5 vale sem exceção: um código só por valor, igual no banco, na resposta, no documento congelado e nos documentos. Onde hoje há duas grafias, vale a da resposta: `Aplicabilidade` usa `GERAL` e `CONDICIONAL`, e `CategoriaObrigatoriedade` usa `ETAPA` (com a emenda da ADR-0058, porque o resumo da obrigatoriedade legal passa a usar o código).
2. **O código decide; o rótulo vai ao lado, só para leitura.** O item 9 fica assim para o documento congelado e para o documento da configuração: o valor é gravado pelo código declarado, e o rótulo vai ao lado, só para leitura, como foto do momento em que o documento foi escrito (no documento congelado, o momento da publicação).
   - No documento congelado, todo objeto que grava valor de enum pelo código leva o subobjeto `contexto`, com o rótulo de cada um desses valores.
   - No documento da configuração (`v1`), o rótulo é a propriedade solta `{campo}Rotulo`, com `readOnly`, ao lado do campo.
   - Quem lê decide só pelo código e ignora o rótulo; a importação ignora o rótulo; a retificação não conta a diferença de rótulo como mudança. As colunas JSON e as mensagens entre módulos e para fora continuam só com o código.
3. **Comentário da coluna.** O comentário de cada coluna de enum (ADR-0124) lista os códigos aceitos, sem os rótulos, e é escrito pela convenção `ConversaoDosEnumsDeContrato`, a partir do atributo do enum.
4. **Rota do tipo de fase.** A tabela se chama `tipos_de_fase`. A rota `fases-canonicas` (`/api/configuracao/fases-canonicas`, `/api/configuracao/admin/fases-canonicas` e `/api/configuracao/vocabularios/fases-canonicas`) será renomeada para `tipos-de-fase` depois do fim de 25/11 (uniplus-api#2606), sem manter a rota antiga; o item 1 da emenda 2 vale a partir da primeira publicação em produção.

## Emenda 4 (2026-11-24) — formato único do código de enum

Decisões do líder técnico (24/11/2026).

1. **Um só formato.** Todo código de enum declarado, no banco, na API, no documento congelado, no documento da configuração (`v1`) e na web, segue o padrão `^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$`: letras maiúsculas e algarismos, palavras separadas por sublinhado, sem acento, até 80 caracteres. Sigla oficial e número entram como uma palavra (`AREA_ENEM`, `LEI_12711`). O código é escrito à mão e repete as palavras do membro (`EM_VERIFICACAO`).
2. **A subida recusa código fora do formato.** A conferência dos códigos (`ConferenciaDosCodigos`) confere o valor de cada código em execução e recusa o que não segue o item 1. Ela não lê código-fonte como texto.
3. **Categoria da obrigatoriedade legal.** Os códigos de `CategoriaObrigatoriedade` seguem o formato do item 1: `ETAPA`, `MODALIDADE`, `DESEMPATE`, `DOCUMENTO`, `FADA`, `ATENDIMENTO` e `OUTROS`; a categoria antes chamada `Bonus` tem o código `FADA`.
4. **Ficam fora do formato:** código de permissão (ADR-0151); código de erro (ADR-0023 e ADR-0102); o discriminador `$tipo` e os nomes de campo do JSON; valores de padrão externo, como INEP, UF e `utf-8`.
5. **Nenhuma coluna grava número.** Isso vale também para as colunas que hoje gravam o número do membro (idempotência, chamadas, convocações, matrículas e sincronização com o SIGAA), que passam a gravar o código.

## Emenda 5 (2026-11-25) — códigos de cadastro e de formulário

1. **O mesmo formato.** O formato do item 1 da emenda 4 vale também para o código que identifica um registro de cadastro ou um formulário do processo: na gravação, na leitura do banco, no documento da configuração e no documento congelado. O formato é definido uma vez e é o mesmo usado pela conferência dos códigos de enum.
2. **Tamanho.** O máximo é 80 caracteres. Um cadastro pode fixar um limite menor, nunca maior. Não há tamanho mínimo além da letra inicial.
3. **Conferência e comparação.** O código é conferido e comparado como chegou, sem aparar espaços, trocar maiúsculas e minúsculas ou normalizar. Fora do formato, é recusado com o código de erro de formato do próprio campo, junto das demais recusas ([ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md)) e sem repetir o valor recebido ([ADR-0023](0023-wire-formato-erro-rfc-9457.md)). Uma linha do banco com código fora do formato é defeito de leitura.
4. **Código formado a partir de texto.** O código formado a partir de um nome ou de uma identificação segue o mesmo formato. Se ultrapassar o tamanho permitido, é recusado e se pede um texto mais curto.
5. **Adoção.** Os cadastros de texto livre que ainda não têm formato o adotam em trabalho próprio; até lá, não emitem recusa de formato.

## Emenda 6 (2026-11-25) — códigos das partes do formulário

O formato e as regras da emenda 5 valem também para o código da página, do grupo e do termo exigido do formulário, no processo e no modelo, e para a citação da página pelo item e pelo grupo; o limite de cada um continua 60. O código do dado que o item coleta é o código do fato do catálogo: tem o limite dele, 50, definido uma vez, e é conferido pela existência no catálogo, como chegou.
