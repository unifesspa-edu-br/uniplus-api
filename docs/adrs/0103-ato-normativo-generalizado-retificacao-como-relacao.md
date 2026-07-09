---
status: "proposed"
date: "2026-07-09"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
  - "P.O. CRCA"
informed:
  - "Equipe Uni+"
---

# ADR-0103: Retificação é uma relação entre atos publicados, não um tipo de ato

## Contexto e enunciado do problema

O modelo atual representa a natureza de um edital como um enumerado: `NaturezaEdital { Nenhuma, Abertura, Retificacao }`. A entidade `Edital` carrega, além dele, `EditalRetificadoId` e `MotivoRetificacao`, e um CHECK garante a bijeção `natureza = Retificacao ⟺ EditalRetificadoId IS NOT NULL`.

Se um implica o outro, um dos dois é redundante. A pergunta é qual — e a resposta muda o modelo.

A prática institucional responde. Dois acervos completos de atos reais da Unifesspa foram analisados: o PS Canaã dos Carajás 2026 (CEPS, 20 atos) e o SiSU 2026 — Edição Única (CRCA, 25 atos, cinco chamadas). Neles:

- Um **comunicado sem número** prorroga o prazo de inscrições de um **edital**.
- Um **aviso** com numeração própria altera o horário de uma **convocação**.
- Um edital retificado **republica com o mesmo número e a mesma data de publicação**, anotando apenas a data em que a retificação ocorreu.
- O CRCA publica ao menos sete tipos de ato — convocação, homologação da análise documental, homologação de recursos, lista de espera, confirmação de interesse, aviso, comunicado — e **retifica convocações**.

Nenhum desses casos é representável por um enumerado de duas naturezas. Um comunicado não é "Abertura" nem "Retificacao": é um comunicado, que por acaso emenda outro ato.

Há ainda um custo mensurável. O índice `ux_editais_processo_abertura_unica` filtra por `natureza = 1` — o valor do enumerado **escrito no banco**. Acrescentar um tipo de ato exige migration, deploy e um novo filtro. É a mesma classe de acoplamento que o projeto proibiu ao decidir que `TipoProcesso` é rótulo, nunca ramo de comportamento.

## Drivers da decisão

- **Princípio fundacional do Uni+** — nenhuma regra ramifica comportamento por tipo em código. Adicionar um tipo de ato deve ser linha de cadastro.
- **Integridade da configuração publicada (RN08)** — a retificação de um ato que congela configuração produz nova versão congelada; a de um ato que não congela, não.
- **Fidelidade à prática institucional** — o modelo precisa representar os atos que os órgãos efetivamente publicam.
- **Auditabilidade da linhagem** — reconstruir o que valia em cada instante exige saber qual ato emendou qual.

## Opções consideradas

- **A**: Manter o enumerado e acrescentar valores conforme novos tipos de ato surgirem.
- **B**: Retificação como **relação** entre atos (`ato_retificado_id` + `motivo`), com o tipo do ato vindo de cadastro.
- **C**: Manter o enumerado e acrescentar um segundo eixo (`tipo_documento`) ao lado dele.

## Resultado da decisão

**Escolhida:** "B — retificação como relação entre atos", porque a retificação é um vínculo entre dois documentos, e não uma propriedade intrínseca de um deles. O enumerado descrevia a relação a partir de um dos seus extremos, o que só funciona enquanto houver exatamente dois tipos de ato.

O ato publicado passa a ter `ato_retificado_id` e `motivo`, com contrato simétrico: **um existe se e somente se o outro existe**. O tipo do ato vem de um cadastro versionado por vigência, e nenhum índice ou verificação carrega literal de tipo no filtro.

A invariante que protege a integridade da configuração publicada **não é** "a retificação referencia um ato do mesmo tipo" — essa é falsa, e o aviso que retifica um edital a derruba. A invariante correta é:

```text
congela(retificador) == congela(retificado)
```

É a **classe de congelamento** que protege a RN08, não o rótulo do tipo. Um ato que não congela configuração não emenda um que congela, nem o inverso.

A cadeia de retificação é **linear**: um ato é retificado no máximo uma vez, e duas retificações sucessivas **empilham na cabeça** da cadeia. Isso é o que a prática faz — o Aviso 05 prorroga o prazo já prorrogado pelo Aviso 04 e cita o Aviso 04, não o edital original. Retificar a raiz de uma cadeia já retificada é recusado, com mensagem que nomeia o ato que a retificou.

## Consequências

### Positivas

- Acrescentar um tipo de ato (`CONVOCACAO`, `HOMOLOGACAO_ANALISE`, `LISTA_ESPERA`) passa a ser **linha de cadastro**, sem migration nem deploy.
- Um aviso pode retificar um edital, e um comunicado sem número pode prorrogar um prazo — como já ocorre na prática.
- A linhagem do que foi publicado deixa de depender do número do documento, que a retificação repete.
- A verificação de integridade (`congela(retificador) == congela(retificado)`) é genérica: não enumera tipos.

### Negativas

- O enumerado `NaturezaEdital` é eliminado do domínio, da persistência e do contrato interno, com impacto em índices e possivelmente em códigos de erro.
- A equivalência do contrato HTTP de `Publicar` e `Retificar` precisa ser **provada por testes de contrato**, não afirmada.
- A unicidade da raiz de cadeia deixa de ser um índice filtrado por literal e passa a depender de um atributo do cadastro de tipos.

### Neutras

- O vocabulário institucional distingue "retificação" (republicação com o mesmo número) de "prorrogação" (ato autônomo que emenda outro por referência). O modelo trata as duas como a mesma relação; a interface e o texto gerado devem respeitar o rótulo de cada órgão.
- Sem base em produção, as migrations são decididas pelo mérito do modelo, não pelo custo de migrar dados.

## Confirmação

- **Fitness test**: nenhuma função ou índice do módulo compara o tipo de ato com um literal. O teste planta uma função que viola a regra e verifica que a detecção a acusa, antes de a ausência de detecção valer como evidência.
- **Teste de domínio**: um ato não congelante que tenta retificar um ato congelante é recusado com erro nomeado, mapeado a HTTP 422 (ADR-0102).
- **Teste de domínio**: duas retificações sucessivas empilham na cabeça; retificar a raiz já retificada é recusado.
- **Teste do princípio**: quando a Habilitação chegar, criar os tipos de ato do CRCA deve ser inserir linhas de cadastro. Se exigir alteração no domínio, esta decisão foi mal implementada.

## Prós e contras das opções

### A — Manter o enumerado e acrescentar valores

- Bom, porque não altera nada do que existe.
- Ruim, porque cada novo tipo de ato vira um valor de enumerado, isto é, **código**. Reencarna no documento a ramificação por tipo que o projeto proibiu no processo seletivo.
- Ruim, porque não representa um comunicado que retifica um edital: a natureza do ato e a sua relação com outro passam a competir pelo mesmo campo.

### B — Retificação como relação (escolhida)

- Bom, porque separa o que o ato **é** (tipo, cadastro) do que ele **faz** (emenda outro ato, relação).
- Bom, porque a integridade da RN08 passa a ser protegida pela classe de congelamento, que é genérica.
- Ruim, porque exige eliminar o enumerado e reescrever índices que dependiam dele.

### C — Enumerado mais um segundo eixo

- Bom, porque preserva o código existente.
- Ruim, porque mantém a bijeção redundante e acrescenta um terceiro estado a manter em sincronia. O erro categorial permanece, agora acompanhado.

## Mais informações

- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — referência cross-módulo por valor, sem chave estrangeira
- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) — entidades forenses, append-only
- [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md) — a configuração vigente é resolvida no instante do ato
- [ADR-0101](0101-retificacao-novo-edital-novo-snapshot-motivo.md) — retificação produz novo ato e nova versão, com motivo; o servidor infere o alvo
- [ADR-0102](0102-invariantes-coerencia-processo-guard-rails-422.md) — invariantes de domínio mapeadas a HTTP 422
- [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md) — a vigência ordena versões, não documentos
- [ADR-0105](0105-modulo-publicacoes-registro-central-dos-atos.md) — o módulo que possui o ato publicado
- Evidência empírica: acervos completos do PS Canaã dos Carajás 2026 (CEPS) e do SiSU 2026 — Edição Única (CRCA)
