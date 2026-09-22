# Seed de catálogos

Bootstrap dos cadastros administráveis, conforme a [ADR-0062](../../docs/adrs/0062-seed-de-catalogos-via-newman-e-endpoints-admin.md).

Cada linha entra pelos **endpoints admin da API**, não por `HasData`/`InsertData`. A diferença não é estética: `InsertData` grava direto na tabela, fabrica um `CreatedBy` sintético e passa por fora do `AuditableInterceptor`. Pelo endpoint, a linha percorre validação, invariantes de domínio e auditoria como qualquer outra escrita, e o `CreatedBy` fica sendo o `sub` do service account — o que torna a trilha legível: linhas do bootstrap distinguem-se das que um humano cadastrou pela UI.

## Uso

```bash
# Todos os catálogos, ambiente dev (stack local em :5200)
ENV=dev bash tools/seeds/run.sh

# Um catálogo específico
ENV=dev CATALOG="Tipos de ato" bash tools/seeds/run.sh
```

Pré-requisito no dev: a stack de smoke no ar (API em `:5200`, Keycloak em `:8080`, realm `unifesspa-dev-local`). Ver `CONTRIBUTING.md`, seção *Smoke / Newman*.

O `run.sh` propaga o exit code do Newman — um seed que falha derruba o passo de bootstrap do pipeline.

### Fora do dev

Os templates `standalone` e `hml` trazem apenas o que é público. O secret do client de bootstrap **não é versionado**: vem do `uniplus-infra`, por variável de ambiente.

```bash
BOOTSTRAP_CLIENT_SECRET=*** ENV=standalone bash tools/seeds/run.sh
```

Sem ele, o script para antes de chamar o Newman e nomeia a causa — em vez de deixar o token endpoint devolver 401 e a falha aparecer no primeiro request.

**Os endereços saem do `uniplus-infra`, e não deste repositório.** Cada ambiente declara o host da
API em `ingress.host` e o issuer do OIDC em `oidc.issuerUri`, no `values.yaml` da sua pasta em
`environments/` — a pasta não tem o nome do `ENV`: `hml` é `hml-standalone-single/`, `standalone` é
`standalone-compact/`.

Os dois campos são derivados, não copiados:

- `base_url` é `https://` mais o `ingress.host` da API.
- `keycloak_token_url` é o `issuerUri` **mais** `/protocol/openid-connect/token`. O issuer sozinho
  não é endpoint de token, e é ele que carrega o prefixo de path do Keycloak (`/auth` em HML e em
  standalone, ausente no dev local) — prefixo perdido é a forma mais comum de errar este campo.

Conferir os dois ao mexer: host que não resolve não produz erro nomeado — é o Newman morrer na
resolução de DNS, que parece indisponibilidade do ambiente.

Atenção ao realm, que **não é o mesmo em todo ambiente**: `unifesspa` em HML, `uniplus` em
standalone, `unifesspa-dev-local` no dev local. Na dúvida, o realm é o último segmento do
`issuerUri` do ambiente.

Um ambiente inteiro pode vir de fora do repositório, montado pelo pipeline:

```bash
SEED_ENV_FILE=/run/secrets/prod.postman_environment.json bash tools/seeds/run.sh
```

Qualquer variável do ambiente pode ser sobrescrita sem editar arquivo: `BASE_URL`, `KEYCLOAK_TOKEN_URL`, `BOOTSTRAP_CLIENT_ID`, `BOOTSTRAP_CLIENT_SECRET`.

## Reexecutar é seguro, e não pelo motivo óbvio

Cada linha faz **preflight**: consulta `GET /tipos-ato/{codigo}/vigente` antes de escrever. Se o tipo já existe, **todos** os campos são comparados com o arquivo de seed — código, nome, os três atributos de consequência, a janela de vigência e a base legal — e o `POST` é **pulado**. Se qualquer um divergir, o seed **falha**, em vez de sobrescrever em silêncio ou de aceitar uma linha que não é a que o arquivo declara.

No script da coleção o pulo é `pm.execution.skipRequest()`. `setNextRequest(null)` não serviria: ele encerra a iteração **depois** que o request atual roda, e o `POST` chegaria ao servidor de qualquer forma.

### Por que a `Idempotency-Key` **não** é derivada do código

A ADR-0062 sugere `Idempotency-Key: {recurso}-{codigo}`, estável entre execuções. Isso é sutilmente perigoso aqui, e o motivo merece registro.

O cache de idempotência guarda a resposta por 24 horas ([ADR-0027](../../docs/adrs/0027-idempotency-key-store-postgresql.md)). Considere: o seed roda, cria `AVISO`; alguém remove `AVISO` pela UI; o seed roda de novo no mesmo dia. O preflight devolve 404 — corretamente, o tipo não existe — e libera o `POST`. Com a chave estável, o `POST` casa a entrada em cache, recebe o **201 original de volta**, e nada é criado. O seed termina verde, e `AVISO` continua ausente.

Por isso a chave é `{{$guid}}`, gerada por execução, e o request assere que a resposta **não** traz `Idempotency-Replayed: true`. A idempotência do seed vem do preflight; a chave existe apenas para satisfazer o `[RequiresIdempotencyKey]` do endpoint. Quem protege o retry de transporte é o próprio preflight: reexecutar depois de uma resposta perdida encontra o tipo criado e pula.

## Autenticação

`client_credentials` contra o Keycloak, com o client confidencial `uniplus-api-bootstrap` (service account com a realm role `plataforma-admin`). O token é cacheado na coleção até 60 s antes de expirar.

Em `dev`, o client e o secret vivem no realm export versionado (`docker/keycloak/realm-export-dev-local.json`) — são credenciais de desenvolvimento, sem valor fora da máquina local. Em **standalone, HML e PROD** o client é precondição de deploy e o secret vem do `uniplus-infra`, por `BOOTSTRAP_CLIENT_SECRET`: **nunca** versionado neste repositório.

Em homologação o client **ainda não existe**: o realm importado pelo `uniplus-infra` declara os
quatro clients de serviço da API, o do portal, o do Apicurio, o do Kafka UI e o do Grafana — e
nenhum `uniplus-api-bootstrap`. Enquanto for assim, o seed para no token endpoint por mais correto
que esteja o resto.

Conferir que o client existe **antes** de culpar o secret: um `client_id` desconhecido e um secret
errado devolvem o mesmo 401 `invalid_client`, e ler esse 401 como "falta o segredo" manda quem
investiga para o lado errado. Existir também não basta — o service account precisa da realm role
`plataforma-admin`, que os endpoints admin exigem, e de token cujo `aud` bata com o `Auth:Audience`
da API; sem isso o 401 do Keycloak vira 401 ou 403 no primeiro request.

## Estrutura

```
seeds/
  seed-tipos-ato.json                     ← dados: array flat, camelCase, um objeto por linha
  seed-calendario-dias-uteis.json         ← idem, um objeto por calendário
tools/seeds/
  seed-catalogos.postman_collection.json  ← um folder por catálogo (preflight + POST + releitura)
  envs/dev.postman_environment.json        ← secret de desenvolvimento, sem valor fora da máquina local
  envs/standalone.postman_environment.json ← secret vazio: vem do uniplus-infra
  envs/hml.postman_environment.json        ← idem
  run.sh
```

O corpo do `POST` é montado a partir da linha do arquivo, campo a campo, e o request **relê o recurso criado** pelo `Location` para conferir o que foi de fato gravado. O `POST` devolve só o `id`: sem a releitura, um corpo incompleto persistiria valores errados e o Newman reportaria sucesso.

O Newman é **pinado por versão exata** no `run.sh`. Um bootstrap cuja ferramenta muda sozinha entre execuções não é bootstrap.

## Acrescentar um catálogo

1. Criar `seeds/seed-<catalogo>.json`, no shape que o endpoint admin espera no body.
2. Acrescentar um folder na coleção, com o par preflight + `POST`.
3. Registrar o par no mapa `CATALOGOS` do `run.sh`.

## Acrescentar um tipo de ato

Uma linha em `seeds/seed-tipos-ato.json`, e rodar de novo. Se isso exigir tocar em `src/publicacoes/`, a [ADR-0103](../../docs/adrs/0103-ato-normativo-generalizado-retificacao-como-relacao.md) foi violada — e o fitness test `PublicacoesSemRamificacaoPorTipoAtoTests` deve ter acusado antes.
