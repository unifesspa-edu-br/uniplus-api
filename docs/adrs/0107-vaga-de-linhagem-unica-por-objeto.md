---
status: "accepted"
date: "2026-07-11"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
  - "P.O. CRCA"
informed:
  - "Equipe Uni+"
---

# ADR-0107: A unicidade de ato por objeto é uma vaga que a linhagem reserva, não um índice sobre o ato

## Contexto e enunciado do problema

O cadastro de tipos de ato marca alguns deles como **únicos por objeto**: um processo seletivo tem um edital de abertura, e não dois; uma chamada tem uma convocação, e não duas. Avisos e comunicados, ao contrário, repetem-se à vontade.

A ADR-0103 registrou a consequência sem resolvê-la: "a unicidade da raiz de cadeia deixa de ser um índice filtrado por literal e passa a depender de um atributo do cadastro de tipos". Faltava dizer **onde** essa unicidade é imposta — e a resposta não é óbvia, porque o predicado atravessa duas tabelas: o atributo `unico_por_objeto` e a linhagem estão no ato (`publicacoes.ato_normativo`); o objeto, no vínculo (`publicacoes.vinculo_ato_entidade`, ADR-0105). O Postgres não impõe unicidade entre tabelas.

O que se quer garantir é uma frase só:

```text
para um tipo único por objeto, um objeto é tratado por no máximo UMA linhagem de atos
```

**Linhagem**, e não ato: a segunda versão de um edital de abertura é uma retificação dele (ADR-0103), e retificações do mesmo edital pertencem à mesma linhagem — a cadeia inteira, identificada pela sua raiz. Um objeto pode e deve acumular vários atos daquela linhagem: a convocação, a retificação da convocação, a retificação da retificação.

## Drivers da decisão

- **A garantia tem de ser do banco.** Duas transações concorrentes que consultem "o objeto está livre?" e insiram em seguida passam ambas. É o mesmo raciocínio que levou a linearidade da cadeia (ADR-0103) a um índice único parcial, e não a uma verificação no handler.
- **O catálogo de tipos é editável** — o que faz do "tipo do ato" um alvo móvel, e obriga a decidir qual identidade é estável.
- **O registro é append-only** (ADR-0063): nada do que se grava aqui se muta ou se apaga.
- **Fronteira do módulo** (ADR-0105): o objeto é opaco. Não há, e não pode haver, chave estrangeira para `processo_seletivo` ou `chamada`.

## Opções consideradas

- **A**: Verificar na aplicação, antes de gravar.
- **B**: Denormalizar "este ato é raiz de cadeia" no vínculo, e criar um índice único parcial sobre `(objeto, tipo)` filtrando por raiz.
- **C**: **Uma vaga por `(objeto, tipo)`**, reservada em nome da linhagem, com índice único e verificação do histórico de atos.

## Resultado da decisão

**Escolhida: "C — a vaga que a linhagem reserva".**

A tabela `publicacoes.linhagem_unica_por_objeto` guarda uma linha por `(entidade_tipo, entidade_id, tipo_codigo)` — a **vaga** do objeto para aquele tipo de ato — apontando para a `raiz_id` da linhagem que a ocupa. O índice único sobre essa tripla é a garantia dura, e é o banco que a impõe, inclusive contra duas transações concorrentes.

Registrar um ato de tipo único por objeto, que se vincula a entidades, passa por duas verificações, e **cada uma vê o que a outra não vê**:

1. **O histórico de atos.** Um ato do mesmo tipo já vinculado ao objeto, fora desta linhagem, é conflito — e a consulta o encontra ainda que ele não tenha reservado vaga alguma. Isso importa porque `unico_por_objeto` é editável: um ato publicado quando o tipo ainda **não** era único por objeto não reservou vaga, e sem esta verificação um ato posterior, publicado depois de o atributo virar, encontraria a vaga livre — e o objeto acabaria com duas linhagens vivas, sem que ninguém tivesse feito nada de errado.
2. **A vaga.** O índice único, que fecha a corrida entre transações concorrentes — invisível a qualquer consulta, por definição.

A recusa é **409**, e não 422: o payload está coerente; o que não cabe é o estado já gravado (mesmo critério de `RaizJaRetificada` e `VigenciaSobreposta`).

Quatro triggers sustentam a estrutura contra o `INSERT` cru, no espírito dos CHECKs que o ato já tem:

- **Append-only** em `vinculo_ato_entidade` e em `linhagem_unica_por_objeto`: mutar um vínculo reescreveria de que objeto o ato tratava; mutar uma vaga transferiria o objeto de uma linhagem a outra sem que ato algum o dissesse.
- **Coerência da vaga com o ato** (deferred): o tipo tem de ser o do ato, a raiz tem de ser a raiz verdadeira da sua cadeia, o ato tem de estar vinculado ao objeto, e o tipo tem de ser único por objeto.
- **Correspondência reversa** (deferred): todo vínculo de um ato único por objeto exige a vaga daquele objeto reservada em nome da sua linhagem. É ela que faz o índice único valer alguma coisa — sem ela, gravar o vínculo e omitir a vaga (um importador, um seed, uma regressão) deixaria o objeto livre para uma segunda linhagem, e o índice, que só vê as vagas que existem, nada acusaria.

### Duas consequências que esta ADR decide, e não apenas registra

**1. O código do tipo de ato passa a ser imutável.** A vaga é chaveada por `tipo_codigo` (copiado por valor do catálogo, ADR-0075). Se o código pudesse ser renomeado, bastaria renomeá-lo para abrir uma vaga nova no mesmo objeto — e o certame teria dois editais de abertura vivos. O código já era, de fato, a identidade do tipo: é por ele que a exclusion constraint agrupa a série de vigências. Renomear uma versão a desgarraria das demais, partindo o tipo em dois. **Renomear passa a ser criar outro tipo**; nome, atributos de consequência, vigência e base legal seguem editáveis.

**2. A vaga é monotônica: ocupada, nunca se libera.** Não há revogação de ato publicado, e a vaga acompanha essa natureza. A consequência é dura e é aceita conscientemente: um ato publicado com o objeto errado — o operador digitou o identificador de outro certame — ocupa a vaga daquele objeto **para sempre**, e o certame errado fica impedido de receber um ato legítimo daquele tipo. Publicar a retificação corrige a narrativa documental, mas não devolve a vaga: a retificação pertence à mesma linhagem, e é essa linhagem que ocupa o objeto.

Enquanto não houver base em produção, o custo disso é nulo. Quando houver, será preciso um mecanismo administrativo, auditável e **append-only** — um ato de anulação de vínculo, não um `DELETE` — e ele é assunto de story própria. Registrar a limitação aqui é preferível a inventar agora um mecanismo de liberação que ninguém pediu e cuja semântica institucional (quem pode anular? com que ato?) ainda não foi decidida pelo CEPS.

## Consequências

### Positivas

- A invariante é imposta pelo banco, e não pela boa vontade do handler.
- A verificação do histórico torna a regra estável mesmo com o catálogo mudando debaixo dela.
- Retificações da linhagem que ocupa o objeto passam sem atrito, que é o que a prática faz.
- A fronteira do módulo continua de pé: a vaga é chaveada por um par opaco, sem chave estrangeira para domínio algum.

### Negativas

- Uma tabela a mais, e quatro triggers, para uma invariante que "parecia" um índice.
- Uma vaga ocupada por engano não se libera (ver acima).
- O `tipo_codigo` deixa de ser editável, o que contraria o que a story do cadastro (#798) assumira.

### Neutras

- Um ato único por objeto **sem vínculo** não reserva vaga: sem objeto, não há vaga a ocupar. A regra é *por objeto*.
- Alternar `unico_por_objeto` no catálogo não reescreve o passado — nenhum ato já publicado muda. O que a alternância muda é o que o próximo ato encontra ao ser registrado.

## Prós e contras das opções

### A — Verificar só na aplicação

- Bom, porque não acrescenta tabela nem trigger.
- Ruim, porque duas transações concorrentes verificam "livre", e ambas gravam. É exatamente a falha que a ADR-0103 fechou com um índice único; repeti-la aqui seria desaprender.

### B — Índice único parcial sobre "o ato é raiz"

- Bom, porque não acrescenta tabela.
- Ruim, porque **não fecha a invariante**. Basta publicar uma raiz sem vínculo e, na retificação dela, vincular o objeto já tratado por outra linhagem: a retificação não é raiz, escapa do filtro do índice, e o objeto acaba com duas linhagens vivas. A chave da regra é a linhagem, não a posição do ato dentro dela.

### C — A vaga que a linhagem reserva (escolhida)

- Bom, porque a chave que o banco trava é exatamente a frase que se quer garantir.
- Bom, porque a verificação do histórico cobre o que a tabela não pode saber (os atos que precedem a existência da vaga).
- Ruim, porque a vaga é estado derivado, e estado derivado precisa de triggers para não divergir do que o deriva.

## Confirmação

- **Teste de integração**: duas raízes distintas do mesmo tipo único por objeto, vinculadas ao mesmo objeto — a segunda é recusada com 409.
- **Teste de integração**: a retificação da linhagem que ocupa o objeto é aceita, e aparece na consulta do objeto.
- **Teste de integração (o furo da opção B)**: raiz publicada sem vínculo, retificada por um ato que vincula o objeto de outra linhagem — recusada.
- **Teste de integração (o furo do catálogo editável)**: ato publicado quando o tipo não era único por objeto, atributo alterado depois, novo ato de outra linhagem no mesmo objeto — recusado.
- **Teste de integração**: `INSERT` cru de vaga incoerente com o ato (tipo divergente, raiz falsa, objeto não vinculado, ato não único) é bloqueado pelo trigger.
- **Teste de integração**: `INSERT` cru de vínculo de ato único por objeto sem a vaga correspondente é bloqueado pelo trigger.
- **Teste de domínio**: o código do tipo de ato não muda no `Atualizar`.

## Mais informações

- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) — entidades forenses, append-only
- [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md) — os atributos de consequência são copiados por valor no instante do ato
- [ADR-0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) — retificação é relação; a cadeia é linear
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — o vínculo genérico ato ↔ entidade, opaco ao módulo
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — sem chave estrangeira atravessando módulo
