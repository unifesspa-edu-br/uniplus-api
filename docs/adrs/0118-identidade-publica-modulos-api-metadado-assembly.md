---
status: "proposed"
date: "2026-07-29"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Backend (CTIC)"
informed:
  - "Equipe Uni+"
---

# ADR-0118: Identidade pública de módulos de API via metadado de assembly

## Contexto e enunciado do problema

A [ADR-0064](0064-convencao-roteamento-path-based-com-prefixo-modulo.md)
estabelece que controllers são expostos sob `/api/{modulo}`. Após a mudança de
topologia registrada na
[ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md), os
módulos internos passaram a compartilhar o mesmo processo no Host, enquanto o
Portal permaneceu executável de forma autônoma. O mesmo nome público de módulo
também identifica o documento OpenAPI correspondente.

O Host já precisava associar cada controller ao seu módulo para preencher
`ApiExplorer.GroupName` e isolar os documentos OpenAPI. Essa associação era
inferida por um mapa de prefixos de namespace. Ao automatizar também o prefixo
das rotas por `IControllerModelConvention`, reutilizar `GroupName` como entrada
do roteamento criou uma dependência temporal entre duas conventions distintas.
No Portal standalone, que não registra a convention de agrupamento do Host,
`GroupName` permanecia nulo e `/api/portal/ping` deixava de existir.

Derivar o contrato público diretamente do namespace ou do nome do assembly
também é frágil: ambos são detalhes internos que podem mudar por reorganização
de código. Além disso, `OrganizacaoInstitucional` é publicado como
`organizacao`, demonstrando que não existe uma transformação textual geral
entre o nome técnico e o segmento público.

É necessário definir uma fonte única, explícita e verificável para a identidade
pública de cada módulo de API, compartilhada por roteamento e OpenAPI, sem
exigir atributo em cada controller nem manter um catálogo central de módulos na
infraestrutura compartilhada.

## Drivers da decisão

- Preservar os paths públicos definidos pela ADR-0064 tanto no Host quanto em
  aplicações standalone.
- Tratar o nome público do módulo como contrato explícito, não como derivação de
  namespace, nome de projeto ou nome de assembly.
- Separar roteamento de `ApiExplorer.GroupName` e da ordem de execução das
  conventions MVC.
- Declarar a identidade uma única vez por assembly de API, sem repetição por
  controller.
- Manter os componentes compartilhados abertos à extensão: um módulo novo não
  deve exigir alteração em um catálogo central.
- Falhar durante a inicialização quando um controller não possuir identidade de
  módulo, evitando exposição silenciosa fora de `/api/{modulo}`.
- Reutilizar a mesma identidade no prefixo HTTP e no nome do documento OpenAPI.

## Opções consideradas

- Derivar o módulo do namespace do controller por mapa compartilhado.
- Derivar o módulo do nome do assembly ou do projeto.
- Declarar o módulo por atributo em cada controller.
- Registrar um catálogo central `Assembly → módulo` no Host ou em
  `Infrastructure.Core`.
- Declarar a identidade por metadado no assembly de API (escolhida).

## Resultado da decisão

**Escolhida:** "declarar a identidade pública por metadado no assembly de API",
porque o assembly é a fronteira usada pelo ASP.NET Core para descoberta dos
controllers, permite uma única declaração verificável por módulo e desacopla o
contrato público de convenções internas de nomenclatura.

Cada projeto `Unifesspa.UniPlus.*.API` deve possuir um assembly marker que seja
dono do nome público canônico:

```csharp
using Unifesspa.UniPlus.Infrastructure.Core.Routing;

[assembly: ApiModule(
    global::Unifesspa.UniPlus.Portal.API.PortalApiAssemblyMarker.ModuleName)]

namespace Unifesspa.UniPlus.Portal.API;

public sealed class PortalApiAssemblyMarker
{
    public const string ModuleName = "portal";

    private PortalApiAssemblyMarker()
    {
    }
}
```

O atributo `ApiModuleAttribute`:

- tem `AttributeTargets.Assembly`;
- não permite múltiplas declarações no mesmo assembly;
- aceita somente segmentos iniciados por letra ASCII minúscula e compostos por
  letras ASCII minúsculas, dígitos ou hífen.

`ApiModuleMetadata.GetRequiredName(Assembly)` é o resolvedor compartilhado. A
ausência do atributo é erro de configuração e deve interromper a construção do
modelo MVC. Não existe fallback para namespace, nome do assembly ou
`ApiExplorer.GroupName`.

`ModuleRoutePrefixConvention` resolve o módulo diretamente do assembly do
controller e combina `api/{modulo}` com a rota relativa declarada pelo
controller. `ModuleApiGroupingConvention`, exclusiva do Host, usa o mesmo
resolvedor para preencher `ApiExplorer.GroupName`, preservando eventual
grouping explícito. O registro de OpenAPI de cada módulo utiliza o
`ModuleName` de seu próprio assembly marker.

Esta ADR emenda a ADR-0064 somente no mecanismo de composição das rotas:
controllers passam a declarar a rota relativa do recurso, enquanto a convention
injeta `api/{modulo}`. A forma pública `/api/{modulo}/{recurso}` e seus paths
existentes permanecem inalterados.

O código compartilhado contém apenas o atributo, o resolvedor e a convention.
Ele não mantém a lista dos módulos concretos. Adicionar um módulo exige declarar
sua identidade no próprio projeto de API e registrá-lo no composition root
correspondente.

Esta decisão se aplica a controllers MVC. Endpoints compartilhados registrados
como Minimal APIs, como autenticação, perfil e health checks, continuam seguindo
suas convenções específicas e não recebem automaticamente o prefixo de módulo.

## Consequências

### Positivas

- Host e Portal standalone calculam o mesmo path sem depender de uma convention
  exclusiva do Host.
- Namespace, nome do projeto e nome do assembly podem ser refatorados sem
  alterar involuntariamente o contrato HTTP.
- Routing e OpenAPI reutilizam uma identidade canônica por módulo.
- Controllers mantêm apenas rotas relativas de recurso e não repetem o nome do
  módulo.
- A infraestrutura compartilhada permanece agnóstica ao roster de módulos.
- Ausência de metadado falha no startup, em vez de produzir rota sem prefixo.
- A leitura do atributo ocorre na construção do modelo MVC, sem reflexão por
  requisição.

### Negativas

- Todo assembly que forneça controllers à aplicação deve declarar
  `ApiModuleAttribute`; uma biblioteca externa de controllers exige adaptação
  explícita ou emenda desta ADR.
- Alterar `ModuleName` é uma mudança de contrato que afeta paths e o nome do
  documento OpenAPI simultaneamente.
- O assembly marker passa a acumular também a responsabilidade de expor a
  identidade pública do módulo.

### Neutras

- O mecanismo usa reflexão para ler um atributo de assembly durante o startup.
  O custo não participa do processamento de requisições.
- A ADR-0064 continua sendo a fonte da forma pública
  `/api/{modulo}/{recurso}`; esta ADR define como `{modulo}` é declarado e
  resolvido.

## Confirmação

- `ApiModuleAttributeTests` valida o formato permitido e o fail-fast para
  assemblies sem metadado.
- `ApiModuleMetadataTests` inclui automaticamente os projetos
  `Unifesspa.UniPlus.*.API`, exige exatamente um atributo por assembly e nomes
  públicos sem colisão.
- O teste de integração do Portal verifica que `GET /api/portal/ping` responde
  e que `GET /ping` não é exposto.
- A issue
  [#835](https://github.com/unifesspa-edu-br/uniplus-api/issues/835) mantém a
  cobertura exaustiva para impedir que qualquer rota de controller escape do
  prefixo esperado no Host ou no Portal standalone.
- A CI executa os projetos `*.ArchTests`, `*.UnitTests` e
  `*.IntegrationTests`.

## Prós e contras das opções

### Namespace com mapa compartilhado

- Bom, porque não exige novo metadado nos assemblies.
- Bom, porque centraliza as exceções de nomes públicos.
- Ruim, porque transforma organização interna de código em contrato HTTP.
- Ruim, porque toda adição ou renomeação de módulo altera o mapa compartilhado.
- Ruim, porque um namespace desconhecido pode ser ignorado silenciosamente.

### Nome do assembly ou projeto

- Bom, porque a informação já existe e é facilmente inspecionável.
- Ruim, porque ainda exige regras de transformação e exceções como
  `OrganizacaoInstitucional → organizacao`.
- Ruim, porque acopla o contrato público ao naming técnico e dificulta
  refatorações.

### Atributo por controller

- Bom, porque torna a identidade explícita no ponto de uso.
- Ruim, porque repete o mesmo valor em todos os controllers e permite drift
  dentro do módulo.
- Ruim, porque mantém a possibilidade de esquecer o atributo em um controller
  novo.

### Catálogo central `Assembly → módulo`

- Bom, porque representa associações explicitamente e pode validar colisões no
  startup.
- Ruim, porque a infraestrutura compartilhada passa a conhecer todos os módulos
  concretos.
- Ruim, porque adicionar um módulo exige modificar um registro central, em vez
  de estender a solução pelo próprio módulo.

### Metadado por assembly

- Bom, porque a identidade fica explícita e próxima da fronteira que possui os
  controllers.
- Bom, porque existe uma declaração por módulo e nenhuma por controller.
- Bom, porque routing e OpenAPI compartilham o mesmo resolvedor.
- Ruim, porque introduz um atributo próprio e uma leitura por reflexão no
  startup.

## Mais informações

- [ADR-0030](0030-openapi-3-1-contract-first-microsoft-aspnetcore-openapi.md)
  — geração e isolamento dos documentos OpenAPI por módulo.
- [ADR-0036](0036-controllers-mvc-para-negocio-minimal-api-para-shared.md)
  — fronteira entre controllers MVC e Minimal APIs compartilhadas.
- [ADR-0064](0064-convencao-roteamento-path-based-com-prefixo-modulo.md)
  — contrato de paths com prefixo de módulo.
- [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md)
  — co-hosting dos módulos internos e Portal standalone.
- [PR #1006](https://github.com/unifesspa-edu-br/uniplus-api/pull/1006) —
  implementação da convention automática de prefixo.
- [Issue #835](https://github.com/unifesspa-edu-br/uniplus-api/issues/835) —
  cobertura exaustiva das rotas por módulo.
- [Microsoft — Work with the application model in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/application-model?view=aspnetcore-10.0).
- [Microsoft — Application Parts in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/advanced/app-parts?view=aspnetcore-10.0).
- [Microsoft — Assembly-level attributes in C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/global).
