# Contracts — OpenAPI baselines e códigos de erro

Specs OpenAPI 3.1 versionados como **fonte de verdade do contrato V1** da `uniplus-api` (ADR-0030, story #290), e a lista dos códigos de erro de fio que a API emite.

## Arquivos

- `openapi.selecao.json` — spec do módulo Seleção (endpoints `/api/selecao/processos-seletivos`, `/api/selecao/obrigatoriedades-legais`, `/api/profile/me`).
- `openapi.ingresso.json` — spec do módulo Ingresso (stub atual; endpoints próprios chegam em sprints posteriores).
- `openapi.autorizacao.json` — spec do módulo Autorização (perfis de acesso, sob o prefixo `/api/autorizacao/*`).
- `openapi.organizacao.json` — spec do módulo Organização Institucional (Instituição e Unidades, sob o prefixo de módulo `/api/organizacao/*`, incluindo as variantes administrativas `/api/organizacao/admin/*`). Áreas Organizacionais ficam fora do contrato — em aposentadoria (issue #625), o controller foi removido.
- `openapi.configuracao.json` — spec do módulo Configuração (Campus e Local de Oferta, sob o prefixo de módulo `/api/configuracao/*`, incluindo as variantes administrativas `/api/configuracao/admin/*`). A Cidade é referenciada por código IBGE + display cache (ADR-0090) — não há endpoint de cidade aqui.
- `openapi.publicacoes.json` — spec do módulo Publicações (atos normativos e catálogo de tipos de ato, sob o prefixo `/api/publicacoes/*`, incluindo `/api/publicacoes/admin/*`).
- `openapi.portal.json` — spec da Portal API, o *Backend for Frontend* público do portal do candidato ([ADR-0131](../docs/adrs/0131-portal-como-bff-publico-de-dominio.md)), sob o prefixo `/api/portal/*`. Nasce só com o endpoint de teste; cada endpoint público novo regenera esta baseline.
- `codigos-de-erro.json` — lista dos códigos de erro de fio (`uniplus.<módulo>.<assunto>.<motivo>`) que a API emite, com o status HTTP e o título de cada um. Gerado do código, não escrito à mão; ver [Códigos de erro](#códigos-de-erro).
- Registro de acesso (tópico `autorizacao_registros_de_acesso`): o valor da mensagem é Avro, pelo padrão da [ADR-0051](../docs/adrs/0051-apicurio-schema-registry-avro-wolverine.md), e o esquema é o `LoteDoRegistroDeAcesso.avsc`, embutido em `src/shared/Unifesspa.UniPlus.Authorization/Contracts/Schemas/`, sem cópia nesta pasta: é a mesma fonte que a API registra no Schema Registry na subida, no subject `<tópico>-value`. Versão 1: só entra mudança compatível para trás (campo opcional novo, com default nulo; valor novo nos vocabulários de motivo da negativa, fonte e sensibilidade, que são texto e não mudam o esquema); mudança incompatível cria a `v2` e muda o cabeçalho `uniplus.versao-schema`. O teste de contrato do `Infrastructure.Core.UnitTests` lê o lote serializado de volta pelo esquema e confere que os campos do esquema são as propriedades do registro, nos dois sentidos.
- `selecao/configuracao/v1/*.schema.json` — JSON Schema (draft 2020-12) do documento de configuração do processo seletivo, versão 1, dividido por assunto em arquivos que se referenciam por `$ref` relativo (`documento` é a raiz; `comuns` guarda as definições compartilhadas e `predicado`, a condição, o predicado, a obrigatoriedade e a base legal; `$id` absoluto sob `https://uniplus.unifesspa.edu.br/contracts/selecao/configuracao/v1/`): o formato do rascunho salvo, da importação e da exportação. Descreve só a forma (tipo, formato, enumerações, limites de tamanho, obrigatoriedade estrutural); a regra de negócio fica no domínio, e por isso um rascunho incompleto é válido. Catálogos e unidades entram por código estável, nunca por identificador interno do banco; as referências entre partes do documento usam a `chave` local de cada parte. Os exemplos completos estão em `selecao/configuracao/exemplos/`. Os objetos recusam propriedade desconhecida, então a compatibilidade da `v1` é "documento válido na `v1` continua válido": entram na `v1` a propriedade opcional nova, o valor novo de uma enumeração e o relaxamento de um limite; criam a `v2` (outro arquivo, outro `$id`, outro `versaoFormato`) remover ou renomear propriedade, tornar uma propriedade obrigatória, estreitar tipo, formato, limite ou enumeração, e mudar o significado de um campo. A `v1` congela na primeira tag que a publica: a cópia dos arquivos vai para `selecao/configuracao/publicadas/v1/` e o teste de compatibilidade recusa a mudança que invalidaria documento válido na cópia (propriedade retirada, obrigatoriedade nova, tipo ou enumeração estreitados, limite apertado, padrão, formato ou constante alterados). Os limites de tamanho do esquema contam caracteres Unicode, e o domínio e as colunas contam unidades UTF-16: um texto com muitos caracteres fora do plano básico (emojis, por exemplo) pode passar no esquema e ser recusado pelo domínio, que dá a recusa final com o código de erro próprio. As decisões entram assim: a fase e a etapa declaram os `tiposDeDecisao`, o documento traz os `motivos` que o processo escolheu (identificados pelo tipo e pelo código do catálogo), a fase traz a `regraDeHomologacao` e os `assinantes`, citados só pelo identificador da conta da pessoa no Uni+ (texto opaco, sem nome nem outro dado pessoal; o nome vem do cadastro na emissão). O teste de contrato do `Infrastructure.Core.UnitTests` valida os exemplos e confere que o rascunho incompleto é aceito e que tipo errado, propriedade desconhecida e identificador interno no lugar do código são recusados com o caminho. A exportação traz, ao lado de cada código, o descritivo que o Uni+ já guarda (nomes, siglas, UF) e os valores derivados, todos marcados `readOnly`: a importação os ignora, a referência é o código e o servidor recalcula o que é derivado. Nenhum campo interno entra: id do banco, datas de auditoria e autoria ficam fora, e cada item se identifica pela `chave` local. O documento exportado traz, na raiz e só de leitura, o `id` do processo (uuid; no rascunho, o id do próprio rascunho, que vira o do processo na publicação; ignorado na importação, que gera id novo; não concede pertencimento) e a `versao` exportada (inteiro, ausente no rascunho); os itens não têm id. Traz também `publicadoEm`, o instante da divulgação da versão (na retificação, o da versão retificada), só de leitura e ausente no rascunho. Regra da identidade: o item de lista que o usuário edita e que pode ser alcançado por decisão, fato ou retificação tem `chave` local, porque o rótulo é texto editável e corrigir a grafia numa retificação não pode virar retirada e inclusão; o item identificado por códigos de catálogo (como o produto de uma fase, por `atoCodigo` e `papel`) mantém a identidade natural, e o objeto único por pai fica sem chave. A coleta entra em `formularios[]` (no máximo um por finalidade, que é a chave do formulário), com etapas, itens, grupos repetíveis e termos exigidos, e em `coleta.regrasDerivacao`. O item cita o fato pelo código do catálogo; tipo de campo, formato, classificação de proteção e demais propriedades do fato são do catálogo e não se declaram, e o termo cita o termo pelo código estável e a versão pelo número, nunca pelo identificador interno. O código local de etapa, grupo, termo e opção só vale dentro do formulário (ou do fato, no caso da opção). As vagas entram em `distribuicaoVagas[]` (uma por oferta, citada pelo `ofertaCursoCodigo`), com `opcoesDeCurso` e `cascataRemanejamento` ao lado: o edital declara os insumos (`vagasOfertadasBase`, `percentualDaReservaLegal`, regras, modalidades por código e a `quantidadeDeclarada` que ele fixa), e o quadro de vagas, o total e os descritivos da oferta e da modalidade vão como leitura, porque o sistema os recalcula. O mesmo esquema é publicado, sem alteração, como componentes de `openapi.selecao.json` (`DocumentoDaConfiguracao` para o documento, `DocumentoDaConfiguracao` + arquivo para a raiz de cada arquivo citado e `DocumentoDaConfiguracao` + arquivo + definição para cada definição citada, como `DocumentoDaConfiguracaoComunsChave`): o corpo do `POST` e do `PUT` de `/rascunhos-de-processos-seletivos` e a resposta `200` do `GET` referenciam o componente do documento, e o cliente gerado o tipa. O teste `DocumentoDaConfiguracaoNaOpenApiTests` do `Infrastructure.Core.UnitTests` confere que cada componente é o trecho do arquivo de origem, sem `$id`, `$schema` e `$defs` e com as referências trocadas, que o conjunto é exatamente o que as referências alcançam a partir de `documento.schema.json` e que o contrato REST os referencia. O cliente gerado a partir desse contrato (`openapi-typescript`) não expressa as restrições condicionais do esquema (`if`/`then`/`else`): o tipo gerado é mais largo que o esquema, por exemplo uma propriedade proibida num ramo sai opcional, e a validação da forma é a da API, que dá a recusa final.
- `formularios/casos/` — corpus de casos da avaliação do formulário, rodado pela API e pelo interpretador do front (ADR-0139); o formato está no README da pasta.

## Vocabulários dos dados

A Configuração publica, com acesso anônimo, as listas de códigos e rótulos em ordem de declaração: `GET /api/configuracao/vocabularios/origens-fato`, `fontes-valores-fato`, `formatos-texto`, `escopos-fato`, `componentes-de-exibicao` e `hipoteses-legais-tratamento`. Cada caminho após o primeiro usa o mesmo prefixo `/api/configuracao/vocabularios/`.

## Como o spec é gerado

O pipeline `AddUniPlusOpenApi("modulo", config)` (ver `Infrastructure.Core/OpenApi/`) registra os transformers que formatam o documento em runtime:

1. `UniPlusInfoTransformer` — title pt-BR, contact CTIC, license MIT, servers Produção/Homologação, `info.version = 1.0.0`.
2. `UniPlusOperationTransformer` — adiciona header `Idempotency-Key` em endpoints com `[RequiresIdempotencyKey]`; coage respostas 4xx/5xx para `application/problem+json` (RFC 9457, ADR-0023).
3. `UniPlusSchemaTransformer` — aplica pattern `^\d{11}$` + nota PII a propriedades `cpf`.
4. `BearerSecuritySchemeDocumentTransformer` — declara o esquema `bearerAuth` (HTTP Bearer, formato JWT) em `components.securitySchemes`; o `AuthorizationOperationTransformer` referencia esse esquema em cada operação protegida, na mesma passagem em que adiciona o `401`/`403`.
5. `InstanteSchemaTransformer` — publica todo instante (`DateTimeOffset`, anulável, em lista ou em dicionário) como `type: string` + `format: date-time`: o conversor JSON do instante é próprio, e sem ele o gerador omite o `type` e o schema do item.

6. `CorpoJsonOperationTransformer` e `RespostaJsonOperationTransformer` — escrevem, nas actions marcadas com `[CorpoJson(componente)]` e `[RespostaJson(componente)]`, o corpo `application/json` do pedido e da resposta `200` como `$ref` para o componente de `components/schemas`. O de resposta roda antes do `VendorMediaTypeOperationTransformer`, que copia o schema para a vendor MIME.
7. `EsquemaDoDocumentoNaOpenApi` (só no documento `selecao`, ligado em `SelecaoModuleRegistration`) — publica o esquema do documento de configuração como componentes. Os componentes saem de `ComponentesDoEsquema`, que segue os `$ref` a partir de `documento.schema.json` (vira componente só o que é alcançado, e uma colisão de nome falha a geração), e são escritos como o esquema os declara (`ComponenteDoEsquemaPublicado`): o modelo de schema da biblioteca não os representa fielmente (a `const` que não é texto vira texto e o esquema booleano `false` some), por isso o texto não passa pelo leitor dela.

Endpoint runtime: `GET /openapi/{modulo}.json`.

## Drift check

A integração `OpenApiEndpointTests` nos módulos com baseline (`tests/Unifesspa.UniPlus.{Selecao,Ingresso,OrganizacaoInstitucional,Autorizacao,Configuracao,Publicacoes,Portal}.IntegrationTests/OpenApiEndpointTests.cs`) compara o spec emitido em runtime com o baseline committed nesta pasta. **Qualquer mudança no contrato faz o teste falhar** — clientes externos (frontend, integradores, `uniplus-developers`) ficam protegidos contra breaking changes acidentais.

### Como aprovar uma mudança

Toda ação de controlador declara `[EndpointName("VerboObjeto")]`: nome escrito à mão com verbo do negócio no infinitivo e objeto, em português e PascalCase, sem acento, `Get`, `Post`, `Admin` ou sigla. Não derive o nome do método, da rota ou do verbo HTTP. Ele é único entre os documentos do host e sua alteração muda o contrato. Campo de texto lido pelo domínio ou pela consulta por `CatalogoDe<T>`, em parâmetro de consulta ou propriedade de pedido, leva `[AceitaCodigosDe<T>]`; o filtro é lido de forma exata.

Ao alterar o contrato, execute o teste `SpecRuntime` do projeto de integração do módulo e siga o [procedimento de aprovação](../CONTRIBUTING.md#aprovação-de-referências-de-teste). O teste compara o documento emitido à referência copiada para a saída. Se houver divergência, informa o arquivo obtido para revisão e aprovação; nunca edite os arquivos OpenAPI à mão. Mudanças nos esquemas compartilhados exigem conferir o contrato de Seleção. Execute também o Spectral sobre o contrato aprovado.

## Códigos de erro

O código de erro vai no corpo `application/problem+json` (ADR-0023), e não é um schema do OpenAPI. `codigos-de-erro.json` publica os códigos, os títulos e os status HTTP que alimentam o catálogo de erros. A API confere esse arquivo contra os próprios registros.

### Formato

```json
{
  "versaoDoFormato": 1,
  "codigos": [
    { "codigo": "uniplus.auth.forbidden", "status": 403, "titulo": "Acesso negado" }
  ]
}
```

- `versaoDoFormato`: inteiro; o consumidor confere antes de interpretar a lista. Muda só se o formato mudar, e não quando um código entra ou sai.
- `codigos`: uma entrada por código de fio, ordenada por `codigo` (ordem ordinal), para o diff entre versões mostrar só o que mudou. Cada entrada tem `codigo`, `status` (HTTP) e `titulo` (pt-BR, estável por código, o mesmo do campo `title` da resposta).

### De onde vem

O arquivo é gerado em memória pelo teste `CodigosDeErroPublicadosTests` (`tests/Unifesspa.UniPlus.Contratos.UnitTests/Conferencias/`), que reúne:

1. todas as `IDomainErrorRegistration` dos módulos e do compartilhado (Seleção, Ingresso, Configuração, Organização Institucional, Publicações, Autorização, Discentes, Portal e `Infrastructure.Core`), obtidas dos contêineres dos hosts;
2. as definições da fronteira HTTP (`ErrosDaFronteiraHttp`), de quem o `GlobalExceptionMiddleware`, o `AuthenticationProblemDetailsWriter`, o `VendorMediaTypeAttribute`, a conferência do identificador da rota e a `RespostaDoCaminhoInexistente` leem status, código e título. Um erro novo emitido fora dos registros nasce ali, e não como literal no emissor.

O mesmo código declarado em mais de um registro com o mesmo status e título entra uma vez; com status ou título diferentes, a geração falha e aponta o código e os registros.

### Como aprovar

Execute `dotnet test tests/Unifesspa.UniPlus.Contratos.UnitTests --filter CodigosDeErroPublicados` e siga o procedimento de aprovação acima. Cada código novo exige uma descrição pública no mesmo PR.

### Descrições públicas

`descricoes-de-erros.json` traz a versão do formato e as entradas em ordem
ordinal por `codigo`. Cada entrada contém `requisitos`, com os identificadores
`UNI-REQ-NNNN` aplicáveis, e as seções obrigatórias `oQueAconteceu` e
`comoResolver`. Use `ondeAparece` quando for necessário indicar em quais
operações a recusa ocorre. Cada item de uma seção é um bloco de Markdown
simples, como um parágrafo, lista ou tabela. Use links HTTPS ou caminhos
absolutos do portal. O título e o status vêm de `codigos-de-erro.json`.

Escreva ou atualize a descrição no mesmo PR que muda o código, conforme a
[ADR-0178](../docs/adrs/0178-texto-publico-dos-erros-na-api.md). Use linguagem
voltada a quem recebe o erro. O teste confere pares de códigos, formato,
seções, requisitos e ausência de exemplos com CPF, e-mail ou sequências de
11 ou mais algarismos. Execute `dotnet test tests/Unifesspa.UniPlus.Contratos.UnitTests --filter DescricoesDeErrosPublicadas`. Quando o conteúdo for válido, o teste fornece a formatação canônica para revisão. Códigos sem descrição exigem texto escrito pelo autor.

## Spectral house-style

`tools/spectral/.spectral.yaml` aplica 5 rules Uni+ específicas (pt-BR em info/summary, problem+json em 4xx/5xx, regex `^uniplus.*` em codes, deprecated→x-sunset, Idempotency-Key required em verbs idempotent) sobre os baselines committed. Job dedicado em `.github/workflows/ci.yml#spectral` falha o PR em violação.

## Roadmap (follow-ups)

Itens originalmente listados em #290 que ficam para próximos PRs (mantém este reviewable):

- ~~`contracts/shared.openapi.json` declarando ProblemDetails, Cursor, `_links`, paginação envelopes via `$ref`.~~ Resolvido por [ADR-0035](../docs/adrs/0035-shared-schemas-cross-module-fitness-test.md): Microsoft.AspNetCore.OpenApi 10 não suporta `$ref` cross-document, e Redocly post-process é desproporcional para 3 schemas duplicados. Em vez disso, `OpenApiSharedSchemasInSyncTests` (`tests/Unifesspa.UniPlus.Contratos.UnitTests/Conferencias/`) faz fitness check byte-a-byte dos schemas com mesmo nome em baselines diferentes. Reavaliar quando ≥10 schemas compartilhados, terceiro módulo, ou portal `uniplus-developers` demandar multi-file.
- `contracts/postman/uniplus-api.postman_collection.json` com cenários smoke (criar processo seletivo com Idempotency-Key, listar com cursor, 406 vendor MIME inexistente, 422 validation).
- Newman em CI rodando contra a API em container — exige docker-compose com Postgres/Kafka/MinIO/Keycloak.

Esses follow-ups dependem dos controllers do módulo Seleção estarem expostos em ambiente integrado (HML/dev cluster) e da infra de CI suportar containers — fora do escopo de Milestone A.
