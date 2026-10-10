# Guia de configuração de cifragem (`UniPlus:Encryption`)

A `uniplus-api` cifra dados sensíveis (cursor de paginação, payload de Idempotency-Key e, futuramente, outros campos do Domínio) por meio de `IUniPlusEncryptionService`. A mesma seção de configuração também escolhe o provider de `IUniPlusBlindIndexService` — o índice cego (HMAC) usado para buscar ou garantir unicidade de um valor cifrado sem decifrá-lo (ADR-0121), cujo primeiro consumidor é a unicidade de CPF na inscrição do candidato (Seleção). A escolha do provider e suas credenciais entram via configuração `IConfiguration` / variáveis de ambiente, sob a seção `UniPlus:Encryption`.

A combinação de provider + campos dependentes é validada **no boot**. Configurações incoerentes derrubam o pod no startup com mensagem específica, em vez de retornar `500 Internal Server Error` silencioso na primeira requisição que toca cifragem. A validação combinada está em `EncryptionOptionsValidator` (registrado por `AddUniPlusEncryption`).

## Matriz de campos por provider

| Campo | `local` | `vault` | Observação |
|---|:---:|:---:|---|
| `Provider` | obrigatório | obrigatório | Valores aceitos: `"local"`, `"vault"` (case-insensitive). |
| `LocalKey` | **obrigatório** | ignorado | Chave AES-GCM 256 em Base64 (32 bytes após decode). |
| `VaultAddress` | ignorado | **obrigatório** | URL absoluta `http`/`https`. Em standalone: `http://platform-vault-uniplus-standalone.vault.svc.cluster.local:8200`. |
| `KubernetesRole` | ignorado | obrigatório (produção) | Role Kubernetes auth registrada no Vault. **Mutuamente exclusivo** com `VaultToken`. |
| `VaultToken` | ignorado | obrigatório (dev/CI) | Token estático para testes de integração — **nunca usar em produção**. **Mutuamente exclusivo** com `KubernetesRole`. |
| `VaultTransitMount` | ignorado | opcional | Mount do engine `transit`. Default: `"transit"`. |
| `KubernetesJwtPath` | ignorado | opcional | Path do JWT do Service Account. Default: `/var/run/secrets/kubernetes.io/serviceaccount/token`. |
| `BlindIndexKeyVersion` | ignorado | opcional | Versão **fixa** da chave HMAC do índice cego (`transit/hmac/<keyName>/sha2-256`). Default: `1`. Não tem efeito no provider `local` — a derivação por HKDF não versiona. |

Quando `Provider=vault`, exatamente um entre `KubernetesRole` e `VaultToken` precisa estar definido. A exclusividade é deliberada: o auth method de `VaultTransitEncryptionService` é determinado pela configuração — `KubernetesRole` ativa Kubernetes auth (lê o JWT do Service Account em `KubernetesJwtPath`); `VaultToken` ativa token auth. Não há heurística silenciosa de fallback. Se a configuração disser "K8s" mas o JWT não estiver disponível em disco (ou estiver vazio), o serviço falha no primeiro resolve com mensagem específica citando o path esperado, em vez de cair para outro auth method.

## Variáveis de ambiente equivalentes

ASP.NET Core mapeia `:` em settings para `__` em env vars:

```bash
UNIPLUS__ENCRYPTION__PROVIDER=local
UNIPLUS__ENCRYPTION__LOCALKEY=<base64-32-bytes>
# ou
UNIPLUS__ENCRYPTION__PROVIDER=vault
UNIPLUS__ENCRYPTION__VAULTADDRESS=http://platform-vault-uniplus-standalone.vault.svc.cluster.local:8200
UNIPLUS__ENCRYPTION__KUBERNETESROLE=uniplus-api
```

## Gerar uma chave local para dev/CI

```bash
head -c 32 /dev/urandom | base64
```

A chave gerada nunca deve ser commitada. Em CI vive como segredo do runner; em ambientes Kubernetes, custodiada no Vault em `secret/<env>/uniplus-api/encryption-local` e materializada como Secret K8s via External Secrets Operator.

## Diagnóstico — todos os erros estouram no boot

Qualquer config inválida derruba o pod com `CrashLoopBackOff` no startup, antes do app aceitar tráfego. Dois mecanismos cooperam para isso:

1. **`EncryptionOptionsValidator` + `ValidateOnStart()`** — checa as settings que não dependem de recursos externos (provider conhecido, LocalKey 32 bytes Base64, VaultAddress como URL absoluta http/https, exclusividade KubernetesRole vs VaultToken). Falha → `OptionsValidationException` no `IStartupValidator.Validate()`.
2. **`EncryptionWarmupHostedService`** — força a resolução do singleton `IUniPlusEncryptionService` no `Host.StartAsync`, propagando qualquer falha do construtor (JWT do ServiceAccount ausente, conteúdo whitespace, `KubernetesJwtPath` em branco) como exceção que aborta o boot.

| Sintoma no log | Mecanismo | Causa |
|---|---|---|
| `UniPlus:Encryption:LocalKey é obrigatório quando Provider = 'local'` | Validator | `Provider=local` (default) mas chave ausente. Provavelmente faltou `ExternalSecret` no chart Helm. |
| `UniPlus:Encryption:LocalKey não é uma string Base64 válida` | Validator | Chave contém caracteres inválidos. Regere com o comando acima. |
| `UniPlus:Encryption:LocalKey deve ter 32 bytes (256 bits) após decode Base64` | Validator | Chave com tamanho errado — AES-GCM-256 exige exatamente 32 bytes. |
| `UniPlus:Encryption:Provider inválido: '...'` | Validator | Valor digitado não está em `{local, vault}`. |
| `UniPlus:Encryption:VaultAddress é obrigatório quando Provider = 'vault'` | Validator | Esqueceu de plugar o endereço do Vault nos values do environment. |
| `UniPlus:Encryption:VaultAddress inválido: '...' Deve ser uma URL absoluta com scheme http ou https` | Validator | Endereço sem scheme ou com scheme inesperado (ex.: `file://`). |
| `UniPlus:Encryption: KubernetesRole e VaultToken são mutuamente exclusivos` | Validator + construtor | Ambos definidos simultaneamente. Em produção, manter apenas `KubernetesRole`. |
| `UniPlus:Encryption: nem KubernetesRole nem VaultToken estão definidos` | Validator + construtor | Authentication method não configurado. |
| `JWT do ServiceAccount não encontrado em '...'` | Warmup hosted service | `Provider=vault` + `KubernetesRole` definido, mas o arquivo do JWT não existe no path configurado. Verificar `automountServiceAccountToken=true` e volume projetado do SA. |
| `JWT do ServiceAccount em '...' está vazio` | Warmup hosted service | Arquivo presente mas sem conteúdo. O kubelet costuma re-popular automaticamente; verificar logs e estado do volume projetado. |
| `UniPlus:Encryption:KubernetesJwtPath está vazio` | Warmup hosted service | Path não configurado. Default é `/var/run/secrets/kubernetes.io/serviceaccount/token`; raramente precisa ser override. |

## Por que validar no boot

A escolha do `IUniPlusEncryptionService` (`LocalAesEncryptionService` vs `VaultTransitEncryptionService`) é resolvida por factory delegate em `CryptographyServiceCollectionExtensions`. Sem o validator + warmup, `Provider=local` sem `LocalKey` só estouraria quando o serviço é instanciado pelo DI **na primeira requisição** que toca cifragem (rota de Idempotency-Key, por exemplo). O consumer receberia `500 Internal Server Error` e o operador precisaria correlacionar log da API com mudanças de configuração — diagnóstico caro em produção.

`OptionsValidationException` no `ValidateOnStart` + falha-no-construtor via `EncryptionWarmupHostedService` transformam todos esses cenários em `CrashLoopBackOff` claro: o pod nunca chega a aceitar tráfego com configuração inconsistente.

## Test factories

`ApiFactoryBase` (em `tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/`) carrega `appsettings.Development.json` que já provê `UniPlus:Encryption:LocalKey` para dev/CI. Por isso o warmup hosted service roda normalmente nos integration tests do projeto. Caso uma fixture específica precise mockar o pipeline sem cifragem real (cenário raro), o pattern do `DisableWolverineRuntimeForTests` é o template para criar um filtro análogo via heurística estável de `ImplementationType`.

## Relação com Vault Transit

A trilha Vault Transit em produção depende de:

1. Mount `transit/` + key `uniplus-idempotency-aesgcm` + policy + role K8s auth no Vault (uniplus-infra#219).
2. Wire-up dos values dos charts `uniplus-api-{portal,selecao,ingresso}` (uniplus-infra#220).

Enquanto o Transit não está disponível em um environment, o provider default `local` permanece adequado para dev/CI desde que a chave esteja presente.

## Índice cego (`IUniPlusBlindIndexService`)

Mesma seção `UniPlus:Encryption`, mesmo `Provider` — escolher `vault` ou `local` troca os dois serviços (cifra e índice cego) juntos, nunca um sem o outro.

- **Provider `local`:** HMAC-SHA256 com uma subchave derivada de `LocalKey` por HKDF-SHA256, com `info = "uniplus-indice-cego:" + keyName`. A subchave nunca é `LocalKey` diretamente — cada `keyName` tem a sua, derivada; a mesma `LocalKey` com `keyName` diferentes produz índices diferentes e não comparáveis entre si.
- **Provider `vault`:** `transit/hmac/<keyName>/sha2-256`, com `key_version` fixado por `BlindIndexKeyVersion` (ver matriz acima). O HMAC devolvido pelo Vault vem prefixado com a versão (`vault:v1:<base64>`) — o serviço descarta esse prefixo antes de devolver o índice; o que é gravado no banco é só o valor HMAC.

### Por que a versão da chave é fixa, não "a mais recente"

O índice cego serve de chave de busca (ex.: unicidade de CPF por processo seletivo). Se o índice acompanhasse a versão mais recente da chave automaticamente, **girar a chave no Vault por qualquer motivo trocaria todos os índices já gravados em silêncio**, e nenhuma busca por um valor antigo encontraria a linha correspondente — a unicidade quebraria sem erro nenhum, só resultados que deixam de aparecer.

### Procedimento de rotação (ou troca de provider)

Rotacionar a chave HMAC no Vault, ou trocar `Provider` de `local` para `vault` (ou vice-versa), muda a função que produz o índice — os índices já gravados ficam órfãos (nenhuma busca por eles bate mais). Não é uma operação de "rodar e esquecer":

1. Girar a chave no Vault (`POST transit/keys/<keyName>/rotate`) ou preparar o novo provider — a versão antiga continua decifrável, só não é mais a versão corrente.
2. Decifrar (não o índice — o **valor original protegido**, via `IUniPlusEncryptionService`) cada linha que tem índice da versão antiga.
3. Recalcular o índice de cada uma com a chave/versão nova.
4. Só então atualizar `BlindIndexKeyVersion` (ou `Provider`) na configuração do deploy, depois que todas as linhas tiverem o índice novo gravado — nunca antes, senão o período de transição tem linhas com índices de duas gerações e nenhuma query cobre as duas ao mesmo tempo.

Sem um consumidor com volume de dados em produção ainda, este procedimento continua sendo plano, não ferramenta — a primeira aplicação real (unicidade de CPF na inscrição) é o gatilho para automatizá-lo.

### Limitação conhecida — `BlindIndexKeyVersion` é único por processo, não por `keyName`

A ADR-0121 decide uma chave HMAC por módulo, nunca compartilhada. `BlindIndexKeyVersion`, porém, é
uma única configuração para todo o processo, aplicada a qualquer `keyName` que `ComputarAsync`
receba. Hoje isso não é problema: há um único consumidor (a unicidade de CPF da inscrição) e uma
única `keyName` em uso. Se um segundo módulo adotar o índice cego com chave própria, girar a chave
de um módulo sem girar a do outro deixaria a versão fixa certa para um e errada para o outro — nesse
momento, `BlindIndexKeyVersion` precisa virar configuração por `keyName` (ex.: mapa `keyName →
versão`), antes de o segundo consumidor entrar em produção, não depois.

## Referência cruzada

- ADR-0027 — Idempotency-Key (boundary validation completada por este fix).
- ADR-0010 — Estratégia de segredos via Vault + ESO.
- ADR-0121 — Cifra em repouso; emenda do índice cego (primeiro consumidor, versão fixa, rotação).
- ADR-0136 e ADR-0081 — classificação de proteção de dados; emenda da cifra só de `Identificador`.
- Story uniplus-infra#219 — stand-up do Vault Transit no standalone.
- Story uniplus-infra#220 — wire-up `uniplus-api → Vault Transit`.
