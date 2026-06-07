---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0078: Modelo de autorização PBAC + ABAC com ponto de decisão único

## Contexto e enunciado do problema

A autorização administrativa do `uniplus-api` foi modelada na [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) como um modelo por papéis fixos por Unidade: papéis globais (administrador / leitor) sobre um identificador de Unidade, com visibilidade por interseção de conjuntos de Unidades. Esse modelo decide o acesso a partir do **papel** do solicitante e do conjunto de Unidades que ele administra.

Um papel global não é suficiente para o domínio do Uni+. A permissão real de uma operação depende de muito mais que o papel:

- de qual módulo se trata (Seleção, Ingresso, Configuração, Organização);
- de qual unidade organizacional o solicitante atua;
- de qual processo seletivo, edital, etapa, banca, chamada ou matrícula está em jogo;
- de qual fase do fluxo está aberta e qual o estado do recurso;
- da classificação do dado retornado (público, interno, pessoal, sensível) e da base legal aplicável (LGPD);
- da preferência de identificação do candidato (nome social vs. nome civil);
- de o solicitante ter recebido uma concessão excepcional fora do seu grupo padrão;
- de o solicitante estar operando em sessão delegada (atuação institucional em nome de uma unidade).

Espalhar essas condições por verificações ad hoc nos controllers — por exemplo, `[Authorize(Policy = "...")]` com lógica de negócio embutida — produz autorização inconsistente, não auditável e difícil de evoluir. O problema é **decidir o acesso de forma uniforme, sensível ao contexto, com um único ponto de decisão e um sujeito explícito**.

## Drivers da decisão

- Autorização sensível a **atributos de contexto**, não apenas ao papel.
- **Um único ponto de decisão** — toda decisão de acesso passa pelo mesmo contrato, para ser auditável e testável.
- **Sujeito explícito** na assinatura da decisão — nunca inferido do ambiente (thread estático, header solto, estado global).
- LGPD-by-design: a decisão precisa poder considerar a classificação do dado e a base legal (detalhado em ADR própria desta frente).
- Concessões fora do papel padrão (excepcional, sessão delegada) avaliadas no servidor, sem depender do conteúdo do token.
- Substituir o modelo por papéis fixos por Unidade da ADR-0057, que não acompanha a complexidade real e tende à proliferação de papéis.

## Opções consideradas

- **A**: Manter o modelo por papéis fixos por Unidade (ADR-0057) — papéis globais por Unidade.
- **B**: **PBAC** (baseado em permissão — *o que* pode ser feito) **+ ABAC** (baseado em atributos de contexto — *em que circunstância*) com um **ponto de decisão único** e **sujeito explícito**.
- **C**: ReBAC / motor externo de políticas (Cedar, OPA, OpenFGA) como sidecar.

## Resultado da decisão

**Escolhida:** "B — PBAC + ABAC com ponto de decisão único", porque combina permissões granulares com atributos de contexto num contrato formal único, auditável e testável, sem o acoplamento operacional de um motor externo nesta fase.

### Ponto de decisão único

Toda decisão de acesso passa por um serviço único, `IAuthorizationDecisionService`, com a assinatura:

```csharp
Task<AuthorizationDecision> DecideAsync(
    AuthorizationSubject subject,        // quem — explícito
    PermissionRequirement requirement,   // o que — permissão + metadados
    ResourceContext resource,            // sobre qual recurso — atributos do alvo
    AuthorizationRequestContext request, // em que requisição — contexto da chamada
    CancellationToken ct);
```

`[Authorize(Policy = ...)]` fica restrito à autenticação básica; **regra de negócio de autorização não vive em atributo de controller** — vive no serviço de decisão.

### Sujeito explícito e concessões efetivas

O sujeito é passado explicitamente como `AuthorizationSubject`, carregando: a identidade (`UsuarioRef`, com emissor + *subject* do token — nunca um `Guid` representando o *subject*), os grupos do token, as unidades administradas (derivadas no servidor), e a lista de **concessões efetivas** (`EffectiveGrant`).

Uma concessão efetiva **unifica três fontes possíveis** — o token OIDC, o vínculo de grupo definido pela aplicação e a concessão excepcional — cada uma com **fonte rastreável**. A decisão pergunta apenas se o sujeito tem **ao menos uma** concessão aplicável ao recurso, considerando escopo e validade. Não exige que a permissão esteja no token *antes* de considerar uma concessão de outra fonte — uma concessão excepcional avaliada no servidor é uma concessão válida, apenas com origem distinta.

### Algoritmo de decisão (forma canônica)

1. Se a permissão exige autenticação multifator e o sujeito não a satisfez → negar (motivo registrado).
2. Se a permissão exige dupla aprovação e a requisição não a apresenta válida e não usada → negar.
3. Selecionar as concessões efetivas aplicáveis: mesma permissão, dentro da validade, com escopo que inclui o recurso (unidade, processo, chamada, tipo de recurso).
4. Se nenhuma concessão aplicável → negar (sem concessão aplicável).
5. Executar as verificações declarativas de contexto exigidas pela permissão; se alguma falha → negar com o motivo correspondente.
6. Se o dado é pessoal ou sensível → confirmar a base legal aplicável.
7. Conceder, registrando **qual concessão (e qual fonte) foi usada** para autorizar.

O resultado (`AuthorizationDecision`) carrega o veredito, o motivo de negativa quando aplicável (`DenyReason`, sem dados pessoais) e a concessão usada para fins de auditoria.

### Verificações declarativas de contexto

Cada permissão declara as verificações de contexto obrigatórias (por exemplo: fase aberta, estado do recurso compatível, escopo de auditoria ativo, base legal aplicável, MFA satisfeito, dupla aprovação registrada, equipe ativa no processo, atribuição documental ativa). O serviço de decisão orquestra a sequência declarada; **cada verificação é uma unidade testável** (`IPermissaoCheck`), não código espalhado.

### O que esta ADR não decide

Em respeito à regra "1 ADR = 1 decisão", esta ADR fixa **apenas** o modelo de decisão (PBAC + ABAC, ponto único, sujeito explícito, concessão efetiva). Decisões correlatas têm ADR própria desta frente de autorização: o catálogo declarativo de permissões e a geração de artefatos; a **hierarquia institucional sem herança de permissão** (ADR seguinte); a proteção de dado por permissão (LGPD-by-design); os grupos OIDC e seu provisionamento; a concessão excepcional e a sessão delegada; o cache e a revogação de concessões; a trilha de auditoria de autorização; e o banco isolado de autorização.

## Consequências

### Positivas

- Decisão de acesso **uniforme, auditável e testável** — um único ponto de decisão.
- Sensível a contexto **sem** espalhar lógica de autorização pelos controllers.
- Sujeito explícito elimina a dependência de estado ambiental e torna a decisão reproduzível em teste.
- Concessões fora do papel padrão (excepcional, sessão delegada) integradas à decisão sem inflar o token.
- A negativa explica-se por um motivo estável e sem dados pessoais, útil para diagnóstico e auditoria.

### Negativas

- Mais peças (tipos formais e verificações) do que um simples atributo de papel.
- Cada nova condição de contexto é uma verificação a implementar e testar.
- Curva de aprendizado para a equipe acostumada ao modelo por papéis fixos.

### Neutras

- Não fecha a porta para um motor externo de políticas (Cedar/OPA/OpenFGA) no futuro — fica como avaliação posterior, caso a complexidade das políticas justifique.

## Confirmação

- **Fitness test**: nenhuma rota de negócio usa `[Authorize(Policy = ...)]` para regra de autorização; toda permissão referenciada em uma decisão existe no catálogo de permissões; o sujeito é parâmetro explícito do serviço de decisão.
- **Golden authorization tests**: cobertura de conceder e de **negar** por escopo de unidade, fase fechada, estado de recurso incompatível e validade expirada — com prova negativa e contraprova positiva.

## Prós e contras das opções

### A — Modelo por papéis fixos por Unidade (manter ADR-0057)

- Bom, porque é simples e já modelado.
- Ruim, porque decide só por papel; condições reais (fase, estado, unidade, classificação do dado, base legal) não cabem no papel e acabam espalhadas pelos controllers; tende à proliferação de papéis.

### B — PBAC + ABAC com ponto de decisão único (escolhida)

- Bom, porque decide por permissão + atributos de contexto num contrato único, auditável e testável, com sujeito explícito.
- Ruim, porque exige tipos formais e verificações declaradas — mais peças que um atributo de papel.

### C — Motor externo de políticas (Cedar/OPA/OpenFGA)

- Bom, porque externaliza a linguagem de políticas e escala para regras complexas.
- Ruim, porque adiciona um componente de runtime e uma linguagem própria sem necessidade comprovada nesta fase; acoplamento operacional alto para o ganho atual.

## Mais informações

- **Supersede** a [ADR-0057](0057-areas-rbac-snapshot-historia-invariantes.md) (modelo por papéis fixos por Unidade): o eixo de decisão deixa de ser o papel por Unidade e passa a ser permissão + atributos de contexto, com escopo por Unidade.
- **Refina** a [ADR-0055](0055-organizacao-institucional-bounded-context.md) (Organização como bounded context): a Unidade permanece o escopo institucional, mas a autorização sobre ela passa a ser PBAC + ABAC, não papel global por Unidade.
- Relaciona-se com: [ADR-0033](0033-icurrentuser-abstraction-via-iusercontext.md) (`IUserContext`, estendido para expor o sujeito), [ADR-0010](0010-audience-unica-uniplus-em-tokens-oidc.md) (audience OIDC), [ADR-0034](0034-problemdetails-em-401-403-via-jwtbearer-events.md) (ProblemDetails em 401/403), [ADR-0019](0019-proibir-pii-em-path-segments-de-url.md) (sem dado pessoal em URL) e [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) (trilha forense isenta de soft-delete).
- A **hierarquia institucional sem herança de permissão** (unidades irmãs; auditoria explícita) é decidida na ADR seguinte desta frente; o catálogo de permissões, a proteção de dado por permissão e o provisionamento OIDC seguem em ADRs próprias.
