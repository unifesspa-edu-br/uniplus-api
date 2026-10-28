---
status: "accepted"
date: "2026-10-27"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0151: Catálogo de permissões em código, um enum por módulo

## Contexto e enunciado do problema

A [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) decide o acesso por permissão, num ponto de decisão único. A [ADR-0080](0080-catalogo-declarativo-de-permissoes-e-codegen.md) definiu o catálogo como um arquivo declarativo com gerador de artefatos. O PR #1258 reverteu essa escolha para código, porque o gerador custava 845 linhas para duas permissões e quebrou o `dotnet publish`; a troca nunca foi registrada (#1259), e as ADRs 0083 e 0088 ainda se apoiam em artefatos gerados que não existem.

O formato que ficou em código também não escala. Cada permissão eram dois membros ligados por convenção de nome (uma constante e um requisito); qualquer ponto que recebe `string` aceita uma permissão inexistente, e o erro aparece em execução como um 403 plausível; o catálogo numa classe única do `Shared.Authorization` precisaria conhecer as permissões de todos os módulos, invertendo a posse do monólito modular; e não há nome legível nem descrição para montar uma tela de concessão. As próximas frentes vão declarar dezenas de permissões, então o formato precisa estar certo antes.

## Drivers da decisão

- Menos complexidade: a permissão passa pela mesma revisão e compilação do código que a usa.
- Permissão inexistente não compila.
- Cada módulo é dono das suas permissões.
- Nome legível e descrição para o frontend e para a tela de concessão.
- Erro de catálogo derruba a subida, não a requisição.

## Opções consideradas

- **A**: manter o catálogo declarativo com gerador (ADR-0080).
- **B**: constantes e requisitos em uma classe única (formato atual).
- **C**: um `enum` de permissões por módulo, cada membro qualificado por um atributo, lido por um catálogo genérico.

## Resultado da decisão

**Escolhida:** "C — um enum por módulo com atributo", que supera a ADR-0080.

- **O catálogo fica em código**, sem arquivo declarativo nem gerador.
- **Um enum por módulo** (`PermissaoSelecao`, `PermissaoAutorizacao`…), marcado com `[ModuloDePermissoes("…")]`. Cada membro leva `[Permissao(código, nome, descrição, sensibilidade, …)]` com os metadados do `PermissionRequirement`: escopo de contexto obrigatório, verificações de contexto, multifator, dupla aprovação e base legal. O código `{módulo}:{recurso}:{ação}` é declarado à mão, nunca derivado do nome do membro, porque é o contrato.
- **`CatalogoDePermissoes<TPermissao>`** (`Shared.Authorization`) lê os atributos e valida cada um com `PermissionRequirement.From`. Como enum não implementa interface, a leitura de `Codigo`, `Nome`, `Requisito` e as demais é uma **extensão por enum**, e não uma genérica sobre `Enum`, que alcançaria todos os enums do sistema.
- **Registro por módulo**: `services.AddCatalogoDePermissoes<PermissaoSelecao>()`, no `Add{Modulo}Module`. O `CatalogoDaAplicacao` reúne os enums registrados.
- **Conferência na subida**, que derruba o boot com mensagem que nomeia o membro: atributo ausente; código repetido, inclusive entre enums; prefixo diferente do módulo do enum; nome ou descrição vazios ou nome repetido; campo de contexto que a decisão não sabe verificar; metadado recusado por `PermissionRequirement.From`.
- **`[ExigePermissao<TPermissao>(…)]`** nos endpoints: filtro de autorização, que roda antes do model binding, consulta o `IVerificadorDeAcesso` e preserva os três desfechos da borda (permitido; negado → 403; identidade incompleta → 401). O C# não admite parâmetro de atributo do tipo genérico, então o construtor recebe o valor como `object` e o confere ao construir; um teste de arquitetura constrói todos os usos do produto, de modo que o engano falha no build de testes e não na primeira requisição.
- **O valor numérico do enum não sai do processo.** Em registro de decisão, resposta da API e frontend circula sempre o código.
- **Dois endpoints de consulta**, em `/api/autorizacao/permissoes`: o catálogo agrupado por módulo e recurso, protegido por permissão própria (`autorizacao:permissoes:consultar-catalogo`), e as permissões efetivas do usuário logado, para o frontend montar menu e ações. A resposta das efetivas informa o que pode aparecer na interface; a decisão continua em cada requisição, porque a concessão pode depender do recurso. O frontend não conhece o catálogo: recebe da API.
- **Posse da permissão dos motivos de decisão de isenção.** O recurso, o controller e a rota (`/api/selecao/admin/motivos-decisao-isencao`) são do módulo Seleção, então a permissão de manutenção pertence ao `PermissaoSelecao`. O prefixo `configuracao:` que ela tinha era do nome do papel, e não do módulo dono, e o recurso se chamava "recursal" sem corresponder à rota; como ainda não há produção, o código passa a `selecao:motivos-decisao-isencao:manter`, e o papel do *client* no Keycloak é renomeado nos ambientes local e de homologação. A regra do prefixo vale sem exceção a partir daqui. A permissão de consultar a auditoria dos motivos, que existia sem uso, sai do catálogo: quem a declarar é a issue que criar a justificativa de inclusão em processo publicado. Configuração e Permissão são módulos distintos no frontend, com deploys separados; o catálogo não pertence a nenhum dos dois.

A migração dos `[Authorize(Roles = …)]` existentes é feita por módulo, em issues próprias. Um teste de arquitetura trava a linha de base: nenhum `[Authorize(Roles)]` novo, e a lista só diminui.

## Consequências

### Positivas

- Permissão nova é um membro de enum com um atributo; nada mais a manter em paralelo.
- Permissão inexistente não compila; erro de catálogo derruba a subida com o nome do membro.
- A posse volta aos módulos, e o frontend e a tela de concessão têm nome e descrição.

### Negativas

- O atributo de endpoint não confere em compilação que o valor é do enum informado (CS0181); a conferência é por teste.
- Papéis do *client* no Keycloak precisam acompanhar os códigos: renomeação exige ajuste nos ambientes.

### Neutras

- A decisão de acesso, o `PermissionRequirement` e o `IAuthorizationDecisionService` não mudam.

## Confirmação

- Testes de unidade do catálogo para cada defeito listado; teste de integração do host com todos os enums registrados e os dois endpoints (401, 403, 200, lista vazia).
- Teste de arquitetura da linha de base de `[Authorize(Roles)]` e da construção de todos os `[ExigePermissao<…>]`.

## Mais informações

- Supera a [ADR-0080](0080-catalogo-declarativo-de-permissoes-e-codegen.md) e torna sem objeto a [ADR-0088](0088-versionamento-cross-repo-do-contrato-de-permissoes.md), porque não há artefato gerado para versionar entre repositórios.
- Ajusta a [ADR-0083](0083-grupos-oidc-governados-pela-aplicacao.md): o manifesto dos grupos deixa de ser gerado do catálogo.
- Ancora no modelo de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md); os media types seguem a [ADR-0028](0028-versionamento-per-resource-content-negotiation.md).
