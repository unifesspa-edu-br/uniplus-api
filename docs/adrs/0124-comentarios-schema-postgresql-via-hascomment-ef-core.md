---
status: "accepted"
date: "2026-08-11"
decision-makers:
  - "Tech Lead"
consulted:
  - "CTIC"
informed:
  - "Equipe Plataforma"
---

# ADR-0124: Comentários do schema PostgreSQL via metadados do modelo EF Core

## Contexto e enunciado do problema

O `uniplus-api` não tem um padrão declarado para documentar tabelas e colunas do
PostgreSQL. Parte do modelo já usa `HasComment` do EF Core — `SyncRunConfiguration`
e `VinculoDiscenteRecordConfiguration` (Discentes), `ProcessoSeletivoConfiguration`
e `EtapaProcessoConfiguration` (Seleção) — mas sem decisão registrada, nada obriga
o padrão nem impede que a próxima configuração documente por outro caminho (SQL
manual, comentário só no código C#, ou nenhuma documentação).

A cobertura retroativa de todas as tabelas e colunas hoje sem comentário pertence
à issue #1057, fora do escopo desta ADR — aqui só o padrão que aquela entrega
deverá seguir.

## Drivers da decisão

- Cada tabela/coluna do schema tem exatamente uma fonte de verdade sobre sua
  documentação — sem lista paralela que diverge silenciosamente do shape real.
- O metadado de schema deve nascer na migration automaticamente, sem passo manual
  esquecível nem sincronização à parte.
- Comentário do PostgreSQL é visível a **qualquer usuário conectado ao banco** —
  nunca pode carregar segredo, credencial, valor real de dado pessoal ou detalhe
  crítico de segurança.
- Preferir a ferramenta que já governa todo o pipeline de schema do projeto
  (EF Core / Npgsql, `docs/guia-banco-de-dados.md`, ADR-0054) em vez de introduzir
  um mecanismo paralelo.

## Opções consideradas

- `HasComment` no modelo EF Core (`ToTable`/`Property`), com a migration gerada
  pelo provider Npgsql materializando o comentário no PostgreSQL.
- SQL manual (`COMMENT ON TABLE`/`COMMENT ON COLUMN`) em toda migration,
  independente do que o modelo já declara.
- Dicionário de dados externo (planilha, wiki, ferramenta de catalogação).

## Resultado da decisão

**Escolhida:** "`HasComment` no modelo EF Core", porque o metadado nasce no mesmo
lugar que já declara tipo, tamanho, nulidade e relacionamento da coluna, e o
provider Npgsql o materializa como `COMMENT ON` na migration gerada —
automaticamente, sem passo manual e sem lista paralela para manter sincronizada.

Toda tabela e coluna representada por uma `IEntityTypeConfiguration<T>` documenta
via `builder.ToTable("...", table => table.HasComment("..."))` para a tabela e
`builder.Property(...).HasComment("...")` para toda coluna mapeada — sem exceção
por julgamento de "relevância", que criaria discricionariedade sobre o que fica
documentado. SQL manual
com `COMMENT ON` fica restrito a objetos que o modelo do EF Core não representa
(schema, extensão, view não mapeada) ou a limitação comprovada do provider Npgsql
para um caso específico — sempre dentro de uma migration versionada, com a
justificativa registrada em comentário no código da própria migration, nunca como
atalho de conveniência para o caso comum. Dicionário de dados externo é descartado
como fonte de verdade: diverge do schema real na primeira migration que mudar uma
coluna sem que alguém lembre de atualizar o documento à parte, e duplica
manutenção que o EF Core já faz de graça a cada `dotnet ef migrations add`.

O padrão de conteúdo do comentário: pt-BR, descrevendo propósito, semântica de
negócio, origem (quando snapshot-copy ou congelamento), unidade (quando o valor
for grandeza sem escala óbvia pelo tipo ou nome — ex.: peso, prazo, percentual) e
nulabilidade/ciclo de vida quando essas informações não são óbvias pelo tipo ou
nome da coluna — nunca
repetindo o nome da tabela/coluna como paráfrase (`SyncRunConfiguration.cs` e
`VinculoDiscenteRecordConfiguration.cs` já seguem essa forma e servem de
referência, junto de `ProcessoSeletivoConfiguration.cs` e
`EtapaProcessoConfiguration.cs`). Sobre o limite de sensibilidade:
`VinculoDiscenteRecordConfiguration.CpfCiphertext` já ilustra o padrão correto —
o comentário descreve que a coluna guarda um envelope cifrado (algoritmo, forma)
sem nunca aproximar-se do valor em si; a mesma disciplina vale para qualquer
coluna que carregue dado pessoal, sensível ou segredo de aplicação. Alterar ou
remover documentação de schema é sempre migration nova — nunca `UPDATE` manual em
produção, nunca edição do comentário fora do fluxo `dotnet ef migrations add` —
para manter modelo, snapshot e schema sempre sincronizados, o mesmo invariante que
já rege qualquer outra mudança de shape no projeto.

## Consequências

### Positivas

- Um só lugar declara tipo, nulidade e documentação da coluna — quem revisa o PR
  vê os três juntos, sem alternar de arquivo.
- Toda migration que adiciona, renomeia ou redefine uma coluna já é o lugar
  natural para documentá-la ou redocumentá-la; nada externo para manter em dia.
- Introspecção de schema (`\d+ tabela` no `psql`, ferramentas de catalogação que
  leem `pg_description`) mostra a documentação sem depender de link para outro
  sistema.

### Negativas

- Objeto de banco fora do modelo do EF Core (view não mapeada, schema, extensão)
  não tem mecanismo declarativo — exige SQL manual, com a disciplina de
  justificativa por escrito para não virar rota de escape para o caso comum.
- Comentário de schema é público a qualquer usuário conectado ao banco — quem
  escreve precisa lembrar do limite de sensibilidade em toda edição; não há gate
  automático que impeça um dado sensível de entrar num comentário, a mitigação
  depende de revisão de PR.

### Neutras

- A cobertura retroativa das tabelas e colunas hoje sem comentário é trabalho
  separado (#1057); esta ADR não exige nenhuma migration própria.

## Confirmação

- Revisão de PR confere que toda `IEntityTypeConfiguration<T>` nova ou alterada
  documenta tabela e toda coluna mapeada via `HasComment`, e que nenhum
  `COMMENT ON` manual aparece numa migration sem comentário de justificativa ao
  lado.
- A issue #1057 (cobertura retroativa) usa esta ADR como padrão de aceite: toda
  tabela/coluna do schema existente termina com o `HasComment` correspondente.

## Prós e contras das opções

### `HasComment` no modelo EF Core

- Bom, porque é gerado junto com a migration — nunca fica dessincronizado do
  shape real da coluna.
- Bom, porque fica no mesmo arquivo que já declara o resto do mapeamento — uma
  leitura, um lugar.
- Ruim, porque não cobre objeto que o modelo não representa.

### SQL manual (`COMMENT ON`) em toda migration

- Bom, porque cobre qualquer objeto, mapeado ou não.
- Ruim, porque duplica informação que o modelo já tem — comentário e definição da
  coluna divergem na primeira migration que mudar uma sem a outra.
- Ruim, porque não há guarda automática contra escrever um `COMMENT ON` que
  `HasComment` já cobriria, gerando os dois ao mesmo tempo.

### Dicionário de dados externo

- Bom, porque desacopla a documentação de um deploy de schema.
- Ruim, porque é a definição de fonte paralela — diverge do schema real na
  primeira mudança não replicada manualmente, e ninguém é obrigado a mantê-lo ao
  evoluir uma migration.
- Ruim, porque introduz ferramenta/processo novo sem necessidade, quando o EF
  Core já resolve o caso comum sem custo adicional.

## Mais informações

- [PostgreSQL — COMMENT](https://www.postgresql.org/docs/current/sql-comment.html)
- [EF Core 10 — `HasComment`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.relationalpropertybuilderextensions.hascomment?view=efcore-10.0)
- `docs/guia-banco-de-dados.md` — fluxo de migrations do projeto (ADR-0054).
- ADR-0121 — criptografia de dados sensíveis em repouso (referência para o limite
  de sensibilidade em `CpfCiphertext`).
- Issue #1057 — cobertura retroativa do schema existente, consome esta ADR como
  padrão de aceite.
- Exemplos atuais: `SyncRunConfiguration.cs`, `VinculoDiscenteRecordConfiguration.cs`,
  `ProcessoSeletivoConfiguration.cs`, `EtapaProcessoConfiguration.cs`.
