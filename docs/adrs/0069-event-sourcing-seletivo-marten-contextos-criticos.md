---
status: "proposed"
date: "2026-05-24"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed: []
---

# ADR-0069: Event Sourcing seletivo com Marten em agregados críticos do Uni+

> **Aceita em princípio** pelo Tech Lead, **não binding** até o gate passar. Adoção
> desacoplada do pacote primário (configuração + inscrição seguem CRUD, embarcam sem
> Marten); piloto = homologação documental. O gate antes de virar binding combina spike
> de atomicidade *append (Marten) + outbox (Wolverine)*, estratégia LGPD/crypto-shredding
> e fitness test da fronteira EF Core × Marten. Expansão (classificação/recurso/resultado)
> só após o piloto. **Origem:** revisão da ADR interna Uni+ (não publicada) e deliberação
> multi-perspectiva de 2026-05-21.

## Contexto e enunciado do problema

O Uni+ gerencia o ciclo de vida de processos seletivos da Unifesspa:
inscrição, análise documental, homologação, pontuação, classificação,
recursos, resultado, convocação e matrícula. São decisões com impacto
institucional sobre candidatos, sujeitas a recurso administrativo, auditoria,
normas de políticas afirmativas, LGPD e eventual contestação judicial.

O sistema precisa responder, anos depois, perguntas como:

- quais fatos de negócio levaram a uma homologação, pontuação, classificação
  ou retificação de resultado;
- qual regra, versão, snapshot ou hash de configuração foi aplicado;
- quem causou a decisão e com qual justificativa;
- como reconstruir o estado de um agregado em uma data, versão ou etapa
  anterior.

O backbone atual cobre consistência técnica, mas não transforma todo histórico
funcional em fonte canônica de verdade:

- ADR-0003 fixa Wolverine como backbone CQRS in-process e deixa event sourcing
  deliberadamente fora da decisão até existir caso de uso concreto.
- ADR-0004 fixa outbox transacional via Wolverine + EF Core sobre PostgreSQL.
- ADR-0005 fixa cascading messages como drenagem canônica de domain events e
  mantém `PublishDomainEventsFromEntityFrameworkCore` desabilitado.
- ADR-0014 fixa Kafka como barramento assíncrono inter-módulos, não como
  Event Store de domínio.
- ADR-0007 fixa PostgreSQL como banco primário.

O modelo CRUD + soft delete + audit trail técnico registra estado atual e
mutações técnicas, mas não preserva por padrão a sequência semântica de fatos
de negócio que levou uma decisão crítica até aquele estado. Para alguns fluxos,
isso é insuficiente como fonte de verdade auditável.

Esta decisão não reescreve o backbone nem torna Event Sourcing padrão global.
Ela abre a exceção prevista pela ADR-0003 para agregados e fluxos críticos que
precisam de histórico funcional canônico, mantendo CRUD tradicional onde ele é
suficiente.

## Drivers da decisão

- Explicabilidade de decisões sujeitas a recurso, auditoria, mandado de
  segurança ou contestação administrativa.
- Reconstrução temporal de agregados críticos por data, versão ou sequência de
  eventos.
- Replay para recriar projeções e read models derivados.
- Preservação da versão de regra aplicada em cada decisão automatizada,
  alinhada ao congelamento de parâmetros por edital (RN08) e ao motor de
  classificação como serviço de domínio puro (ADR-0013).
- Ausência de novo componente operacional fora do PostgreSQL já adotado
  (ADR-0007).
- Coerência com a stack existente: Wolverine e Marten pertencem ao ecossistema
  JasperFx, com integração nativa entre command handling, event store e outbox.
- Adoção seletiva e justificada por agregado, sem contaminar cadastros,
  catálogos, RBAC, parametrização, templates e demais CRUDs auxiliares.

## Opções consideradas

- **Manter apenas CRUD com audit trail técnico**.
- **Tratar Kafka como log/fonte de verdade dos eventos de domínio**.
- **Adotar Event Store dedicado (EventStoreDB/KurrentDB)**.
- **Adotar Event Sourcing seletivo com Marten sobre o PostgreSQL existente**.

## Resultado da decisão

**Escolhida:** Event Sourcing seletivo com **Marten sobre PostgreSQL** como
Event Store canônico para agregados e fluxos críticos, porque entrega histórico
funcional auditável e reconstrução temporal sem introduzir componente
operacional novo, reaproveitando o PostgreSQL (ADR-0007) e o ecossistema
JasperFx já presente pelo Wolverine (ADR-0003).

A decisão é **aceita em princípio** e **desacoplada do pacote primário**:
configuração e inscrição seguem CRUD e embarcam sem Marten. O ES só vira binding
após um gate de viabilidade (ver Confirmação) e é validado por um único piloto —
homologação documental — antes de expandir. Mantém-se:

- Wolverine como backbone de comandos, handlers e mensageria;
- EF Core como persistência dos contextos CRUD;
- Kafka como barramento de integração e fan-out inter-módulos;
- PostgreSQL como único componente de persistência operacional.

### Escopo de adoção

A adoção é decisão explícita por agregado, nunca default. Um agregado pode ser
event-sourced quando pelo menos uma condição forte vale:

1. a sequência histórica importa tanto quanto o estado atual;
2. a decisão pode ser objeto de recurso, auditoria, contestação administrativa
   ou judicial;
3. há necessidade real de reconstrução temporal por data, versão ou etapa;
4. projeções precisam ser recriáveis por replay;
5. o evento precisa carregar ou referenciar a regra, versão, snapshot ou hash
   que produziu a decisão;
6. correções posteriores precisam aparecer como novos fatos, não como
   alteração destrutiva do passado.

Caso contrário, o agregado permanece CRUD tradicional com EF Core, soft delete,
audit trail e, quando aplicável, entidades forenses append-only (ADR-0063).

### Fluxo-alvo

```text
Command HTTP/API
  -> ICommandBus / Wolverine
  -> Handler de aplicação
  -> Marten Event Store (append no stream do agregado)
  -> Projeções/read models derivados
  -> Evento de integração explícito
  -> Wolverine outbox
  -> Kafka
```

O Event Store guarda fatos de domínio canônicos. Kafka recebe eventos de
integração, versionados e compatíveis com os contratos públicos do sistema.
Esses dois modelos podem ter nomes, payloads e políticas de evolução
diferentes.

### Relação com ADR-0003

Esta ADR é uma emenda controlada à ADR-0003. A ADR-0003 adotou Wolverine como
backbone CQRS e deixou capacidades avançadas, incluindo event sourcing, fora da
decisão inicial. A ADR-0069 ativa essa possibilidade apenas para agregados
críticos com justificativa funcional explícita.

As abstrações `ICommandBus` e `IQueryBus` continuam sendo a entrada da camada
Application. Código de domínio e aplicação não deve passar a depender de
`Wolverine.*` diretamente.

### Relação com ADR-0004/0005

Nos agregados event-sourced, a escrita do agregado deixa de ser `DbContext +
SaveChanges` e passa a ser o *append* de eventos no stream Marten. O caminho de
outbox deve usar a integração Marten/Wolverine para persistir os envelopes de
mensageria na mesma unidade transacional do append, ou outro mecanismo validado
por spike que prove atomicidade equivalente.

A invariante da ADR-0005 continua valendo: `PublishDomainEventsFromEntityFrameworkCore`
permanece desabilitado. Não há scraper EF para contextos event-sourced.

Contextos CRUD permanecem como hoje: EF Core + cascading messages explícitas via
retorno do handler + Wolverine outbox.

### Relação com ADR-0014

Kafka é barramento de integração, não fonte canônica de domínio. É proibido
modelar `Command -> Kafka -> reconstrução por consumidores` como fonte de
verdade de agregados. Replay de estado canônico acontece a partir do Marten
Event Store.

### Relação com ADR-0013 e RN08

Eventos de cálculo, homologação, pontuação, classificação, desempate,
recurso, publicação ou republicação de resultado devem carregar ou referenciar
a versão da regra aplicada. Quando a regra for content-addressable, o evento
registra o hash/snapshot canônico usado na decisão.

A pergunta "com qual regra este resultado foi calculado?" deve ser respondível
pelo próprio histórico canônico do agregado, sem depender de estado atual de
tabelas auxiliares.

### Regras de governança

1. Eventos representam fatos de negócio em linguagem de domínio:
   `InscricaoHomologada`, `PontuacaoCalculada`, `RecursoDeferido`,
   `ResultadoRepublicado`. Eventos genéricos como `EntityUpdated`,
   `StatusChanged` ou `RegistroAlterado` são proibidos em streams críticos.
2. Streams pertencem a agregados com fronteira transacional enxuta, por
   exemplo `inscricao-{id}`, `recurso-{id}` ou
   `resultado-{processoSeletivoId}`. Streams gigantes são proibidos.
3. Eventos Marten são a fonte de verdade do histórico. Projeções, read models e
   tabelas de consulta são derivados e recriáveis.
4. Eventos Kafka são contratos de integração. Eles podem ser derivados dos
   eventos canônicos, mas não substituem o Event Store.
5. Correções são novos fatos (`PontuacaoRetificada`,
   `HomologacaoReavaliada`, `ResultadoRepublicado`), nunca edição destrutiva
   de evento passado.
6. Snapshots Marten são otimização para leitura de streams longos. Eles não
   substituem os eventos como fonte de verdade.
7. Versionamento de eventos é política permanente: mudanças incompatíveis
   exigem novo tipo/versão de evento, upcaster ou plano de migração
   documentado.
8. Projeções síncronas só são permitidas quando fizerem parte da unidade
   transacional validada; projeções assíncronas devem assumir consistência
   eventual explicitamente.

### Metadados e payload

Eventos críticos devem separar metadados técnicos, metadados de auditoria e
payload de domínio.

Metadados técnicos recomendados:

- `event_id`;
- `stream_id`;
- `stream_version`;
- `timestamp_utc`;
- `correlation_id`;
- `causation_id`.

Metadados ou payload de auditoria, conforme o evento:

- `actor_user_id` ou identificador equivalente do principal autenticado;
- papéis ou escopos relevantes no momento da decisão, quando necessários para
  auditoria;
- `edital_id`;
- `processo_seletivo_id`;
- motivo/justificativa administrativa;
- hash ou versão de regra aplicada;
- identificador de snapshot de edital, configuração, governança ou regra.

`TraceId` é observabilidade, não verdade de domínio. Pode ser propagado em
headers/logs/traces, mas não deve ser obrigatório como parte canônica do evento
armazenado, porque amostragem e retenção de tracing têm ciclo de vida diferente
do histórico jurídico.

### LGPD e retenção

O Event Store tende a ser append-only; portanto, o payload dos eventos exige
restrição mais forte que um CRUD mutável.

Regras mínimas:

- não armazenar documentos, anexos, laudos ou imagens no evento;
- não duplicar PII sensível quando referência indireta for suficiente;
- usar identificadores estáveis e snapshots mínimos para auditoria;
- mascarar, pseudonimizar ou cifrar campos sensíveis quando o dado for
  indispensável;
- documentar política de retenção compatível com processo seletivo, LGPD e
  normas internas;
- bloquear `UPDATE`/`DELETE` operacional nas tabelas de evento em produção por
  role, trigger ou política equivalente, registrando tentativa anômala como
  incidente.

### Contextos e agregados candidatos

Candidatos iniciais:

- homologação documental de inscrição;
- julgamento de recurso;
- cálculo de pontuação;
- classificação e desempate;
- publicação, retificação e republicação de resultado;
- convocação e matrícula quando houver impacto jurídico ou reclassificação.

Itens que devem permanecer CRUD em V1:

- cadastros auxiliares;
- RBAC e permissões;
- parametrização e catálogos;
- templates e conteúdo administrativo;
- endpoints técnicos e smoke tests;
- read models e projeções derivadas.

### Piloto recomendado

Começar por **homologação documental de inscrição**.

Motivos:

- tem valor de auditoria claro;
- é mais linear que recurso/resultado;
- valida eventos de fato de negócio, metadados de ator, justificativa,
  projeção, replay e integração com outbox;
- evita começar pelo fluxo mais complexo do sistema, como classificação ou
  republicação de resultado.

O piloto deve produzir pelo menos:

- stream canônico de uma inscrição homologada/rejeitada/reavaliada;
- projeção de consulta reconstruível por replay;
- evento de integração separado, se houver consumidor real;
- testes Given/When/Then do agregado;
- teste de replay;
- teste de atomicidade append + outbox;
- decisão documentada de schema/provisionamento Marten.

Expansão para **recurso/resultado** só deve ocorrer depois que o piloto provar
as convenções de evento, metadata, versionamento, projeção, replay, observabilidade
e operação.

## Consequências

### Positivas

- Histórico funcional imutável para decisões críticas.
- Reconstrução temporal e replay de projeções.
- Explicabilidade forte de homologação, pontuação, classificação, recurso e
  resultado.
- Separação clara entre fonte de verdade, read models e eventos de integração.
- Reuso do PostgreSQL já adotado, sem Event Store dedicado.
- Alinhamento com a stack JasperFx já presente pelo Wolverine.

### Negativas

- Dois modelos de persistência no mesmo produto: EF Core para CRUD e Marten para
  agregados event-sourced.
- Maior complexidade conceitual e curva de aprendizado.
- Versionamento de eventos passa a ser disciplina permanente.
- Projeções assíncronas introduzem consistência eventual.
- Operação precisa entender schema Marten, replay, rebuild de projeções e
  crescimento das tabelas de eventos.
- Testes exigem raciocínio por sequência temporal, não apenas estado final.

### Neutras

- Handlers de comandos críticos passam a ser testados por Given eventos / When
  comando / Then eventos emitidos.
- Consultas operacionais devem ler projeções/read models, não reconstruir
  streams em tempo real para toda tela.
- Snapshots podem ser usados depois de evidência de streams longos, não como
  otimização antecipada.

## Confirmação

Esta ADR só vira **binding** depois que o gate de viabilidade passar. O gate é
composto por:

1. **Spike de atomicidade** provando que `append (Marten) + outbox (Wolverine)`
   commitam na mesma unidade transacional — incluindo falha controlada que prove
   que append sem envelope, ou envelope sem append, não fica marcado como
   sucesso.
2. **Estratégia LGPD/crypto-shredding** desenhada para PII em payload
   append-only.
3. **Fitness tests de fronteira** EF Core × Marten (ArchUnitNET, ADR-0012):
   - contextos CRUD não importam Marten;
   - agregados event-sourced não são persistidos via `DbContext` EF Core;
   - handlers de comando event-sourced não escrevem diretamente em tabelas de
     projeção derivadas;
   - Kafka não é usado como fonte canônica para reconstrução de agregados;
   - eventos críticos seguem convenção de nomes de fatos de negócio;
   - eventos críticos carregam metadados mínimos exigidos pelo tipo de decisão;
   - eventos de integração Kafka são produzidos por tradução explícita ou
     mecanismo documentado, não por exposição automática do evento canônico.

Testes obrigatórios do piloto (homologação documental):

1. Given/When/Then do agregado.
2. Replay do stream reconstruindo estado esperado.
3. Rebuild da projeção a partir do Event Store.
4. Atomicidade entre append no Marten e envelope no outbox.
5. Falha controlada provando que append sem envelope, ou envelope sem append,
   não fica marcado como sucesso.
6. Idempotência/retry do publish para Kafka, quando houver evento de integração.
7. Evolução de evento: pelo menos um teste de compatibilidade, upcaster ou regra
   de versionamento.
8. LGPD: teste/validação de que payloads críticos não carregam anexos ou PII
   excessiva.

Pendências a fechar antes de o piloto virar binding:

- escolher formalmente o agregado piloto (recomendado: homologação documental);
- definir pacote/versão de Marten e da integração Wolverine/Marten;
- definir schema PostgreSQL do Marten e estratégia de provisionamento (alinhado
  à ADR-0039 — provisioning de schema como responsabilidade do deploy);
- definir se o outbox será via integração Marten/Wolverine nativa ou outro
  mecanismo validado por spike;
- definir convenções de nomes de streams e de nomes/versões de eventos;
- definir metadados obrigatórios por tipo de decisão;
- definir política LGPD para payload de eventos;
- definir política de evolução/upcasting de eventos;
- definir política de projeções síncronas vs. assíncronas;
- definir observabilidade mínima: logs, traces, métricas de projeção, outbox,
  replay e falhas.

## Prós e contras das opções

### Manter apenas CRUD com audit trail técnico

- Bom, porque tem menor complexidade, modelo conhecido, queries diretas e baixo
  custo de onboarding.
- Ruim, porque o histórico funcional é limitado; reconstrução temporal é frágil;
  auditoria centrada em mudança técnica, não em decisão de domínio.

### Kafka como fonte de verdade

- Bom, porque é integração natural event-driven e permite replay operacional de
  mensagens dentro da janela de retenção.
- Ruim, porque Kafka é barramento, não Event Store de domínio; retenção,
  compactação, governança, versionamento e reconstrução por consumidor não
  atendem sozinhos à auditoria jurídica. Também conflita com a ADR-0014.

### Event Store dedicado

- Bom, porque oferece append-only e subscriptions de primeira classe.
- Ruim, porque é um novo componente operacional com backup, HA, observabilidade,
  segurança, deploy e capacitação próprios. Adiado até existir evidência de que
  Marten/PostgreSQL não atende escala, isolamento ou operação.

### Event Sourcing seletivo com Marten

- Bom, porque usa PostgreSQL já adotado; integra com Wolverine; suporta streams,
  metadata, projections e outbox; permite adoção seletiva.
- Ruim, porque adiciona Marten ao stack produtivo; exige disciplina de fronteira
  com EF Core, estratégia de schema/provisionamento e política de evolução de
  eventos.

## Mais informações

- [ADR-0003](0003-wolverine-como-backbone-cqrs.md): Wolverine como backbone CQRS.
- [ADR-0004](0004-outbox-transacional-via-wolverine.md): outbox transacional via Wolverine + EF Core.
- [ADR-0005](0005-cascading-messages-para-drenagem-de-domain-events.md): cascading messages como drenagem canônica de domain events.
- [ADR-0007](0007-postgresql-18-como-banco-primario.md): PostgreSQL como banco primário.
- [ADR-0012](0012-archunitnet-como-fitness-tests-arquiteturais.md): ArchUnitNET como fitness tests arquiteturais.
- [ADR-0013](0013-motor-de-classificacao-como-servicos-de-dominio-puros.md): motor de classificação como serviços de domínio puros.
- [ADR-0014](0014-kafka-como-bus-assincrono-inter-modulos.md): Kafka como bus assíncrono inter-módulos.
- [ADR-0039](0039-provisioning-schema-wolverine-via-deploy.md): provisioning de schema como responsabilidade do deploy.
- [ADR-0040](0040-helper-wolverine-outbox-cascading-canonico.md), [ADR-0041](0041-padrao-retorno-handlers-wolverine-cascading.md), [ADR-0044](0044-roteamento-domain-events-pg-queue-kafka-opcional.md): helper, retorno de handlers e roteamento atual do outbox.
- [ADR-0052](0052-rastreabilidade-cross-service-traceparent-service-name-enricher.md): rastreabilidade cross-service por `traceparent`, `CorrelationId` e middleware Wolverine.
- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md): entidades forenses append-only isentas de soft-delete.
- Marten Event Store: <https://martendb.io/events/>
- Wolverine + Marten: <https://wolverinefx.net/guide/durability/marten/>
- **Origem:** revisão da ADR interna Uni+ (não publicada) e deliberação multi-perspectiva de 2026-05-21 (Architect / Pragmatic Engineer / Devil's Advocate), com decisão do Tech Lead de aceitar o princípio gateado por spike.
