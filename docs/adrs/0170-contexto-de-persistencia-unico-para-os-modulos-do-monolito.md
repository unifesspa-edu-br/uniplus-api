---
status: "accepted"
date: "2026-11-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0170: Os módulos internos do monólito compartilham um contexto de persistência

> **Substitui a [ADR-0108](0108-registro-do-ato-por-mensagem-duravel.md)** (registro do ato por mensagem durável).
> **Substitui a [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md)** e a
> **[ADR-0107](0107-vaga-de-linhagem-unica-por-objeto.md)** apenas nos pontos listados em "Trechos substituídos".
> O que elas decidem sobre o ato, a vaga e a trava "Publicações não conhece os domínios" continua valendo.

## Contexto e enunciado do problema

Os módulos internos do `uniplus-api-host` rodam no mesmo processo, no mesmo banco, com o mesmo usuário e a mesma instância do Wolverine ([ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md), [ADR-0087](0087-banco-isolado-para-o-contexto-de-autorizacao.md)). Mesmo assim, cada módulo tem o seu `DbContext`. Esse isolamento não protege nada em tempo de execução, porque as credenciais são as mesmas.

Ele impede, porém, a transação entre módulos: o Wolverine não admite dois `DbContext` num handler ([ADR-0108](0108-registro-do-ato-por-mensagem-duravel.md)). Por isso, fatos que o negócio exige atômicos viraram mensagem, saga, reentrega e estado pendente: o registro do ato em Publicações com o estado "registro pendente", o dado do assinante viajando na mensagem, a divulgação nascendo do consumo de `AtoNormativoRegistrado` e comandos marcados `[NonTransactional]` que gravam fora da transação do outbox.

## Drivers da decisão

- **Atomicidade entre módulos** onde o negócio a exige.
- **Fronteira de módulo verificável**, sem depender do contexto como barreira.
- **Sem estado pendente nem mensagem** para o que é um fato só.
- **Uma só configuração de persistência** (conexão, migration, histórico, idempotência).

## Opções consideradas

- **A**: Manter um contexto por módulo e a mensagem durável (ADR-0108).
- **B**: Um contexto único para os módulos internos do host, com a fronteira garantida por teste de arquitetura e pelo catálogo do banco.
- **C**: Manter os contextos e compartilhar a conexão e a transação entre eles.

## Resultado da decisão

**Escolhida: "B — um contexto único".** A opção C não resolve, porque o Wolverine continua sem admitir dois contextos num handler e o envio aninhado abre outro escopo. A opção A mantém o problema que motivou esta ADR.

### Escopo

Os módulos internos do host compartilham um contexto. Ficam de fora o portal (`PortalDbContext`, implantação própria), a API GEO, o verificador de documentos e a trilha de auditoria forense ([ADR-0086](0086-trilha-de-auditoria-com-hmac-e-cofre.md)).

### Fronteira

A fronteira entre módulos é o **módulo** (projeto, namespace e esquema), e não o contexto. Ela é garantida por:
- teste de arquitetura: nenhum módulo depende de `Domain`, `Application`, `Infrastructure` ou `API` de outro, só do `Contracts`; só `{Modulo}.Infrastructure` e o projeto de composição dependem do contexto; as configurações EF ficam em `{Modulo}.Infrastructure.Persistence`; implementação de interface do `Contracts` que só lê não grava; handler de comando não chama `ICommandBus.Send` nem `IMessageBus.InvokeAsync`;
- catálogo do banco: sem chave estrangeira entre esquemas de módulos, e toda tabela está no esquema de um módulo ou da plataforma.

A referência entre módulos continua por valor ([ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md)). Estar no mesmo contexto não autoriza chave estrangeira entre módulos.

### Composição

- Uma base abstrata no `Infrastructure.Core`, injetada pelos repositórios.
- Um contexto concreto num projeto de composição, que referencia as `Infrastructure` dos módulos e guarda a migration e a fábrica de design.
- O modelo é montado **sempre completo**, com a contribuição de cada módulo numa lista explícita: configurações EF, esquema dos tipos, funções, sequências, interceptadores e scripts. Não há composição parcial, nem em teste.
- Um `IUnitOfWork` único no lugar dos `I{Modulo}UnitOfWork`, com uma só opção de codegen. O descarte das alterações não salvas vale para o escopo inteiro.

### Persistência

- Uma connection string, uma migration inicial e uma tabela de idempotência.
- As tabelas da plataforma (idempotência e histórico de migrations) ficam no esquema `plataforma`.
- Os scripts SQL continuam nos módulos. Os objetos compartilhados (`btree_gist`, `normalizar_para_comparacao`) existem uma vez só e rodam primeiro. Script de um módulo não referencia o esquema de outro.
- Não há separação de privilégio por esquema: a pendência de usuário próprio do banco para o esquema `autorizacao` ([ADR-0087](0087-banco-isolado-para-o-contexto-de-autorizacao.md)) deixa de existir.
- `dotnet ef migrations has-pending-model-changes` roda no CI.

### Consistência entre módulos

Um fato que o negócio exige atômico entre módulos é gravado **na mesma transação**, por uma **porta de escrita** no `Contracts` do módulo dono, chamada diretamente por quem grava. Mensagem fica para efeito em sistema externo ou posterior.

- A porta **não confirma**: a gravação é de quem a chamou. Ao devolver uma recusa de negócio depois de ter mutado, quem chama **descarta as alterações não salvas** antes de devolver, porque `Result.Failure` não desfaz.
- O leitor do `Contracts` lê na transação do chamador, sem rastrear e sem gravar.

### Ordem de travas

Quando uma transação grava em mais de um módulo, ela adquire as travas nesta ordem: **recurso compartilhado** (catálogos e configuração), depois **processo**, depois **ato**. Outra ordem é defeito. A ordem é fixada em teste.

### Publicações

O módulo continua, com as tabelas no esquema `publicacoes`, a rota `/api/publicacoes` e a leitura do portal. Ele continua sem conhecer os domínios: sem coluna, chave estrangeira ou enumerado de processo seletivo, chamada ou configuração de certame.

- A mensagem de registro do ato é trocada por uma porta de escrita em `Publicacoes.Contracts`, chamada na transação de quem publica. Saem a mensagem, o handler, o estado "registro pendente", a reentrega do registro e o dado pessoal na mensagem.
- **A divulgação do certame é gravada na mesma transação** da versão e do ato, com o instante oficial definido pelo sistema ([ADR-0133](0133-divulgacao-do-certame-materializada-no-registro-do-ato.md), emenda 2; [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md), emenda 1).
- **A cópia ao acervo continua posterior** ([ADR-0132](0132-armazenamento-publico-separado-para-documento-publicado.md)). A divulgação nasce sem o endereço do edital, e um efeito durável o registra quando a cópia termina, com reentrega.
- O conflito de identificador legível e a vaga de linhagem ocupada viram **recusa síncrona e nomeada** (409), sem nada gravado.

### Trechos substituídos

- **ADR-0105:** a emenda 1 que descreve o registro no Publicações como auditoria posterior, efeito da divulgação, feito depois dela. O registro é feito na transação da divulgação. O Publicações guarda o instante que recebe e nunca o define.
- **ADR-0107:** a pré-validação da vaga por leitura antes de publicar. A própria inserção recusa, e a recusa é 409 nomeado. A estrutura da vaga, os triggers e a monotonicidade continuam.

## Consequências

### Positivas

- Os comandos voltam à transação do outbox, e a gravação em dois módulos é atômica.
- Saem a mensagem, a saga de efeitos, o estado pendente, a montagem de idempotência e de migration por contexto e o `[NonTransactional]` imposto pelo segundo contexto.
- Uma conexão por escopo, em vez de até três.

### Negativas

- A fronteira passa a depender só das travas de arquitetura e do catálogo.
- Transações mais largas exigem a ordem de travas.
- Um snapshot de migration só concentra os conflitos entre branches. Ao rebasear, a migration própria é descartada e gerada de novo, e o `has-pending-model-changes` pega a regeneração esquecida.
- O modelo completo é construído no primeiro uso, e as fixtures de teste aplicam o modelo inteiro. O tempo de subida e do `MigrateAsync` é medido.

## Confirmação

- **Teste de integração de atomicidade** entre dois módulos, com mutação real nos dois: exceção, recusa e sucesso. A exceção e a recusa não deixam resíduo nem envelope no outbox. O sucesso grava as duas mudanças e o envelope no mesmo commit.
- **Teste de ordem de travas** entre módulos.
- **ArchUnitNET**, cada regra de fronteira com um caso negativo.
- **Catálogo do banco**, com o roster completo dos módulos (inclusive `autorizacao` e `discentes`).
- **Migrations:** `has-pending-model-changes` limpo e comparação do catálogo com a migration por módulo anterior, com só as diferenças esperadas: idempotência e histórico unificados, funções deduplicadas.
- **Código gerado pelo Wolverine** comparado antes e depois.
- **Tempo de subida** medido antes e depois.

## Mais informações

- [ADR-0004](0004-outbox-transacional-via-wolverine.md) — outbox transacional
- [ADR-0042](0042-application-nao-depende-diretamente-de-dbcontext.md) — a aplicação não depende do contexto
- [ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md) — topologia do monólito
