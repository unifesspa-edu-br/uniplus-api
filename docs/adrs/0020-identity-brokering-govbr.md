---
status: "accepted"
date: "2026-05-01"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0020: Identity brokering gov.br via Keycloak

## Contexto e enunciado do problema

A plataforma Uni+ é o sistema institucional de identificação digital e controle de acesso aos serviços digitais da Unifesspa. O Keycloak (ADR-0016) atua como provedor central de identidade do `uniplus-api`, mas o lado de federação externa não estava detalhado: a ADR-0016 menciona "federação com Gov.br via padrão OIDC" sem especificar a estratégia de brokering, deixando configuração de realm, mappers e flows sujeita a re-decisão a cada implementação.

A Story `uniplus-api#218` (cpf-matcher SPI) foi entregue em 01/05/2026 (PR #228 mergeado): SPI publicada na v1.0.2, smoke E2E ok, integração funcional do Keycloak com gov.br via Identity Provider OIDC e flow customizado de first-broker-login. O artefato canônico de consumo é a **imagem Docker composta** `ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.x` (base `quay.io/keycloak/keycloak:26.5.7` + JAR `cpf-matcher` embutido em `/opt/keycloak/providers/`), publicada pelo repo `unifesspa-edu-br/uniplus-keycloak-providers` a cada tag `v*.*.*`. O fluxo antigo (clonar o repo de providers lado a lado, buildar o JAR via Maven e fazer mount como volume) está obsoleto — devs e operadores apenas consomem a imagem pronta. O JAR continua sendo publicado no GitHub Release como artefato auditável. Esta ADR formaliza a decisão arquitetural por trás da entrega.

A integração precisa atender:

- **Lei 14.129/2021 (Governo Digital):** uso preferencial do Login Único do gov.br para serviços ao cidadão.
- **Lei 12.711/2012 atualizada pela Lei 14.723/2023 (Cotas):** identificação confiável do CPF para vinculação a inscrições e classificações.
- **LGPD (Lei 13.709/2018):** minimização de dados, retenção curta e mascaramento (peer arquitetural ADR-0011 cobre o lado de logs).
- **RN01:** um CPF só pode realizar uma inscrição ativa por processo seletivo — exige CPF como identificador único do candidato.

Em 23/04/2026 foi enviada solicitação formal ao gov.br requisitando o cadastro da Unifesspa como parceira integradora. Em 27/04/2026 foram fornecidas as credenciais de homologação. As credenciais de produção dependem da hospedagem do RP em domínio oficial `.edu.br`, conforme **Portaria SGD/MGI nº 7.076/2024, art. 3º** — a Unifesspa qualifica como autarquia federal.

A documentação técnica oficial do gov.br (`acesso.gov.br/roteiro-tecnico`) impõe um conjunto restrito de capacidades: somente `client_secret_basic` para autenticação no token endpoint, PKCE S256 obrigatório, RS256 como algoritmo de assinatura do `id_token`, ausência de suporte a `private_key_jwt`, mTLS, FAPI ou CIBA. Essas restrições direcionam parte das decisões abaixo.

## Drivers da decisão

- Conformidade legal com Lei 14.129/2021 (uso preferencial do Login Único).
- CPF verificado pelo gov.br como insumo direto para o cumprimento da RN01 e da Lei de Cotas.
- Fricção zero para candidato em janela de inscrição com prazo legal — perda direta de candidatos é inaceitável.
- Centralização de identidade no realm institucional, mantendo troca de IdP externo transparente para os apps.
- Restrições protocolares do gov.br (`client_secret_basic`, sem `private_key_jwt`/mTLS) — segurança ancorada em TLS de transporte e manejo do secret.
- Nível de confiabilidade gov.br (bronze/prata/ouro) auditável por regras de negócio futuras.

## Opções consideradas

- Identity brokering gov.br via Keycloak com auto-link por CPF no first-broker-login (decisão).
- Cadastro próprio de candidatos no Uni+ (CPF + senha + verificação por e-mail), sem federar com gov.br.
- gov.br como User Federation síncrona em vez de IdP brokering.
- `private_key_jwt` ou mTLS para autenticação do RP no token endpoint do gov.br.
- Confirmação por e-mail no first-broker-login (em vez de auto-link por CPF).

## Resultado da decisão

**Escolhida:** identity brokering gov.br via Keycloak, com auto-link por CPF no first-broker-login.

O realm institucional `unifesspa` recebe um Identity Provider OIDC com alias `govbr`, e o `uniplus-api` permanece resource server validando tokens emitidos pelo realm — a cadeia de confiança gov.br → Keycloak → `uniplus-api` é transparente para os módulos de domínio.

### Configuração do IdP gov.br

| Parâmetro | Valor | Observação |
|---|---|---|
| `alias` | `govbr` | Compõe o redirect URI `<keycloak>/realms/unifesspa/broker/govbr/endpoint` |
| `providerId` | `oidc` | Provider OIDC genérico do Keycloak (não há plugin específico) |
| `clientAuthMethod` | `client_secret_basic` | Única opção aceita pelo gov.br |
| `pkceEnabled` | `true` | Obrigatório no gov.br |
| `pkceMethod` | `S256` | Único método aceito |
| `syncMode` | `FORCE` | Atualiza atributos do usuário a cada login (CPF, nome, nível de confiabilidade) |
| `trustEmail` | `true` | E-mail do gov.br já é verificado pela própria plataforma |
| `linkOnly` | `false` | Permite criação de usuário no primeiro login |
| `storeToken` | `false` | Tokens do gov.br não são persistidos no Keycloak (minimização LGPD) |
| `defaultScope` | `openid email profile govbr_confiabilidades govbr_confiabilidades_idtoken` | Inclui `_idtoken` para receber `reliability_info` no `id_token` sem chamada extra ao userinfo |

### Endpoints gov.br

- **Homologação:** issuer `https://sso.staging.acesso.gov.br/.well-known/openid-configuration`; userinfo `https://sso.staging.acesso.gov.br/userinfo/` (com barra final, conforme doc oficial); JWKS `https://sso.staging.acesso.gov.br/jwk` (singular, conforme doc oficial); logout `https://sso.staging.acesso.gov.br/logout`.
- **Produção:** mesmos paths em `sso.acesso.gov.br` (sem `staging.`).

### `client_id` por hostname e gating de produção

O `client_id` registrado no gov.br é **idêntico ao hostname público do RP** — em homologação `keycloak-hom.unifesspa.edu.br`, em produção `keycloak.unifesspa.edu.br`. Como consequência:

- Credenciais de produção só são liberadas após o Keycloak institucional estar deployado no domínio oficial `.edu.br` com TLS válido (Portaria SGD/MGI 7.076/2024 art. 3º). O deploy do realm é pré-requisito explícito do go-live.
- Mudanças nas URLs de redirect login/post-logout exigem canal formal gov.br (não há autoatendimento) — a comunicação por escrito da URL atual cadastrada precisa preceder qualquer teste de produção.

### Redirect URIs registrados no gov.br

- HML: `https://keycloak-hom.unifesspa.edu.br/realms/unifesspa/broker/govbr/endpoint`
- PROD: `https://keycloak.unifesspa.edu.br/realms/unifesspa/broker/govbr/endpoint`

### Mapeamento de claims (Identity Provider Mappers)

| Mapper | Tipo Keycloak | Origem (claim gov.br) | Destino |
|---|---|---|---|
| `cpf` | `oidc-user-attribute-idp-mapper` | `sub` (CPF do usuário, conforme doc gov.br) | atributo `cpf` |
| `nome-completo` | `oidc-username-idp-mapper` (parcial) + `oidc-user-attribute-idp-mapper` | `name` | `firstName` + `lastName` (split na última palavra) |
| `email` | `oidc-user-attribute-idp-mapper` | `email` | `email` |
| `nivel-confiabilidade` | `oidc-user-attribute-idp-mapper` (com JSON path) | `reliability_info.level` no `id_token`, valores `bronze`/`silver`/`gold` | atributo `nivelConfiabilidade` |
| `role-candidato` | `oidc-hardcoded-role-idp-mapper` | — (constante) | role realm `candidato` |

O atributo `cpf` é exposto pelo client scope `uniplus-profile` (existente no realm) como claim no `id_token` e no `/userinfo` — consumido pelo frontend e pelo `uniplus-api` sem chamada extra. Em logs estruturados, o valor é mascarado como `***.***.***-XX` pelo `PiiMaskingEnricher` (ADR-0011).

### Política de first-broker-login: auto-link por CPF

Ao primeiro login via gov.br, executa-se um flow customizado clonado de `First Broker Login` que substitui o executor padrão `Detect Existing Broker User` por uma SPI Java compilada (`cpf-matcher`):

1. Lê o `sub` (CPF) retornado pelo gov.br.
2. Busca usuário existente no realm pelo atributo `cpf`.
3. Se encontrar, **vincula automaticamente** a conta do gov.br ao usuário existente, sem confirmação por e-mail.
4. Se não encontrar, **cria automaticamente** novo usuário no realm, com `cpf`, `email`, `firstName`, `lastName` e role `candidato`.

A SPI `cpf-matcher` foi publicada na v1.0.2 e validada por smoke E2E na entrega da Story #218. Fluxo padrão do Keycloak usa **e-mail** como chave de matching, o que falha quando o candidato muda de e-mail entre processos seletivos — CPF é estável e já garantido único pela RN01.

### Configurações operacionais

- **Client secret** do gov.br armazenado **exclusivamente** em Kubernetes Secret (HML/PROD) ou variável de ambiente local protegida. Nunca em `realm-export.json`, `.env` versionado, documentação, ticket ou chat.
- **Rotação de secret:** anual (ou imediata em caso de incidente), via canal formal gov.br.
- **Logs do Keycloak:** configuração não pode expor o header `Authorization` em modo DEBUG.
- **TLS obrigatório** em todos os endpoints (exigência do gov.br e do realm institucional).
- **Single Logout (SLO):** ao deslogar do Uni+, propagar logout para o gov.br via `end_session_endpoint`.
- **Smoke E2E:** suíte mantém usuário fictício em homologação para validar o ciclo completo a cada deploy do realm — sem PII real, identificadores derivados de seeds determinísticos.

## Consequências

### Positivas

- Conformidade com Lei 14.129/2021 e cumprimento institucional do Login Único.
- Fricção zero para candidato: clica "Entrar com gov.br" → confirma consentimento no gov.br → entra direto no Uni+, sem cadastro adicional, sem confirmação de e-mail.
- CPF verificado pelo gov.br elimina cadastros fraudulentos com CPF de terceiros, fortalecendo a integridade dos processos seletivos.
- Nível de confiabilidade auditável (`nivelConfiabilidade`) disponível para regras de negócio futuras (ex.: exigir conta prata para inscrições em editais sensíveis).
- Centralização real: todos os módulos (Seleção, Ingresso, Portal e futuros) consomem o mesmo realm; troca de IdP gov.br por outro provedor é transparente para os apps.
- SLO via gov.br: usuário desloga em uma sessão e propaga para todas as aplicações federadas.

### Negativas

- Dependência operacional do gov.br: indisponibilidade do gov.br impede login de candidatos. Mitigação: comunicado claro na tela de login e referência à status page do gov.br.
- `syncMode: FORCE` propaga atualizações de e-mail/CPF feitas no gov.br ao Uni+ — comportamento desejado, mas exige atenção em auditoria.
- Flow customizado de first-broker-login (SPI `cpf-matcher`) é mais código a manter no Keycloak vs. flow padrão. Mitigação: testes E2E cobrindo auto-link por CPF e criação de novo usuário.
- Restrição protocolar do gov.br (apenas `client_secret_basic`, sem `private_key_jwt`/mTLS) — segurança limitada ao TLS de transporte e ao manejo do secret.
- Mudanças nas URLs de redirect login/post-logout só pelo canal formal gov.br — não há autoatendimento; toda mudança deve ser confirmada por escrito antes do deploy.

### Riscos

- **Comprometimento do `client_secret`:** vazamento permite a um atacante iniciar fluxos OIDC em nome do RP. Mitigação: storage só em Kubernetes Secret, rotação anual, alerta automático em logs (instrumentação coberta pela ADR-0018).
- **Colisão de CPF no auto-link:** dois usuários no realm com mesmo `cpf` por engano de cadastro manual quebraria o auto-link. Mitigação: constraint de unicidade no atributo `cpf` no realm + procedimento operacional para correção manual.
- **gov.br adicionar novos requisitos (FAPI, mTLS):** evolução futura do gov.br pode exigir migração de protocolo. Mitigação: monitorar canal de comunicação gov.br e o portal de gestores; abrir ADR superseding quando ocorrer.
- **Bloqueio de credenciais de PROD:** sistema precisa estar deployado em `keycloak.unifesspa.edu.br` com TLS válido **antes** de receber credenciais de produção. Mitigação: planejar deploy do Keycloak institucional como pré-requisito explícito do go-live.

## Confirmação

- SPI `cpf-matcher` v1.0.2 publicada (como JAR no GitHub Release e embutido na imagem composta `ghcr.io/unifesspa-edu-br/uniplus-keycloak:1.0.2`) e validada por smoke E2E na entrega da Story #218.
- Suíte de smoke executa fluxo completo (autorize → callback → first-broker-login → token no `uniplus-api`) contra realm de homologação, sem PII real.
- Health check `/health/auth` valida que a chave pública do realm é acessível e que validação de token simulado passa.
- ADR-0016 referencia esta ADR na seção `Mais informações` como ponteiro acionável.

## Prós e contras das opções

### Cadastro próprio de candidatos no Uni+

- Bom, porque elimina dependência externa do gov.br.
- Ruim, porque viola Lei 14.129/2021 (uso preferencial do Login Único), reinventa cadastro/recuperação/verificação de identidade, não dá nível de confiabilidade auditável e adiciona fricção significativa para quem já tem conta gov.br.

### gov.br como User Federation síncrona

- Bom, porque centralizaria a base de usuários sem brokering.
- Ruim, porque o gov.br **não oferece** API de federação síncrona — apenas autenticação via OIDC. Tecnicamente impossível.

### `private_key_jwt` ou mTLS no token endpoint do gov.br

- Bom, porque eliminaria o segredo compartilhado entre RP e gov.br.
- Ruim, porque o gov.br **não suporta** esses métodos hoje (confirmado na doc oficial). Reavaliação futura como ADR superseding caso o gov.br venha a oferecer.

### Confirmação por e-mail no first-broker-login

- Bom, porque é o comportamento padrão do Keycloak.
- Ruim, porque a fricção em janela de inscrição com prazo legal causa abandono direto. E-mail é menos estável que CPF entre processos seletivos. gov.br já valida CPF e e-mail — duplicar verificação não agrega segurança.

## Mais informações

- ADR-0010 — audience única `uniplus` em tokens OIDC (validação no resource server).
- ADR-0011 — mascaramento de CPF em logs via enricher Serilog (peer arquitetural; CPF do gov.br é mascarado em todos os sinks).
- ADR-0016 — Keycloak como identity provider OIDC (esta ADR detalha a federação externa que a ADR-0016 cita sem especificar).
- ADR-0018 — OpenTelemetry para instrumentação do `uniplus-api` (instrumentação que monitora rotação/uso do secret).
- ADR-0019 — proibir PII em path segments de URL (CPF do gov.br nunca compõe path).
- Lei 14.129/2021 — Lei do Governo Digital.
- Lei 12.711/2012 atualizada pela Lei 14.723/2023 — Lei de Cotas.
- LGPD (Lei 13.709/2018), Art. 6º inciso VII e Art. 46.
- Portaria SGD/MGI nº 7.076/2024, art. 3º — exigência de domínio oficial para credenciais de produção.
- Roteiro técnico oficial gov.br: [acesso.gov.br/roteiro-tecnico](https://acesso.gov.br/roteiro-tecnico).
- Documentação Identidade Digital para Gestores Públicos: [gov.br/governodigital — identidade digital](https://www.gov.br/governodigital/pt-br/identidade/identidade-digital-para-gestores-publico).
- Story de implementação: #218 (cpf-matcher SPI integrado, mergeada via PR #228).
- Issue espelho deste repositório: #231.
- Repositório que builda e publica a imagem composta + JAR: [`unifesspa-edu-br/uniplus-keycloak-providers`](https://github.com/unifesspa-edu-br/uniplus-keycloak-providers) — workflow `release.yml` dispara em tag `v*.*.*` e publica em GHCR (`ghcr.io/unifesspa-edu-br/uniplus-keycloak`) com tags semver `1.0.2`, `1.0`, `1.x`, `latest`. Política de pinning recomendada documentada em [`docker/keycloak/README.md`](../../docker/keycloak/README.md#imagem-do-keycloak).
- **Origem:** revisão da ADR interna Uni+ ADR-029 (não publicada). Esta promoção mantém o escopo gov.br; a federação institucional via LDAP/AD para servidores fica fora desta ADR e deverá ser tratada em ADR própria quando entrar no backlog.
