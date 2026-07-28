---
status: "proposed"
date: "2026-07-28"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Segurança da Informação"
  - "DPO (Josiene Campos)"
informed:
  - "Equipe de desenvolvimento (uniplus-api)"
---

# ADR-0121: Criptografia de dados sensíveis em repouso, reutilizável entre módulos

## Contexto e enunciado do problema

O critério de aceite da issue `unifesspa-edu-br/uniplus-api#878` (skeleton do módulo
Discentes) declarava o CPF "criptografado em repouso (`CpfValueConverter`)". Na prática o
`CpfValueConverter` existente (`Infrastructure.Core/Persistence/Converters/CpfValueConverter.cs`)
apenas converte `Cpf ↔ string(11 dígitos)` para o EF Core — não cifra nada, e a coluna mapeada
(`varchar(11)`) nem comportaria ciphertext autenticado. O checkbox foi marcado por engano.

O mascaramento de CPF em log já está resolvido pelo `PiiMaskingEnricher` e por `Cpf.Mascarado`
(ADR-0011) — não é objeto desta decisão. Também já existe `IUniPlusEncryptionService`
(implementações Vault Transit em produção e AES-GCM local em dev/CI), usado hoje em dois
pontos — cache de resposta de idempotência (ADR-0027) e cursor de paginação (ADR-0026) — mas
em ambos os casos cifrando um payload opaco **fora** do pipeline do EF Core, nunca um campo de
entidade rastreado pelo `ChangeTracker`.

Isso expôs uma lacuna real: não existe hoje nenhum padrão para cifrar um campo de entidade em
repouso. Como a aplicação vai crescer — mais módulos, mais campos sensíveis (e-mail, telefone,
RG, dados de saúde/PcD) —, a decisão registrada aqui precisa ser um padrão reutilizável, não uma
correção pontual do CPF do módulo Discentes.

Três incompatibilidades técnicas centrais motivam a decisão:

1. `IUniPlusEncryptionService` é assíncrono e resolvido via DI; um `ValueConverter` do EF Core
   usa expressões síncronas e a convenção atual (`HaveConversion<T>()`) exige um construtor
   sem parâmetros — não há injeção de dependência nesse ponto do pipeline.
2. O tipo de coluna atual (`varchar(11)`) não comporta ciphertext autenticado (nonce + tag +
   dados cifrados); qualquer cifragem real implica migrar para `bytea`.
3. Cifra não determinística (nonce aleatório, como já faz `LocalAesEncryptionService`) impede
   `WHERE campo = valor` e índice de igualdade sobre a coluna cifrada.

Há ainda uma restrição de adoção: `ValueObjectConventions` aplica os `ValueConverter` de forma
global por tipo de Value Object (`Cpf`, `Email`, `NomeSocial`, `NotaFinal`) e convive com módulos
que ainda mapeiam esses VOs via `OwnsOne` — conflito já registrado e diferido na
[ADR-0054](0054-naming-convention-e-strategy-migrations.md). Cifragem de campo sensível não deve
reabrir essa tensão: precisa ser política por propriedade, não característica global do tipo.

## Drivers da decisão

- confidencialidade em repouso com autenticação e nonce aleatório (não determinística);
- Domain e Application sem dependência de Vault, EF Core ou Infrastructure;
- suporte a CPF hoje, e-mail/telefone/RG/dados de saúde amanhã, sem um `switch` central por tipo;
- adoção opt-in por entidade/propriedade, nunca alteração global de todo `Cpf`/`Email`;
- igualdade/busca sobre o campo cifrado somente quando um caso de uso concreto a exigir;
- comportamento aceitável de latência e throughput, inclusive em lote;
- testes unitários sem depender de Vault, e teste de integração realista com o provedor local;
- falha fechada — indisponibilidade do serviço de criptografia nunca deve resultar em gravação
  em texto claro.

## Opções consideradas

- **A. `SaveChangesInterceptor`/`IMaterializationInterceptor`** — interceptor assíncrono cifra
  antes do `SaveChangesAsync` e decifra na materialização.
- **B. `ValueConverter` com chamada bloqueante ao serviço assíncrono** — o que o próprio
  `EncryptedCpfValueConverter` escrito na branch `feature/878-skeleton-modulo-discentes` faz
  hoje, via `.GetAwaiter().GetResult()`.
- **C. Proteção explícita na fronteira de repositório/query service** — o mesmo princípio já
  usado por idempotência e cursor: a cifragem ocorre fora do pipeline de conversão do EF Core.
- **D. Cifra determinística no próprio campo** — permite `WHERE ciphertext = valor` diretamente.
- **E. Criptografia no PostgreSQL (`pgcrypto`) ou apenas no volume/disco.**

## Resultado da decisão

**Escolhida: Opção C — proteção explícita na fronteira de repositório/query service**, com
adoção opt-in por propriedade e um provedor de produção baseado em envelope encryption, porque é
a única alternativa que concilia corretamente a assincronia real do `IUniPlusEncryptionService`
com o pipeline do EF Core sem esconder I/O de rede atrás de uma chamada síncrona, e porque
mantém o Domain e a Application inteiramente alheios a Vault, EF Core e nomes de chave.

O modelo de domínio (`VinculoDiscente`, `Cpf`) permanece em texto claro apenas em memória, no
processo da aplicação. A interface de repositório exposta ao domínio continua falando a
linguagem do domínio (`Task<VinculoDiscente?> GetByIdSigaaAsync(...)`). A implementação, na
camada de Infrastructure, mapeia entre a entidade de domínio e um modelo de persistência
separado que armazena apenas o envelope cifrado (`bytea`), chamando
`IUniPlusEncryptionService.EncryptAsync`/`DecryptAsync` de forma assíncrona nesse mapeamento —
nunca dentro de um `ValueConverter` ou de um `Func` síncrono do EF Core.

A adoção é **opt-in por propriedade**, registrada explicitamente na configuração de persistência
de cada entidade — nunca uma característica global do tipo `Cpf`/`Email` via
`ValueObjectConventions`. Um novo campo sensível (Telefone, RG, dado de saúde) ganha, no máximo,
uma pequena rotina de codificação/decodificação na Infrastructure; nenhuma mudança num
`switch` central. Isso também evita reabrir o conflito `OwnsOne` × `ValueConverter` global já
diferido na ADR-0054.

Para o CPF do módulo Discentes especificamente: **precisa de índice de igualdade (blind index)**.
`VinculoDiscente` é granular por vínculo — o mesmo CPF pode aparecer em várias linhas — e o
módulo Ingresso vai consumir Discentes para detectar duplo vínculo (mesmo CPF com dois vínculos
ativos), consulta que exige localizar linhas por CPF dentro da própria tabela do Discentes.
`id_discente_sigaa` continua sendo a chave natural para o upsert da réplica em si
(`IVinculoDiscenteRepository.GetByIdSigaaAsync`), mas não resolve essa segunda consulta. Esta
decisão foi revisada em 28/07 (a versão original desta ADR presumia, incorretamente, que nenhuma
busca por CPF seria necessária). A prática se repete: praticamente todo módulo do sistema acaba
precisando de busca por igualdade sobre CPF em algum ponto — a exceção seria a regra rara, não o
padrão.

### Correlação cross-module sem chave compartilhada

A pergunta natural — "então os módulos vão comparar índice cego uns com os outros?" — já tem
resposta na arquitetura de dados existente. `ADR-0097` já proíbe FK cruzando módulo e a
arquitetura padrão de acesso cross-module é o **Reader** (ADR-0056): um módulo nunca lê a tabela
de outro diretamente, só chama uma interface que o módulo dono expõe (`I{Entidade}Reader`),
chamada in-process dentro do mesmo monólito (sem rede, sem serialização).

Isso resolve a busca por CPF entre módulos **sem exigir chave/`MatchScope` compartilhado**: o
serviço de Habilitação (Ingresso), para checar duplo vínculo, chama algo como
`IVinculoDiscenteReader.ExisteVinculoAtivoDuplicadoAsync(Cpf cpf)` — uma interface exposta pelo
próprio Discentes. O CPF em texto claro passa como parâmetro do método (chamada in-process, não é
um vazamento novo); a implementação, dentro de `Discentes.Infrastructure`, calcula o índice cego
com a **chave do próprio Discentes** e compara contra a coluna `cpf_lookup_digest` da **sua
própria tabela**. Quem chama nunca vê a chave nem o ciphertext do módulo dono — só recebe o
resultado da consulta (`bool`/DTO mínimo). O mesmo vale para o padrão alternativo de cópia
(snapshot) de dado entre módulos: se um módulo mantém cópia local com `origem_id`, o índice cego
dessa cópia é calculado com a chave do módulo que a possui, no momento em que a cópia é gravada.

Uma chave compartilhada entre módulos (`MatchScope` único) só faria diferença se alguém
comparasse índices cegos de tabelas de módulos diferentes **fora** desses dois caminhos — por
exemplo, um relatório rodando `JOIN` direto entre `discentes.vinculo_discente.cpf_lookup_digest`
e `ingresso.matricula.cpf_lookup_digest` via SQL cru, sem passar por nenhum Reader. Isso já
seria uma violação de isolamento por outro motivo (a mesma proibição de FK cruzando módulo da
ADR-0097) e não é um caminho sancionado. **Decisão: uma chave HMAC por módulo, nunca
compartilhada entre módulos** — a correlação acontece através do Reader, não através da chave.

Consequência prática ainda não implementada: `IUniPlusEncryptionService` hoje só expõe
`EncryptAsync`/`DecryptAsync` (cifra simétrica), não HMAC. Suportar índice cego exigirá uma nova
capacidade — provavelmente um `IBlindIndexService` ou extensão do serviço existente — que **não
foi construída nesta decisão**. Fica para quando a story de Habilitação (Ingresso) for planejada
e o índice cego do Discentes for de fato implementado; não há consumidor concreto hoje para
justificar antecipar essa capacidade.

Para produção, a evolução recomendada é **envelope encryption**: o Vault Transit passa a
proteger apenas uma chave de dados (DEK) versionada por propósito, mantida em cache por tempo
limitado; a cifragem em si roda localmente (AES-GCM, nonce aleatório por valor), reduzindo o
custo de uma chamada ao Vault por linha para uma chamada por carregamento/renovação de chave.
Essa otimização **não é implementada nesta decisão** — depende de key provisioning no Vault
Transit (trabalho de infraestrutura, `uniplus-infra`) ainda não feito. Enquanto isso, o padrão
descrito acima funciona corretamente chamando `IUniPlusEncryptionService` diretamente e de forma
assíncrona na fronteira do repositório — correto tanto com o provedor local (dev/CI, sem custo de
rede) quanto com Vault Transit (produção, pagando uma chamada de rede por valor até a otimização
de envelope ser adotada).

A topologia real de produção já está definida: o Vault fica na infraestrutura da própria
Unifesspa, e a aplicação roda **fora**, num datacenter Tier III via Kubernetes — ou seja, a
chamada ao Vault Transit atravessa uma fronteira de rede entre dois ambientes. Por isso o cache
da DEK não é só em memória do processo (que se perde a cada reinício de pod): o desenho é
**cache no nível do Kubernetes** — a chave sincronizada do Vault fica disponível como Secret do
cluster, com TTL de até 5 minutos, de forma que um pod novo (escalonamento, reinício) consegue
subir com a chave já cacheada mesmo que a comunicação com a Unifesspa esteja momentaneamente
indisponível ou com latência alta. O "catálogo" de chaves de dados citado nas perguntas
originais desta ADR não precisa de armazenamento/backup próprio: o Vault Transit já versiona
chaves nativamente (fonte de verdade durável); o Secret do Kubernetes é só um cache operacional
recarregável — perdê-lo degrada resiliência até a comunicação com o Vault ser reestabelecida, não
perde dado nem exige processo de restore dedicado.

## Consequências

### Positivas

- padrão reutilizável, sem transformar `CpfValueConverter`/`ValueObjectConventions` numa
  responsabilidade criptográfica global;
- compatibilidade natural com chamadas assíncronas, injeção de dependência e cancelamento —
  nenhum `.GetAwaiter().GetResult()` escondendo I/O de rede;
- ciphertext autenticado e não determinístico (`bytea`, AEAD);
- igualdade/busca suportada apenas quando um caso de uso a exigir, com o vazamento de
  frequência/igualdade documentado explicitamente nesse momento;
- caminho de evolução para envelope encryption sem mudar a interface de repositório consumida
  pelo domínio;
- testável sem depender de Vault real em toda a suíte (fake determinístico de
  `IUniPlusEncryptionService` nos testes unitários; provedor local real via Testcontainers na
  integração).

### Negativas

- entidades protegidas exigem um modelo de persistência explícito, separado da entidade de
  domínio — mais código do que mapear a entidade diretamente;
- qualquer leitura/escrita que hoje contornasse o repositório e acessasse o `DbContext` bruto
  passa a expor apenas o envelope cifrado, nunca o valor claro — reforça a necessidade de todo
  acesso passar pelo repositório;
- sem a otimização de envelope encryption, o provedor Vault Transit paga uma chamada de rede por
  valor cifrado/decifrado — aceitável para os volumes atuais, mas a ser revisitado se a réplica
  de Discentes crescer o suficiente para tornar isso um gargalo real de sincronização;
- contexto autenticado por linha/campo (AAD específico, além do nome da chave) não é suportado
  pela assinatura atual de `IUniPlusEncryptionService.EncryptAsync/DecryptAsync` — ampliar isso é
  trabalho futuro que afeta também os dois consumidores existentes (idempotência, cursor).

### Neutras

- o `CpfValueConverter` atual continua existindo, sem alteração, para as propriedades que
  deliberadamente permanecem em texto claro (uso já validado em outros módulos);
- o mascaramento de log da ADR-0011 não é alterado por esta decisão — são controles
  complementares (confidencialidade em repouso vs. exposição em log).

## Confirmação

- teste de integração (Testcontainers) provando que a coluna `bytea` da entidade protegida nunca
  contém os 11 dígitos do CPF em texto claro, mesmo inspecionando via SQL cru;
- teste de integração provando que o mesmo CPF persistido em duas linhas produz envelopes
  diferentes (nonce aleatório);
- teste de arquitetura ou revisão manual garantindo que nenhum `ValueConverter`/interceptor do
  EF Core chama `IUniPlusEncryptionService` de forma síncrona (`.Result`/`.GetAwaiter().GetResult()`).

## Prós e contras das opções

### A. `SaveChangesInterceptor`/`IMaterializationInterceptor`

- Bom, porque centraliza parte do comportamento no pipeline do EF e o caminho de gravação pode
  de fato aguardar o serviço assíncrono.
- Ruim, porque a materialização do EF e os `ValueConverter` continuam síncronos — não há um
  ponto simétrico e seguro para aguardar o Vault na leitura; alternar o mesmo membro rastreado
  entre texto claro e cifrado interfere em snapshots e detecção de mudança; consultas e updates
  em lote podem contornar o interceptor.

### B. `ValueConverter` com chamada bloqueante (`.GetAwaiter().GetResult()`)

- Bom, porque é o caminho mais direto de implementar — foi o que a branch já tentou.
- Ruim, porque bloqueia uma thread do pool numa chamada de rede real ao Vault a cada linha
  materializada; sob carga esgota o thread pool e piora exatamente o cenário de indisponibilidade
  que se quer evitar. Rejeitada.

### C. Proteção explícita na fronteira de repositório/query service (escolhida)

- Bom, porque combina naturalmente com DI, cancelamento e chamadas assíncronas; torna explícitos
  os pontos onde o texto claro entra e sai da persistência; aceita fakes pequenos em teste.
- Ruim, porque exige um modelo de persistência separado da entidade de domínio para toda
  propriedade protegida, e disciplina para impedir que caminhos alternativos acessem o
  `DbContext` bruto.

### D. Cifra determinística no próprio campo

- Bom, porque consulta e unicidade diretas sobre a coluna cifrada são simples.
- Ruim, porque revela repetições e frequência — CPF tem domínio pequeno e estrutura conhecida,
  o que facilita confirmação por dicionário a quem obtiver acesso ao banco. Não recomendada como
  padrão; exigiria decisão de segurança específica e justificativa por campo.

### E. Criptografia no PostgreSQL (`pgcrypto`) ou apenas no volume/disco

- Bom, porque criptografia de volume protege mídia e snapshots perdidos, com baixa mudança no
  domínio.
- Ruim, porque nenhuma das duas protege contra leitura lógica por credencial/administrador do
  banco, nem resolve igualdade/rotação; `pgcrypto` exige que a chave ou o texto claro cheguem ao
  servidor de banco, o que enfraquece o Vault Transit como fronteira criptográfica. Continua
  válida como defesa em profundidade complementar, não como substituta desta decisão.

## Decisões tomadas em 28/07 (fecham as questões originalmente em aberto)

A versão original desta ADR deixou quatro perguntas em aberto para Segurança da Informação e o
DPO. Revisão com o responsável técnico do projeto fechou as quatro:

1. **Busca por igualdade sobre CPF** — sim, é a regra, não a exceção: praticamente todo módulo
   do sistema acaba precisando dela em algum ponto (ex.: Habilitação, no Ingresso, consultando
   duplo vínculo no Discentes). Resolvida via **Reader cross-module (ADR-0056)** com **chave HMAC
   por módulo, nunca compartilhada** — ver seção "Correlação cross-module sem chave
   compartilhada" acima. Não presumir mais que um módulo não precisa de índice cego só porque o
   caso de uso ainda não foi mapeado.
2. **DEK em cache com TTL** — aceito, TTL de até 5 minutos. O mecanismo não é cache em memória do
   processo isolado: é **cache no nível do Kubernetes** (Secret sincronizado do Vault), desenhado
   para a topologia real de produção (Vault na infraestrutura da Unifesspa, aplicação rodando
   fora, em datacenter Tier III via Kubernetes) — garante que pods novos subam mesmo com falha de
   comunicação ou latência alta entre os dois ambientes.
3. **Fronteiras de categoria de chave** — uma chave HMAC/AEAD por **módulo e por categoria**
   (ex.: `uniplus-discentes-identificadores-aesgcm`, não uma `uniplus-pii-identifiers-aesgcm`
   única compartilhada entre módulos) — consequência direta da decisão 1: como a correlação
   cross-module passa pelo Reader, nenhum módulo precisa comparar índice cego com o de outro, e
   chave por módulo é suficiente e mais isolada. **Ainda em aberto**: o desenho exato de
   mounts/policies do Vault por ambiente (dev/homologação/produção) — proposta é seguir a mesma
   fronteira de cluster/namespace que o Kubernetes já vai ter por ambiente, mas isso precisa ser
   confirmado com quem desenha o provisionamento no `uniplus-infra`.
4. **Catálogo de chaves embrulhadas** — não precisa de armazenamento/backup próprio. O Vault
   Transit já é a fonte de verdade durável (versiona chaves nativamente); o Secret do Kubernetes é
   só cache operacional recarregável.

Consequência ainda não implementada: o índice cego do Discentes (coluna `cpf_lookup_digest`,
serviço de HMAC — hoje `IUniPlusEncryptionService` só cifra/decifra, não calcula HMAC — e o
`IVinculoDiscenteReader` que a Habilitação vai consumir) fica para quando aquela story for
planejada. Registrar aqui evita repetir a análise, não antecipa a implementação sem consumidor
concreto.

## Mais informações

- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Persistence/Converters/CpfValueConverter.cs`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Persistence/Converters/ValueObjectConventions.cs`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Cryptography/IUniPlusEncryptionService.cs`
- `src/shared/Unifesspa.UniPlus.Infrastructure.Core/Logging/PiiMaskingEnricher.cs`
- [ADR-0011](0011-mascaramento-de-cpf-em-logs.md) — mascaramento de CPF em log (controle
  complementar, não alterado por esta decisão)
- [ADR-0026](0026-paginacao-cursor-opaco-cifrado.md) e
  [ADR-0027](0027-idempotency-key-store-postgresql.md) — usos existentes de
  `IUniPlusEncryptionService`, ambos fora do pipeline do EF Core
- [ADR-0054](0054-naming-convention-e-strategy-migrations.md) — conflito diferido entre
  `ValueObjectConventions` e `OwnsOne`
- issues `unifesspa-edu-br/uniplus-api#878`, `#874`, `#876`
