---
status: "proposed"
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
