---
status: "accepted"
date: "2026-05-03"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0027: `Idempotency-Key` opt-in com store em PostgreSQL adjacente ao outbox

## Contexto e enunciado do problema

Comandos `POST` e `PATCH` da `uniplus-api` precisam tolerar retry de cliente sem produzir efeito colateral duplicado. Cenários reais que motivam essa decisão:

- Candidato envia `POST /inscricoes` no portal; rede cai antes da resposta chegar; cliente retry — sem idempotency, duas inscrições seriam criadas, violando RN01 ("um CPF, uma inscrição ativa por processo seletivo") apenas no banco, mas com side effects (email de confirmação, evento Kafka, gravação outbox) já em curso.
- Operador administrativo dispara `POST /editais/{id}/publicar`; timeout de proxy retorna 504 ao cliente; operador retry — sem idempotency, evento `EditalPublicado` é despachado duas vezes, drenando notificações duplicadas.
- Aplicação cliente integrada (ex.: integração futura com SIGAA) replays mensagens em janela de recovery — qualquer comando crítico (recurso, matrícula, recálculo) pode ser entregue múltiplas vezes.

Constraints de unicidade no domínio cobrem **parte** do problema (não permitem duas inscrições ativas para o mesmo CPF), mas não cobrem side effects fora do banco (eventos, emails, integrações), e não distinguem **retry legítimo** (mesmo payload, mesma intent) de **request lógica diferente** que por acaso colide com a unique key.

A IETF tem um draft em fase final (`draft-ietf-httpapi-idempotency-key-header`) padronizando o header `Idempotency-Key` que vários produtores de API (Stripe, Square, PayPal, GitHub) já implementam de forma compatível. A decisão aqui é (i) adotar essa convenção, (ii) escolher onde persistir o cache de respostas idempotentes, (iii) definir granularidade (header opt-in vs sempre obrigatório) e (iv) escolher o tratamento de violações.

## Drivers da decisão

- **Conformidade com regras de negócio críticas.** RN01 exige unicidade real, e processos como recurso administrativo, publicação de edital e matrícula não toleram duplicação silenciosa. Idempotency é a primeira linha de defesa antes de constraints no domínio.
- **Side effects fora do banco.** Outbox (ADR-0004) garante eventos no `IEnvelopeTransaction`, mas não impede que duas execuções do mesmo handler enfileirem dois eventos. Idempotency em camada anterior elimina isso.
- **Conformidade com padrão emergente.** `draft-ietf-httpapi-idempotency-key-header-07` é o caminho que clientes e bibliotecas vão consumir nos próximos anos; alinhamento agora evita migração depois.
- **Atomicidade entre commit do agregado e gravação do cache.** Cache em Redis cria janela onde o agregado foi commitado mas o cache não chegou (ou vice-versa). Cache no mesmo PostgreSQL onde o outbox vive permite gravação na mesma `IEnvelopeTransaction` ([ADR-0004](0004-outbox-transacional-via-wolverine.md)).
- **Auditabilidade.** Cache em Postgres é trivial de inspecionar e exportar para auditoria; cache em Redis exige tooling adicional.
- **LGPD baseline.** Cache contém payload de request e response — pode incluir dados pessoais. Precisa cifragem at-rest com chave gerenciada externamente ([umbrella ADR-0022](0022-contrato-rest-canonico-umbrella.md), princípio 3).

## Opções consideradas

- **A. `Idempotency-Key` opt-in via atributo `[RequiresIdempotencyKey]`**, store em PostgreSQL na mesma transação do agregado, conforme `draft-ietf-httpapi-idempotency-key-header-07`.
- **B. Mesma adoção do header, mas store em Redis** ([ADR-0008](0008-redis-como-cache-distribuido.md)).
- **C. Sem `Idempotency-Key`** — confiar em unique constraints no domínio.
- **D. Idempotency implícita via UPSERT** em todos os comandos — sem header.

## Resultado da decisão

**Escolhida:** "A — `Idempotency-Key` opt-in com store em PostgreSQL adjacente ao outbox", porque é a única opção que combina (i) conformidade com o draft IETF, (ii) atomicidade transacional entre commit do agregado e gravação do cache, (iii) cobertura de side effects fora do banco, e (iv) auditabilidade nativa.

### Endpoints sob `Idempotency-Key`

A obrigatoriedade do header é **opt-in por endpoint**. Endpoints elegíveis são marcados com o atributo `[RequiresIdempotencyKey]` na camada `API`. Critério para marcação: endpoint é POST ou PATCH **e** sua semântica natural não é idempotente (ou seja, repetir a mesma request causaria efeito acumulativo). Categorias iniciais que exigem o atributo:

- Inscrição em processo seletivo (`POST /inscricoes`).
- Cadastro/publicação de edital (`POST /editais`, `POST /editais/{id}/publicar`).
- Recurso administrativo (`POST /recursos`).
- Upload de documento comprobatório vinculado a cota.
- Matrícula no módulo Ingresso.

Endpoints `GET`, `PUT` puros (substituição completa, naturalmente idempotente), `DELETE` (idempotente por semântica) e `HEAD` **não** recebem o atributo. `PATCH` puramente idempotente (ex.: `PATCH /editais/{id}` que atualiza campos com last-write-wins) é avaliado caso a caso.

### Forma do header e contrato com o cliente

- **Header:** `Idempotency-Key: <opaque-string>`. Recomenda-se ULID ou GUID v7; a `uniplus-api` aceita qualquer string opaca de 1 a 255 caracteres ASCII imprimíveis (sem espaços, vírgulas ou ponto e vírgula, conforme draft IETF).
- **Header ausente em endpoint marcado** → 400 Bad Request com `code: uniplus.idempotency.key_ausente`. Cliente deve gerar key e tentar novamente.
- **Header presente em endpoint não marcado** → ignorado silenciosamente (compatível com Stripe). Não é erro porque clientes podem enviar a key uniformemente.
- **Header malformado** (caracteres proibidos, comprimento fora do range) → 400 com `code: uniplus.idempotency.key_malformada`.

### Lookup, replay e validação

Em cada request com `Idempotency-Key`, o middleware (camada API):

1. Calcula a chave de lookup composta: `(scope, endpoint, idempotency_key)`. `scope` carrega tenant futuro + identificador do principal autenticado (sub do JWT) para evitar colisão entre clientes diferentes que escolham a mesma key.
2. Calcula `body_hash` da request body via SHA-256 (estável por content-type + canonical JSON ordering quando aplicável).
3. Consulta o cache em PostgreSQL pela chave de lookup.

Resultados possíveis:

- **Cache miss** — primeira execução. Handler roda; ao final, response (status + headers relevantes + body) é gravada na mesma `IEnvelopeTransaction` ([ADR-0004](0004-outbox-transacional-via-wolverine.md)) que commita o agregado. TTL de 24 horas é aplicado.
- **Cache hit + body_hash bate** — replay legítimo. Resposta cacheada é retornada **verbatim** (mesmo status code, mesmo body, header `Idempotency-Replayed: true` adicional informativo). Handler **não** roda novamente.
- **Cache hit + body_hash diverge** — mesma key reusada com payload diferente. 422 Unprocessable Entity com `code: uniplus.idempotency.body_mismatch`. Sinaliza bug no cliente; não tenta inferir intent.
- **Cache expirado** — request tratada como nova; handler roda. TTL é responsabilidade do cliente (24h é o teto).

### Status codes em cache

Tanto **2xx** quanto **4xx** são cacheados. A motivação é a convenção Stripe: se 4xx não fosse cacheado, um atacante poderia retry com diferentes keys até encontrar uma sequência que muda o estado, ou esgotar quotas indiretamente. **5xx não é cacheado** — cliente deve poder retry após falha transitória do servidor.

### Cifragem at-rest

O payload do cache (request body + response body) pode conter PII (CPF, nome social, email do candidato). Cifragem AES-GCM com chave gerenciada externamente (HashiCorp Vault transit em produção; fixture local em CI), mesma infraestrutura usada pelo cursor de paginação ([ADR-0026](0026-paginacao-cursor-opaco-cifrado.md)). Chave nunca sai do gerenciador. Rotação de chave não invalida entradas existentes — TTL de 24 horas as expira naturalmente.

### Esta ADR não decide

- Schema exato da tabela `idempotency_cache` (índices, particionamento, política de cleanup) — tarefa de implementação.
- Algoritmo exato de canonicalização de JSON para `body_hash` — sugestão razoável é `RFC 8785 JSON Canonicalization Scheme`, decisão final na implementação.
- Política de cleanup de entradas expiradas (job periódico vs `pg_cron` vs partition drop) — implementação.
- Headers cacheados além de `Content-Type` e `Location` — caso a caso na implementação.

## Consequências

### Positivas

- **Retry seguro.** Clientes (frontend, integradores externos) podem retry com confiança em qualquer cenário de timeout/network/proxy 5xx, sem risco de duplicação semântica.
- **Conformidade com RN01.** Inscrição duplicada por retry deixa de ser vetor; constraint de unicidade no domínio passa a ser última linha de defesa, não primeira.
- **Atomicidade alcançada parcialmente** (revisado pós-implementação — ver "Negativas"). A intenção original de uma transação única foi reduzida a três transações separadas; o ganho efetivo é eliminação de janela de inconsistência ao 5xx (reservation deletada) e proteção contra duplicação concorrente via UNIQUE index. Caminho de retry permanece seguro embora não atomic-ideal.
- **Auditabilidade.** Cache em Postgres é inspecionável via SQL para suporte e auditoria sem tooling extra.
- **Convergência com mercado.** Comportamento idêntico a Stripe/Square/PayPal facilita integração de clientes que já implementam o padrão.

### Negativas

- **Atomicidade parcial (ajuste pós-implementação, story #286).** A promessa original "commit do agregado + cache na mesma `IEnvelopeTransaction`" não foi alcançada. A implementação usa três transações separadas: `TryReserve` (insert da reservation), o handler do agregado (commit via `IEnvelopeTransaction` do Wolverine), e `Complete` (update da reservation com response cifrada). Integrar `ResourceFilter` ao pipeline da `IEnvelopeTransaction` exige extensão fora do escopo da story que entregou o middleware. Janela de inconsistência: se `Complete` falhar após o handler commitar (network blip, deploy, exceção tardia), a entry permanece em status `Processing` até o TTL (24h) — clientes que retry com a mesma key recebem 409 nesse intervalo. Mitigação parcial: 5xx/Canceled deleta a reservation imediatamente (DELETE atômico próprio) para liberar o cliente a retry após falha do handler. ADR será revisada quando a integração com `IEnvelopeTransaction` for implementada.
- **Carga adicional no Postgres.** Cada request idempotente faz uma leitura + uma escrita extra na mesma transação. Mitigação: índice em `(scope, endpoint, key)` e TTL agressivo (24h); volume do cache fica limitado por janela.
- **Complexidade de cleanup.** Entradas expiradas precisam de job periódico ou estratégia de particionamento. Sem cleanup, tabela cresce indefinidamente.
- **Cifragem adiciona latência.** Cifragem/decifragem por request idempotente acrescenta ~ms por chamada à infraestrutura de chave. Aceitável dado o ganho de segurança.
- **Carga cognitiva no consumidor.** Cliente precisa saber gerar key estável e quais endpoints exigem o header. Documentação no portal e biblioteca de cliente (futuro Angular client codegen) atenuam.

### Neutras

- A infraestrutura de chave (Vault) é a mesma usada por [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — não introduz dependência nova; só estende o uso.
- Endpoints existentes do `EditalController` precisam ser anotados quando refatorados como pilot — escopo do PR de migração.

## Confirmação

1. **Spectral rule no CI** — endpoints declarados no spec OpenAPI com método `POST`/`PATCH` que tenham `[RequiresIdempotencyKey]` declaram parâmetro de header `Idempotency-Key` como required. Endpoints sem o atributo não declaram esse header como required.
2. **Fitness test ArchUnit** — controllers cujo nome/anotação indique categoria de comando crítico (registrar, publicar, criar recurso, criar matrícula, fazer upload comprobatório) carregam `[RequiresIdempotencyKey]`. Listagem das categorias mantida em arquivo de configuração do teste; novas categorias entram via PR.
3. **Smoke E2E (Postman/Newman)** — cenários cobrem (a) primeira request retorna response normal; (b) replay com mesma key + mesmo body retorna response idêntica + `Idempotency-Replayed: true`; (c) replay com mesma key + body diferente retorna 422 com `uniplus.idempotency.body_mismatch`; (d) request com header malformado retorna 400 com `uniplus.idempotency.key_malformada`; (e) request sem header em endpoint marcado retorna 400 com `uniplus.idempotency.key_ausente`.
4. **Auditoria de cache** — relatório SQL periódico (suporte/segurança) verifica padrões de uso anormal: alta taxa de body_mismatch indica bug de cliente; alta taxa de cache hit em curto intervalo indica retry storm.

## Mais informações

- [ADR-0022](0022-contrato-rest-canonico-umbrella.md) — umbrella (princípio 3 LGPD aplicado ao payload do cache).
- [ADR-0023](0023-wire-formato-erro-rfc-9457.md) — wire format dos erros 400/422 desta ADR.
- [ADR-0004](0004-outbox-transacional-via-wolverine.md) — origem da mesma `IEnvelopeTransaction` onde o cache é gravado.
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) — usa a mesma infraestrutura de chave externa.
- [ADR-0007](0007-postgresql-18-como-banco-primario.md) — banco onde o cache vive.
- [ADR-0008](0008-redis-como-cache-distribuido.md) — opção B rejeitada referencia esta ADR.
- [draft-ietf-httpapi-idempotency-key-header-07](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header-07).
- [Stripe — Idempotent Requests](https://docs.stripe.com/api/idempotent_requests) — referência operacional para comportamento de cache de 4xx e replay verbatim.
- [RFC 8785 — JSON Canonicalization Scheme (JCS)](https://www.rfc-editor.org/rfc/rfc8785.html) — sugestão para canonicalização de body usada no `body_hash`.
