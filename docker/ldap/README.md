# OpenLDAP local — dados sintéticos

Este diretório contém a configuração do **OpenLDAP local** usada no ambiente de desenvolvimento para reproduzir o cenário do LDAP institucional Unifesspa, em particular o bug histórico de CPF com zero à esquerda truncado.

> ⚠️ **Apenas para ambiente local.** Dados são **sintéticos** (gerados pela ferramenta [4devs](https://www.4devs.com.br/gerador_de_pessoas)) e não pertencem a pessoas reais. Nunca usar em homologação ou produção.

## Por que existe

A validação em homologação do Identity Brokering com gov.br ([ADR-029](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-029-identity-brokering-govbr-ldap-google.md)) revelou que parte dos `brPersonCPF` no LDAP institucional está armazenada com 10 dígitos (zero à esquerda truncado por carga histórica). gov.br retorna sempre 11 dígitos no claim `sub`, e isso quebra qualquer matching ingênuo de CPF entre os dois sistemas.

A solução arquitetural é desenvolver um Authenticator customizado em Java SPI que faça matching tolerante (ver [issue #217](https://github.com/unifesspa-edu-br/uniplus-api/issues/217)). Para desenvolver e testar esse SPI sem expor PII institucional, este OpenLDAP local com dados sintéticos foi criado.

## Como subir

```bash
docker compose -f docker/docker-compose.yml up -d openldap
```

Healthcheck deve ficar `healthy` em ~30s:

```bash
docker compose -f docker/docker-compose.yml ps openldap
# docker-openldap-1   ...   Up 30 seconds (healthy)   636/tcp, 0.0.0.0:1389->389/tcp
```

## Como testar

### Listar todos os entries (ldapsearch externo)

```bash
ldapsearch -x -H ldap://localhost:1389 \
    -b "ou=Users,dc=unifesspa,dc=edu,dc=br" \
    -D "cn=admin,dc=unifesspa,dc=edu,dc=br" -w admin \
    "(objectClass=inetOrgPerson)" uid cn employeeNumber
```

### Buscar pelo CPF malformado (10 dígitos)

```bash
ldapsearch -x -H ldap://localhost:1389 \
    -b "ou=Users,dc=unifesspa,dc=edu,dc=br" \
    -D "cn=admin,dc=unifesspa,dc=edu,dc=br" -w admin \
    "(employeeNumber=7094871422)" uid cn employeeNumber
```

### Conectar via Apache Directory Studio ou similar

| Parâmetro | Valor |
|---|---|
| Host | `localhost` |
| Port | `1389` |
| Base DN | `dc=unifesspa,dc=edu,dc=br` |
| Bind DN | `cn=admin,dc=unifesspa,dc=edu,dc=br` |
| Senha | `admin` |
| TLS | desabilitado |

## Estrutura de dados

```
dc=unifesspa,dc=edu,dc=br
└── ou=Users
    ├── uid=kevin.peixoto      (CPF malformado: 10 dígitos)
    ├── uid=emanuelly.fernandes (CPF malformado: 10 dígitos)
    ├── uid=fernando.melo      (CPF malformado: 10 dígitos)
    ├── uid=lara.almeida       (CPF normal: 11 dígitos)
    ├── uid=aurora.bernardes   (CPF normal: 11 dígitos)
    ├── uid=antonio.jesus      (CPF normal: 11 dígitos)
    ├── uid=jennifer.sales     (CPF normal: 11 dígitos)
    ├── uid=emilly.cruz        (CPF normal: 11 dígitos)
    ├── uid=breno.araujo       (CPF normal: 11 dígitos)
    └── uid=kevin.barbosa      (CPF normal: 11 dígitos)
```

**Distribuição estratégica:** 7 entries com CPF de 11 dígitos (caso feliz, gov.br = LDAP) + 3 entries com CPF de 10 dígitos (zero à esquerda truncado, simulando o bug do LDAP institucional).

Os 3 CPFs canonicais "reais" dos entries malformados (com zero à esquerda — equivalente ao que o gov.br retornaria) ficam comentados no início de cada bloco do LDIF gerado, para facilitar o teste do SPI:

```
# CPF malformado (truncado): canonical=07094871422 | armazenado=7094871422
dn: uid=kevin.peixoto,...
employeeNumber: 7094871422
```

## Atributo `employeeNumber` e `brPersonCPF`

No LDAP institucional Unifesspa, o CPF é armazenado em `brPersonCPF` (atributo do schema customizado `brPerson`). Aqui usamos o atributo padrão `employeeNumber` (de `inetOrgPerson`) com o mesmo papel — evita complexidade de carregar schema custom no OpenLDAP via bootstrap. Para o objetivo de testar o SPI Keycloak, a equivalência é total: o User Federation mapper traduz `employeeNumber` → `cpf` (atributo do user no Keycloak) exatamente como faria com `brPersonCPF` no ambiente institucional.

## Como gerar/regerar o LDIF

O arquivo `bootstrap/01-users.ldif` é versionado no repo e carregado automaticamente pelo container OpenLDAP no primeiro start.

Para regenerar (ex.: ao adicionar mais entries no `data/seed-4devs.json`):

```bash
python3 scripts/generate-ldif.py
```

O script é determinístico: rodar 2x produz output idêntico (CPFs canonicais sintéticos são derivados de hash do nome).

> **Forçar reload do LDAP:** o container só processa o LDIF no **primeiro start**. Para reaplicar, derrubar com volume:
> ```bash
> docker compose -f docker/docker-compose.yml down -v openldap
> docker compose -f docker/docker-compose.yml up -d openldap
> ```

## Como adicionar novos entries

1. Editar `data/seed-4devs.json` (ou substituir por novo dump da 4devs).
2. Os primeiros 3 entries do array sempre viram "malformados" (CPF de 10 dígitos) — ordem importa.
3. Rodar `python3 scripts/generate-ldif.py`.
4. Reload do container conforme acima.

## Origem dos dados

- **Ferramenta:** [4devs — gerador de pessoas](https://www.4devs.com.br/gerador_de_pessoas)
- **Características:** CPFs com DVs válidos pela Receita Federal, mas não pertencentes a pessoas reais. Endereços, telefones, emails são todos sintéticos.
- **Quantidade gerada:** 10 pessoas (em `data/seed-4devs.json`)
- **CPFs canonicais sintéticos com zero à esquerda:** os 3 entries malformados têm seus CPFs reescritos pelo `generate-ldif.py` com base num hash determinístico do nome — DV calculado para garantir validade pela Receita Federal.

## Credenciais do dev local

| Usuário | Senha |
|---|---|
| `cn=admin,dc=unifesspa,dc=edu,dc=br` | `admin` |
| Senha de qualquer user sintético (uid=...) | `Changeme!123` |

Senhas armazenadas em texto plano no LDIF (ambiente dev). Não usar nada similar em PROD.

> 🔒 **Isolamento de rede:** o `docker-compose.yml` mapeia a porta como `127.0.0.1:1389:389` — só acessível pelo host local, NÃO exposto na LAN. Isto compensa o uso de credencial fraca (admin/admin) e o fato de a imagem `osixia/openldap:1.5.0` não receber updates desde 2021. Se precisar acessar de outra máquina (ex.: outro dev no time), preferir SSH tunnel (`ssh -L 1389:localhost:1389 dev-host`) em vez de expor a porta.

## Referências

- [Issue uniplus-api#217](https://github.com/unifesspa-edu-br/uniplus-api/issues/217) — Story que originou este ambiente
- [ADR-029](https://github.com/unifesspa-edu-br/uniplus-docs/blob/main/docs/adrs/ADR-029-identity-brokering-govbr-ldap-google.md) — Identity Brokering gov.br + LDAP via Keycloak
- [PR uniplus-api#216](https://github.com/unifesspa-edu-br/uniplus-api/pull/216) — Configuração inicial do gov.br como IdP
- [osixia/openldap](https://hub.docker.com/r/osixia/openldap) — imagem Docker utilizada
