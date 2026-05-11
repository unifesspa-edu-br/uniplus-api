# Guia de configuração de cifragem (`UniPlus:Encryption`)

A `uniplus-api` cifra dados sensíveis (cursor de paginação, payload de Idempotency-Key e, futuramente, outros campos do Domínio) por meio de `IUniPlusEncryptionService`. A escolha do provider e suas credenciais entram via configuração `IConfiguration` / variáveis de ambiente, sob a seção `UniPlus:Encryption`.

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

Quando `Provider=vault`, exatamente um entre `KubernetesRole` e `VaultToken` precisa estar definido. A exclusividade é deliberada: `VaultTransitEncryptionService` cai para `VaultToken` quando o JWT do Service Account não está montado em disco; aceitar ambos definidos abriria janela para um token estático suplantar a identidade de workload em caso de erro operacional no mount do SA.

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

## Diagnóstico — erros típicos no boot

Cada falha de validação cita o caminho exato da setting que precisa de atenção:

| Sintoma no log | Causa |
|---|---|
| `UniPlus:Encryption:LocalKey é obrigatório quando Provider = 'local'` | `Provider=local` (default) mas chave ausente. Provavelmente faltou `ExternalSecret` no chart Helm. |
| `UniPlus:Encryption:LocalKey não é uma string Base64 válida` | Chave contém caracteres inválidos. Regere com o comando acima. |
| `UniPlus:Encryption:LocalKey deve ter 32 bytes (256 bits) após decode Base64` | Chave com tamanho errado — AES-GCM-256 exige exatamente 32 bytes. |
| `UniPlus:Encryption:Provider inválido: '...'` | Valor digitado não está em `{local, vault}`. |
| `UniPlus:Encryption:VaultAddress é obrigatório quando Provider = 'vault'` | Esqueceu de plugar o endereço do Vault nos values do environment. |
| `UniPlus:Encryption:VaultAddress inválido: '...' Deve ser uma URL absoluta com scheme http ou https` | Endereço sem scheme ou com scheme inesperado (ex.: `file://`). |
| `UniPlus:Encryption requer KubernetesRole (produção) ou VaultToken (testes/dev) quando Provider = 'vault'` | Authentication method não configurado. |
| `UniPlus:Encryption: KubernetesRole e VaultToken são mutuamente exclusivos` | Ambos foram definidos simultaneamente. Em produção, manter apenas `KubernetesRole`. |

## Por que validar no boot

A escolha do `IUniPlusEncryptionService` (`LocalAesEncryptionService` vs `VaultTransitEncryptionService`) é resolvida por factory delegate em `CryptographyServiceCollectionExtensions`. Sem o validator condicional, `Provider=local` sem `LocalKey` só estoura quando o serviço é instanciado pelo DI **na primeira requisição** que toca cifragem (rota de Idempotency-Key, por exemplo). O consumer recebe `500 Internal Server Error` e o operador precisa correlacionar log da API com mudanças de configuração — diagnóstico caro em produção.

O fail-fast no boot transforma esse `500` em um `CrashLoopBackOff` claro: o pod nunca chega a aceitar tráfego com configuração inconsistente.

## Relação com Vault Transit

A trilha Vault Transit em produção depende de:

1. Mount `transit/` + key `uniplus-idempotency-aesgcm` + policy + role K8s auth no Vault (uniplus-infra#219).
2. Wire-up dos values dos charts `uniplus-api-{portal,selecao,ingresso}` (uniplus-infra#220).

Enquanto o Transit não está disponível em um environment, o provider default `local` permanece adequado para dev/CI desde que a chave esteja presente.

## Referência cruzada

- ADR-0027 — Idempotency-Key (boundary validation completada por este fix).
- ADR-0010 — Estratégia de segredos via Vault + ESO.
- Story uniplus-infra#219 — stand-up do Vault Transit no standalone.
- Story uniplus-infra#220 — wire-up `uniplus-api → Vault Transit`.
