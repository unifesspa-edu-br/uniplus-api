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

**2026-05-14 (revisado):** Diretriz do sponsor substitui o auto-seeder pelo bootstrap via Newman. Auditoria captura usuário real; formulários admin (pós-frontend-ready) usam o mesmo caminho de escrita. ADR reescrita; [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md) §"Implementation Notes" atualizada para remover referências ao seeder.

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

## Emenda 1 (2026-05-16) — vocabulário e URLs path-based

A diretriz sponsor reservou o termo "catálogo" para um futuro conceito de
domínio. O que esta ADR descreve são **entidades de parametrização** (per
ADR-0056). E, per ADR-0064, todos os endpoints admin seguem o padrão
path-based com prefixo de módulo. O conteúdo técnico (Newman, idempotência,
audit, fitness test de roster) permanece válido — apenas os nomes e
URLs mudam:

| Antigo | Novo |
|---|---|
| "10 catálogos" | "10 entidades de parametrização" |
| `tools/seeds/seed-catalogos.postman_collection.json` | `tools/seeds/seed-parametrizacao.postman_collection.json` |
| variável `${CATALOG}` no `run.sh` | `${ENTIDADE}` |
| Folder Postman "AreasOrganizacionais (catálogo)" | "AreasOrganizacionais" (sem qualificador) |

As URLs admin alvo dos POSTs do Newman seguem a ADR-0064:

| Recurso | URL admin |
|---|---|
| Modalidade | `POST /api/parametrizacao/admin/modalidades` |
| NecessidadeEspecial | `POST /api/parametrizacao/admin/necessidades-especiais` |
| TipoDocumento | `POST /api/parametrizacao/admin/tipos-documento` |
| Endereco | `POST /api/parametrizacao/admin/enderecos` |
| AreaOrganizacional | `POST /api/organizacao/admin/areas-organizacionais` |
| TipoEdital | `POST /api/selecao/admin/tipos-edital` |
| TipoEtapa | `POST /api/selecao/admin/tipos-etapa` |
| CriterioDesempate | `POST /api/selecao/admin/criterios-desempate` |
| LocalProva | `POST /api/selecao/admin/locais-prova` |
| ObrigatoriedadeLegal | `POST /api/selecao/admin/obrigatoriedades-legais` |

`docs/guia-banco-de-dados.md` e `seeds/README.md` (quando criado em #463)
devem refletir o novo vocabulário e os paths atualizados.

## Emenda 2 (2026-09-01) — critério que separa seed por migration de carga por Newman

A decisão original vale para **dado que a instituição administra**. Não vale para **vocabulário
normativo**, e a prática divergiu disso desde julho de 2026: `Modalidade`, `PrecedenciaFase`,
`FatoCandidato`, `FatoValorDominio`, `CategoriaDocumento` e `TipoDocumento` são materializados por
`HasData` em migration. A ADR seguia `accepted` dizendo o contrário, e quem lia a decisão e o
código encontrava duas respostas para a mesma pergunta.

### O critério

Diretriz do Tech Lead (2026-09-01), em duas metades:

1. **Newman/endpoint admin é para dado que exige registro de quem fez a operação.
   `HasData`/migration é para o que não precisa de blame.**
2. **Update é sempre via API** — alterar é sempre alguém decidindo mudar.

| Momento | Regime |
|---|---|
| criação de dado normativo | seed **ou** carga admin — indiferente |
| criação de dado operacional | endpoint admin, com autor |
| update de dado **administrável** | endpoint admin, com autor |
| evolução de **vocabulário sem CRUD** | migration de seed, revisada em PR |

O critério classifica pela **natureza do dado**, não pela consequência da ausência: não depende de
julgar o quanto uma falta dói.

### A quarta linha, e por que ela não é exceção ao critério

`FatoCandidato` e `FatoValorDominio` são **append-only e não têm CRUD**: o controller expõe apenas
`GET fatos-candidato` e `GET fatos-candidato/{codigo}`, e a [ADR-0116](0116-origem-ponto-resolucao-binding-fato-valor-dominio.md)
determina que reclassificar um fato se faz por migration de seed. Exigir "update via API" desses
catálogos mandaria usar um caminho que não existe.

A regra do update continua valendo pelo que ela protege: **alterar dado que alguém administra exige
saber quem alterou**. Nesses catálogos não há dado administrado — há vocabulário, e mudá-lo é
mudança de código. O blame não some: está no commit e no PR que alterou a lista, com revisão. O que
não se admite é migration que altere linha de cadastro **administrável**, porque ali existe um
operador cuja edição seria sobrescrita sem registro.

Distinção prática: se a tela permite editar aquele campo, a migration não pode tocá-lo.

### Por que isto não contradiz o driver de auditoria honesta

A decisão original rejeitou a opção A por **auditoria fabricada** — o sentinel
`"seed:embedded@v1"`, ator sintético sem correspondente no Keycloak. Esse fundamento continua
válido e esta emenda não o toca.

O que a análise de 2026-05-14 equiparou indevidamente foi `CreatedBy` **nulo** ao sentinel: a
opção B foi rejeitada porque *"`CreatedBy` fica null ou hardcoded — mesma fabricação de auditoria
da alternativa A"*. **Nulo e sentinel são opostos.** Sentinel inventa um autor que não existe;
nulo declara que não há autor a registrar. Para a Lei 12.711, atribuir a criação a um operador é
que seria a informação desonesta.

Logo: `created_by` nulo **é** auditoria honesta para dado normativo, e o driver original fica
preservado — não relaxado.

### Os demais contras da opção B foram resolvidos na prática

- **GUID não-determinístico:** os seeds em uso derivam id determinístico com prefixo próprio
  (`PrecedenciaFaseSeed`, `CategoriaDocumentoSeed`), o que também torna o `Down` seletivo.
- **"Toda correção no seed gera migration destrutiva":** resolvido pela segunda metade do critério.
  Seed usa `INSERT … ON CONFLICT DO NOTHING` e **nunca `UPDATE`** — o deploy acrescenta o que falta
  e não toca no que existe. Corrigir linha existente é ato administrativo, com responsável.
- **Polimorfismo do `Predicado`:** alcançava `ObrigatoriedadeLegal`, que permanece fora do grupo
  de seed.

### Classificação das entidades

| Regime | Entidades |
|---|---|
| Seed em migration | `Modalidade`, `FatoCandidato`, `FatoValorDominio`, `CategoriaDocumento`, `PrecedenciaFase`, `TipoDocumento`, `TipoEtapa`, `TipoProcesso`, `FaseCanonica` |
| Endpoint admin | `Campus`, `Curso`, `LocalOferta`, `OfertaCurso`, `CalendarioDiasUteis`, `TermoConsentimento`, `CondicaoAtendimentoEspecializado`, `CriterioDesempate`, `ObrigatoriedadeLegal` |

Das dez entidades da decisão original, **quatro não existem mais**: `NecessidadeEspecial`,
`Endereco`, `AreaOrganizacional` e `LocalProva` foram removidas ou substituídas na evolução do
modelo desde maio de 2026 — `CondicaoAtendimentoEspecializado` ocupa hoje o lugar da primeira, e
`TipoEdital` foi renomeado para `TipoProcesso`. Os arquivos de seed correspondentes, previstos no
layout original, seguem a mesma sorte das entidades.

`TipoEtapa` e `TipoProcesso` entram na primeira coluna por constatação, não por decisão nova: as
migrations `20260811221349_CriaCadastroTiposEtapa` e `20260810180213_TiposProcessoConfiguraveis` já
inserem os sete e os oito códigos legados, com autor nulo. Os dois vieram de enum promovido a
entidade — vocabulário, por definição. Mantê-los no bootstrap por Newman faria a collection, numa
base recém-migrada, tentar `POST` de códigos que já existem: conflito onde ela espera 200/201, e o
passo pós-deploy falha. Acrescentar código novo permanece administrativo.

(`TipoEdital` é o nome antigo de `TipoProcesso`; o arquivo de seed previsto na decisão original
guarda o nome anterior.)

O critério reclassifica exatamente o que a prática já havia movido — sinal de que descreve a razão
que os autores seguiram sem nomear.

A exceção é `FaseCanonica`, que a prática **não** havia movido: a classificação aqui é
prospectiva. A implementação, e com ela a emenda da
[ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md) — que hoje declara a fase
100% CRUD-administrada, sem seed —, vêm na mudança que a semeia. Até lá, base recém-migrada
continua sem as fases, que é justamente o problema que motivou este critério: as arestas de
`PrecedenciaFase` já nascem semeadas apontando para vértices que não existem.

### Efeito sobre os arquivos de seed previstos

`seeds/seed-modalidades.json`, `seeds/seed-tipos-documento.json`, `seeds/seed-tipos-etapa.json` e
`seeds/seed-tipos-edital.json` **saem do escopo do bootstrap por Newman**: os quatro são vocabulário
normativo, e `TipoDocumento`, `TipoEtapa` e `TipoProcesso` já são semeados por migration. Manter
qualquer um deles na collection produz conflito numa base recém-migrada, não idempotência. Os
demais arquivos da lista original permanecem no regime de endpoint admin.

### O que permanece válido

Registry de decisão sobre o caminho de escrita **para dado administrado**: os endpoints admin
seguem sendo a superfície canônica, a collection Newman segue como documentação viva do shape da
API, a semântica de auditoria com `sub` real do JWT vale para tudo que entra por ali, e a
precondição de `IAuditableEntity` continua. A emenda toca apenas a fronteira de qual entidade entra
por qual caminho.

### Consequência conhecida, e como o critério a evita

`HasData` em cadastro **também** administrável tem dois riscos, e a segunda metade do critério
elimina os dois por construção — **em catálogo administrável**, a migration de seed não emite
nenhum dos dois comandos:

- **`UpdateData`**, gerado ao editar um item da lista, sobrescreveria o que o administrador mudou
  naquela linha;
- **`DeleteData`**, gerado ao **remover** um item da lista, apagaria a linha **fisicamente** — sem
  passar pelo soft-delete auditado da API, que é como o cadastro registra remoção. Em catálogo
  administrável como `CategoriaDocumento` ou `TipoDocumento`, isso destrói dado do operador e a
  trilha junto.

Onde o EF geraria qualquer um deles, a migration é escrita à mão: `Sql` com `INSERT … ON CONFLICT
DO NOTHING` para acrescentar. **Corrigir e remover têm forma própria** — descrita adiante em
"Como se corrige um item semeado que nasceu errado" —, com o comando guardado pelas colunas de
auditoria e, quando o alvo é referenciado, pela ausência de referência viva. O que não existe é o
`UpdateData`/`DeleteData` **gerado pelo scaffold**, que não tem nenhuma dessas guardas.

### Como se corrige um item semeado que nasceu errado

Descartar o `UpdateData`/`DeleteData` gerado resolve o risco de sobrescrever o operador, mas
sozinho cria outro: a migration original continua inalterada, então **toda base criada depois ainda
recebe a linha obsoleta**. Corrigir só pela API conserta o ambiente onde alguém rodou a correção e
deixa o conteúdo do catálogo dependendo da idade do banco — e os arquivos de bootstrap desses
catálogos saíram do Newman, então não há passo pós-deploy que reponha.

A correção é **nova migration**, nunca edição da migration já aplicada, com o comando condicionado
a a linha continuar como o seed a criou:

```sql
UPDATE configuracao.<tabela> SET <coluna> = <valor novo>
 WHERE id = '<id determinístico do seed>'
   AND updated_at IS NULL
   AND created_by IS NULL;
```

As duas condições de auditoria são o que distingue este `UPDATE` do proibido: ele alcança a linha
que **ninguém tocou** — em base nova e em base antiga igualmente — e não alcança a que o operador
editou, que é a única que a regra protege. O `AuditableInterceptor` preenche as duas colunas em
qualquer escrita pela API, então a distinção é mecânica.

**Vale para atributo, não para o código.** O código é a identidade que as outras tabelas guardam —
como texto, sem chave estrangeira —, então renomeá-lo por `UPDATE` deixa as referências apontando
para o valor antigo, exatamente como o `DELETE` faria. Corrigir código errado exige a mesma
verificação de referência viva descrita abaixo, e, havendo alguma, deixa de ser correção por
migration: vira substituição com tratamento do que aponta, como a
[ADR-0112](0112-fronteira-append-only-do-catalogo-de-regras.md) descreve
para o catálogo de regras.

**Remoção exige uma condição a mais: nenhuma referência viva.** As colunas de auditoria não
denunciam uso — usar uma categoria não atualiza a linha dela —, e as referências entre cadastros
deste módulo são **texto sem chave estrangeira**: `TipoDocumento.categoria` guarda o código de
`CategoriaDocumento`, e `PrecedenciaFase` guarda os códigos de `FaseCanonica`, ambos por decisão
deliberada, com a existência garantida pelo handler no caminho de escrita. O banco, portanto, não
recusa o `DELETE` — ele deixa a referência pendurada.

Antes de remover, a migration confere quem aponta para o código:

```sql
DELETE FROM configuracao.categoria_documento
 WHERE codigo = '<código a remover>'
   AND updated_at IS NULL
   AND created_by IS NULL
   AND NOT EXISTS (SELECT 1 FROM configuracao.tipo_documento t
                    WHERE t.categoria = configuracao.categoria_documento.codigo);
```

Havendo referência, a remoção não acontece por migration: é decisão que exige tratar o que aponta
para a linha, e isso é ato administrativo. Onde a linha tiver dono, idem — e a divergência que
sobra é deliberada, não acidente de idade do banco.

**A proibição é do catálogo administrável, não da migration.** Nos catálogos de fato —
`FatoCandidato` e `FatoValorDominio` — a migration de seed é o único caminho de escrita que existe,
e reclassificar um fato exige alterar a linha: é o que a
[ADR-0116](0116-origem-ponto-resolucao-binding-fato-valor-dominio.md) determina, e `INSERT … ON
CONFLICT DO NOTHING` não faria. Ali o `UPDATE` em migration é legítimo, porque não há operador cuja
edição possa ser sobrescrita — o blame da mudança fica no PR, que é onde ele existe.

O discriminador é o mesmo da tabela de momentos: **se a tela permite editar aquele campo, a
migration não pode tocá-lo.**

**Ausência de CRUD não basta, porém.** `RegraCatalogo` também é seed-governado com API somente de
leitura, e ainda assim a [ADR-0112](0112-fronteira-append-only-do-catalogo-de-regras.md)
só admite substituir uma entrada **enquanto nenhuma `VersaoConfiguracao` a referenciar**; a partir
do primeiro congelamento que a use, a linha vira fato e vale append-only estrito. Alterá-la por
migration depois disso mudaria a definição por trás de um processo seletivo já congelado, e o
snapshot deixaria de ser reproduzível.

Ou seja: o caminho de escrita responde **quem** pode alterar; o regime de congelamento responde
**se** ainda pode. Onde houver referência congelada, a resposta é não — por migration ou por
qualquer outro caminho.

**A prescrição vale daqui em diante, e não cobre o que já foi aplicado.** As três migrations de
`Modalidade` — `20260723111701_SeedModalidadesFederais`, `20260829000213_SemeiaModalidadePcdPuro` e
`20260829005332_SemeiaModalidadesInstitucionaisPsiq` — usam `InsertData`, anterior a esta emenda.
Num ambiente onde um administrador já tenha criado um dos códigos semeados, o conflito de código
único **aborta a migração** em vez de pular a linha, e com ela o deploy. O risco é baixo (os códigos
são das modalidades legais, que ninguém cadastra à mão) mas real, e converter essas migrations
exigiria reescrever migration já aplicada — trabalho próprio, não desta emenda. As de
`CategoriaDocumento` e `TipoDocumento` já seguem a forma tolerante; a de `FaseCanonica` nasce assim
na mudança que a semeia, ainda não aplicada.

## Mais informações

- [ADR-0023](0023-problemdetails-rfc-9457.md) — ProblemDetails RFC 9457 (Newman tests checam contra).
- [ADR-0027](0027-idempotency-key-store.md) — Idempotency-Key store (semântica de replay).
- [ADR-0028](0028-versionamento-per-resource-content-negotiation.md) — Vendor MIME per resource (Accept/Content-Type por linha).
- [ADR-0054](0054-naming-convention-e-strategy-migrations.md) — Naming snake_case + migrations.
- `docs/guia-banco-de-dados.md` §5 — Pattern opt-in do `IAuditableEntity`.
- [ADR-0055](0055-organizacao-institucional-bounded-context.md) — Invariante de roster fechado para AreaOrganizacional.
- [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md) — Módulo Parametrizacao e read-side desmembramento (referências ao seeder removidas).
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — Pattern de snapshot-copy cross-módulo (independente deste seed).
- Documentação Newman — <https://learning.postman.com/docs/running-collections/using-newman-cli/command-line-integration-with-newman/>.
- Diretrizes do sponsor (2026-05-14): "remover os seeds e termos os arquivos json para fazer as requests"; "quando a interface gráfica tiver pronta aí pode cadastrar pelo formulario"; "podemos usar newman para fazer isso para nós".
