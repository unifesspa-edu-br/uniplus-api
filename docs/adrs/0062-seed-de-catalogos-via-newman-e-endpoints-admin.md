---
status: "accepted"
date: "2026-05-14"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
  - "DevOps (DIRSI)"
---

# ADR-0062: Seed de catálogos via Newman + endpoints admin (sem auto-seeder)

## Contexto e enunciado do problema

O escopo da Sprint 3 exige popular dados canônicos de referência em 3 bancos PostgreSQL isolados:

- `uniplus_organizacao`: 5 áreas (CEPS, CRCA, PROEG, PROGEP, PLATAFORMA).
- `uniplus_parametrizacao`: 12 modalidades, 12 necessidades especiais, 18 tipos de documento, 6 endereços.
- `uniplus_selecao`: 8 tipos de edital, 14 tipos de etapa, 9 critérios de desempate, 6 locais de prova, 14 obrigatoriedades legais.

Total ~100 linhas de dado canônico, originadas dos JSONs validados no protótipo HTML em `repositories/uniplus-prototipos-html/prototipo-cadastro-edital/data/seed-*.json`.

O design inicial (rejeitado — ver seção "Histórico") propunha um subsistema `IReferenceDataSeeder` + `EmbeddedJsonSeedSource` auto-invocado pelo `MigrationHostedService` na startup da API, com sentinel `CreatedBy = "seed:embedded@v1"` para distinguir linhas de seed das edições humanas. O sponsor rejeitou esse approach com duas clarificações:

1. **Auditoria precisa refletir o admin real.** Linhas registradas durante o deploy devem carregar o `sub` do JWT do plataforma-admin que disparou o registro, não um sentinel fabricado. Isso casa com a semântica existente do `AuditableInterceptor` (per `docs/guia-banco-de-dados.md` §5) e evita override especial de `IUserContext` no escopo do seed.
2. **Os mesmos endpoints admin** que a futura UI (em `uniplus-web`) consumirá devem ser a superfície canônica de escrita desde o dia 1. Bootstrap e operação contínua compartilham um único caminho de código; a diferença é apenas quem dispara cada chamada (DevOps via CLI no install → admin via formulário depois).

O sponsor também propôs **Newman** (Postman CLI) como ferramenta para o registro inicial: uma collection roda contra cada ambiente (dev, standalone, HML, PROD) usando OAuth2 client_credentials contra o Keycloak para obter token plataforma-admin, depois itera sobre os arquivos de seed fazendo POST de uma linha por request. Newman é padrão de indústria, integra em CI e serve como **documentação viva** do shape da API admin — a mesma collection é consumível pelos devs frontend ao construir os formulários do `uniplus-web`.

## Drivers da decisão

- **Auditoria honesta**: `CreatedBy` precisa refletir um usuário Keycloak real, não um sentinel.
- **Caminho de escrita único**: bootstrap e operação contínua não devem ter código separado.
- **Documentação viva**: a collection serve aos devs frontend quando forem construir o formulário.
- **CI-friendly**: Newman roda em qualquer runner que tenha Node.js.
- **Idempotência**: re-rodar Newman após deploy parcial não pode duplicar nem quebrar.
- **Multi-instituição**: outras IFES devem conseguir fork-ar os seeds (JSONs), não código C#.

## Opções consideradas

- **A**: `IReferenceDataSeeder` + `EmbeddedJsonSeedSource` auto-invocado na startup.
- **B**: `HasData` em `IEntityTypeConfiguration` do EF Core.
- **C**: Migration com `InsertData` raw SQL.
- **D**: Híbrido — `HasData` para áreas + JSON loader para os demais.
- **E**: Newman + endpoints admin (escolhida).

## Resultado da decisão

**Escolhida:** "E — bootstrap via Newman invocando os mesmos endpoints admin", porque é a única opção que preserva audit honesta (real JWT sub), unifica o caminho de escrita entre bootstrap e admin UI, e gera documentação viva consumível pelo time frontend — sem o custo de manter infra de seeder customizada.

### Layout do repositório

```text
repositories/uniplus-api/
├── seeds/                                              # apenas arquivos de dado (sem código)
│   ├── seed-areas-organizacionais.json
│   ├── seed-modalidades.json
│   ├── seed-necessidades-especiais.json
│   ├── seed-tipos-documento.json
│   ├── seed-enderecos.json
│   ├── seed-tipos-edital.json
│   ├── seed-tipos-etapa.json
│   ├── seed-criterios-desempate.json
│   ├── seed-locais-prova.json
│   └── seed-obrigatoriedades-legais.json
└── tools/seeds/
    ├── seed-catalogos.postman_collection.json
    ├── envs/
    │   ├── dev.postman_environment.json
    │   ├── standalone.postman_environment.json
    │   └── hml.postman_environment.json
    ├── run.sh
    └── README.md
```

Cada `seeds/seed-*.json` é array JSON flat de linhas (sem envelope), no shape que o endpoint admin correspondente espera no body. Chaves JSON em camelCase (convenção HTTP); colunas de banco em snake_case (per [ADR-0054](0054-naming-convention-e-strategy-migrations.md)) — `System.Text.Json` `PropertyNamingPolicy.CamelCase` na API + EFCore.NamingConventions na camada de banco.

### Estrutura da collection

A collection Postman tem:

- **Pre-request script no nível collection** — obtém token plataforma-admin via OAuth2 `client_credentials` no Keycloak, cacheia em `{{access_token}}` com TTL, refresca na expiração.
- **10 folders, um por catálogo** — cada um itera sobre o `seeds/seed-*.json` correspondente via `--iteration-data` do Newman.
- **Configuração por request**:
  - `Authorization: Bearer {{access_token}}`
  - `Accept: application/vnd.uniplus.{recurso}.v1+json` (vendor MIME per [ADR-0028](0028-versionamento-per-resource-content-negotiation.md))
  - `Content-Type: application/vnd.uniplus.{recurso}.v1+json`
  - `Idempotency-Key: {recurso}-{{codigo}}` (determinístico — re-run é idempotente per [ADR-0027](0027-idempotency-key-store.md))
  - Body: uma linha do arquivo de seed.
- **Teste por request** — assert response `201` (primeira execução) ou `200` (re-run via cache de idempotência); falha caso contrário.

### Execução

Runbook DevOps em standalone/HML/PROD inclui um passo "post-deploy bootstrap":

```bash
# Standalone
ENV=standalone bash tools/seeds/run.sh

# Catálogo específico (ex.: depois de adicionar nova área)
ENV=standalone CATALOG=AreasOrganizacionais bash tools/seeds/run.sh
```

`run.sh` invoca `newman run` com env file e flags de folder/iteration-data apropriados. Exit code propaga — pipeline CI/CD falha se o bootstrap falhar.

### Semântica de auditoria

Toda linha registrada via Newman carrega `CreatedBy` = JWT `sub` do principal do client_credentials (`uniplus-api-bootstrap-plataforma-admin` ou similar — provisionado no Keycloak como parte do setup standalone). Isso torna a trilha explícita:

- Linhas com `CreatedBy = '<bootstrap-client-sub>'` foram registradas no deploy via Newman.
- Linhas com `CreatedBy = '<human-user-sub>'` foram registradas depois via UI admin do `uniplus-web` (post-frontend-ready).
- Ambos os caminhos passam pelo mesmo `AuditableInterceptor` populando a partir de `IUserContext.UserId` (per `docs/guia-banco-de-dados.md` §5, padrão opt-in). Sem sentinel. Sem override especial de `IUserContext`.

Precondição: entidades que participam desse fluxo **devem implementar `IAuditableEntity`** explicitamente (per diretriz do sponsor sobre auditoria opt-in entidade-a-entidade). As 10 entidades de catálogo desta demanda implementam a interface.

### Transição para o frontend

Quando `uniplus-web` entregar os formulários admin (PRD separado, pós-Sprint 3):

1. Admins autenticam pelo Keycloak no navegador.
2. Formulários fazem POST nos mesmos `/api/admin/{recurso}`.
3. `CreatedBy` é populado com o `sub` do admin humano (não o sub do client bootstrap).
4. A collection Newman permanece como:
   - **Ferramenta de bootstrap inicial** para novos deploys.
   - **Documentação viva** do shape da API admin para devs frontend.
   - **Fixture de smoke test** em integração CI.

### Invariante de roster fechado do AreaOrganizacional

Per [ADR-0055](0055-organizacao-institucional-bounded-context.md), o roster de `AreaOrganizacional` é fechado — adicionar nova área exige nova ADR. O invariante é enforce-ado por **fitness test** (xUnit + ArchUnitNET) que lê `seeds/seed-areas-organizacionais.json` em build time e confirma que cada linha tem `adrReferenceCode` apontando para um arquivo em `docs/adrs/`. Roda em CI a cada PR. Se uma área for adicionada ao seed sem ADR correspondente, o build quebra.

Esse fitness test é o **único ponto** do código que toca o JSON do seed em compile/test time. Runtime nunca lê — Newman é o único consumer em runtime.

### Integração com fixture de teste

Testes de integração que precisam de catálogo populado (ex.: testes de endpoint de wizard que consomem `IModalidadeReader` via DI cross-módulo) têm duas opções:

- **Opção A (preferida para V1)**: fixture chama `newman run` contra a instância de teste da API antes da suíte rodar. Adiciona dependência Newman no ambiente CI (já presente via npm).
- **Opção B (alternativa)**: fixture lê os JSONs direto, deserializa e faz POST via `HttpClient` do `WebApplicationFactory` — replica a lógica do Newman em C#. Evita dependência mas duplica o shape da request.

Decisão por classe de teste durante a implementação. Call sites mais simples preferem A; cenários de alta cobertura podem preferir B para controle.

## Consequências

### Positivas

- **Auditoria é honesta.** Cada linha carrega um Keycloak subject real em `CreatedBy`, sem sentinel sintético.
- **Caminho único de escrita.** Bootstrap e operação contínua batem nos mesmos endpoints — discrepâncias impossíveis.
- **Documentação viva.** A collection Postman é em si um registro de como a API admin funciona; devs frontend consomem como referência ao construir formulário.
- **CI-friendly.** Newman roda em qualquer runner com Node.js. JSON da collection é revisável em PR.
- **Idempotente.** `Idempotency-Key` determinístico (`{recurso}-{{codigo}}`) garante que re-rodar Newman é seguro.
- **Fork multi-instituição.** Cada IFES adotante mantém seu próprio `seeds/` e env files — fork dos JSONs, não do C#.
- **Sem infra especial.** Sem `IReferenceDataSeeder` / `ISeedDataSource` / extensão do `MigrationHostedService`. Elimina uma classe de bugs (seeder falha no meio da startup, deixa estado parcial, bloqueia readiness da API).

### Negativas

- **Passo manual no deploy.** Newman precisa ser invocado depois da API subir — deployment fresco não está imediatamente funcional. Mitigado por inclusão explícita no runbook de standalone/HML/PROD + smoke step no CI.
- **Dependência externa em Newman.** Adiciona Node.js à toolchain de deploy. Já presente na maioria dos runners CI; preocupação menor.
- **Testes de integração precisam de bootstrap.** Tests que requerem catálogo populado precisam invocar Newman (ou replicar via `HttpClient`). Setup mais lento que seed em memória, mas mais realista.
- **JSONs não são embedded resources.** Arquivo faltando/errado só é detectado em runtime (Newman falha) ou no fitness test (para áreas). Mitigado por `newman --dry-run` no CI.

### Neutras

- A collection Postman fica em `tools/seeds/seed-catalogos.postman_collection.json` — fonte única.

## Confirmação

- **Risco**: passo bootstrap esquecido no deploy.
  **Mitigação**: runbook deploy explícito; smoke standalone inclui `GET /api/areas-organizacionais` retornando as 5 entradas esperadas.
- **Risco**: drift de versão do Newman.
  **Mitigação**: pinning de `newman` em `package.json`; CI usa versão pinada.
- **Risco**: credenciais Keycloak rotacionadas.
  **Mitigação**: env files referenciam segredo via env var, resolvido do Vault per pattern ESO 5 do `uniplus-infra`.
- **Risco**: JSON do seed diverge do schema da entidade.
  **Mitigação**: body validado pela API em runtime (422 ProblemDetails em mismatch); `newman --dry-run` no CI pega collection JSON malformada; fitness test valida `adrReferenceCode` em áreas.

## Histórico

**2026-05-13 (inicial):** Decidido `IReferenceDataSeeder` + `EmbeddedJsonSeedSource` com sentinel `"seed:embedded@v1"`.

**2026-05-14 (revisado):** Diretriz do sponsor substitui o auto-seeder pelo bootstrap via Newman. Auditoria captura usuário real; formulários admin (pós-frontend-ready) usam o mesmo caminho de escrita. ADR reescrita; [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) §"Implementation Notes" atualizada para remover referências ao seeder.

## Prós e contras das opções

### A — `IReferenceDataSeeder` + `EmbeddedJsonSeedSource` (original, rejeitada)

- **Prós**: totalmente automático — DB fresco sempre tem catálogos após API subir. Sem dependência externa de CLI.
- **Contras**: auditoria fabricada — sentinel `"seed:embedded@v1"` é ator sintético que não existe como subject Keycloak real. Viola "audit reflete user real". Override especial de `IUserContext` durante scope do seed adiciona complexidade. Diverge do fluxo de endpoint admin estabelecido — bootstrap e operação contínua são dois code paths distintos com invariantes sutilmente diferentes. Quando UI admin entrar em `uniplus-web`, precisaria coexistir com linhas que o seeder criou com sentinel — duas "famílias" de linha de catálogo na mesma tabela.
- **Por que rejeitada**: diretriz sponsor (2026-05-14): "remover os seeds e termos os arquivos json para fazer as requests e cadastrar — assim fica registrado o user real com base no token". Audit honesty e write path único superam a conveniência do auto-seed.

### B — `HasData` em EF Configuration

- **Prós**: mecanismo nativo do EF. Idempotente por construção.
- **Contras**: `EntityBase.Id = Guid.CreateVersion7()` é não-determinístico — `HasData` exige GUID compile-time constant. `HasData` bypassa o `AuditableInterceptor` (grava via SQL de migration), então `CreatedBy` fica null ou hardcoded — mesma fabricação de auditoria do alternativa A. Não suporta `Predicado` polimórfico do `ObrigatoriedadeLegal` limpo (drift no model snapshot). Toda correção no seed gera migration destrutiva.
- **Por que rejeitada**: mesma raiz audit + polimorfismo.

### C — Migration com `InsertData` raw SQL

- **Prós**: idempotente re-run-safe; isolada de migrations de schema.
- **Contras**: campos de audit hardcoded (`'system-seed'` ou null) — mesma fabricação. Correções no seed exigem novas migrations entulhando histórico. JSON polimórfico (`Predicado`) em arquivo de migration é hell de escape. Fork multi-instituição exige fork de migrations.
- **Por que rejeitada**: semântica de auditoria. Migrations são para schema, não para seed.

### D — Híbrido (`HasData` para áreas + JSON loader para os demais)

- **Prós**: estrutural ganha o `HasData`; resto fica dinâmico.
- **Por que rejeitada**: dois mecanismos para manter. Áreas via `HasData` ainda batem nos problemas de Guid dinâmico + bypass do interceptor + sentinel. Uniformidade vence.

### E — Newman + endpoints admin (escolhida)

- **Prós**: discussão acima.
- **Contras**: discussão acima.

## Mais informações

- [ADR-0023](0023-problemdetails-rfc-9457.md) — ProblemDetails RFC 9457 (Newman tests checam contra).
- [ADR-0027](0027-idempotency-key-store.md) — Idempotency-Key store (semântica de replay).
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — Vendor MIME per resource (Accept/Content-Type por linha).
- [ADR-0054](0054-naming-convention-e-strategy-migrations.md) — Naming snake_case + migrations.
- `docs/guia-banco-de-dados.md` §5 — Pattern opt-in do `IAuditableEntity`.
- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — Invariante de roster fechado para AreaOrganizacional.
- [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md) — Módulo Parametrizacao e read-side carve-out (referências ao seeder removidas).
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — Pattern de snapshot-copy cross-módulo (independente deste seed).
- Documentação Newman — <https://learning.postman.com/docs/running-collections/using-newman-cli/command-line-integration-with-newman/>.
- Diretrizes do sponsor (2026-05-14): "remover os seeds e termos os arquivos json para fazer as requests"; "quando a interface gráfica tiver pronta aí pode cadastrar pelo formulario"; "podemos usar newman para fazer isso para nós".
