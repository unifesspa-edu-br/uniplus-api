---
status: "accepted"
date: "2026-08-25"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Product Owner (CEPS)"
informed:
  - "Equipe Uni+"
---

# ADR-0126: Regime de turno da oferta de curso — `INTEGRAL` deixa de ser turno e passa a ser o regime que nomeia dois turnos

## Contexto e enunciado do problema

A [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) modelou o turno da `OfertaCurso` como atributo **único e opcional**, num domínio fechado de quatro tokens: `MATUTINO`, `VESPERTINO`, `NOTURNO` e `INTEGRAL`. A opcionalidade foi justificada pela oferta a distância, que se supunha não ter turno.

Duas premissas desse desenho caíram, ambas registradas no requisito **UNI-REQ-0137** do registro canônico de requisitos:

1. **`INTEGRAL` não é um período do dia.** É a informação de que o curso funciona em **dois** períodos — mas o rótulo não diz *quais*. Um curso integral manhã+tarde e um curso integral tarde+noite recebem o mesmo token, e o quadro de vagas do processo seletivo não consegue exibir os turnos reais ao candidato.
2. **Nenhum formato pedagógico dispensa o turno.** A oferta a distância e a semipresencial também funcionam em turnos específicos; a nulidade que a ADR-0066 admitia não corresponde à realidade acadêmica.

Sem produção e com a tabela `oferta_curso` vazia no ambiente de homologação, a correção pode ser feita de uma vez, sem etapa de compatibilidade.

## Drivers da decisão

- O quadro de vagas precisa exibir **quais** turnos o curso ocupa, não um rótulo agregador.
- O vocabulário de turno deve descrever período do dia, e só isso — sem um token que signifique "dois períodos".
- A obrigatoriedade precisa valer para todo formato pedagógico, sem caminho de exceção.
- O token `REGULAR` já é usado por `OfertaCurso.ProgramaDeOferta`; o novo campo não pode criar dois `REGULAR` de sentidos distintos na mesma entidade.
- O banco deve recusar por conta própria as combinações que o agregado recusa (defesa em profundidade, como já ocorre com programa, formato e base legal).

## Opções consideradas

- **Manter `INTEGRAL` como turno e tornar o campo obrigatório.**
- **Substituir o turno único por `RegimeDeTurno` declarado + coleção de turnos** (esta ADR).
- **Inferir o regime pela quantidade de turnos informados**, sem campo próprio.

## Resultado da decisão

**Escolhida:** "Substituir o turno único por `RegimeDeTurno` declarado + coleção de turnos", porque é a única que faz o dado dizer quais períodos o curso ocupa mantendo o vocabulário de turno íntegro.

A `OfertaCurso` passa a ter dois campos obrigatórios no lugar do `Turno` opcional:

- **`RegimeDeTurno`** — domínio fechado `REGULAR` (um turno) ou `INTEGRAL` (dois turnos distintos). O nome é `regimeDeTurno`, e não "tipo de oferta", porque `programaDeOferta` já usa o token `REGULAR` com outro sentido — dois `REGULAR` na mesma entidade seriam ambíguos na leitura e no contrato.
- **`Turnos`** — de um a dois tokens distintos entre `MATUTINO`, `VESPERTINO` e `NOTURNO`, devolvidos sempre em ordem canônica (matutino, vespertino, noturno), qualquer que seja a ordem de entrada.

O regime é **declarado, nunca inferido**: enviar dois turnos com `regimeDeTurno = REGULAR` é recusa, não promoção silenciosa a `INTEGRAL`. Inferir o regime tornaria impossível distinguir um erro de digitação de uma intenção, e faria o sistema aceitar como integral uma oferta que o operador não declarou como tal.

`INTEGRAL` sai do domínio de turno. `TurnoOferta` fica com três valores, e os números 1, 2 e 3 dos períodos remanescentes não mudam.

Esta ADR supersede **apenas** o ponto de turno da ADR-0066. O modelo de três níveis (curso curricular · oferta regulatória · código e-MEC por campus), o snapshot-copy da unidade ofertante e o guard condicional da base legal seguem vigentes.

## Consequências

### Positivas

- O quadro de vagas exibe os turnos reais da oferta, sem depender de um rótulo que não os nomeia.
- O vocabulário de turno volta a significar exclusivamente período do dia.
- A obrigatoriedade é uniforme: não existe caminho de código que aceite oferta sem turno.
- O banco recusa lista vazia, turno repetido e quantidade incompatível com o regime declarado.

### Negativas

- Remover `turno` da resposta é mudança incompatível. A [ADR-0028](0028-versionamento-de-api-por-media-type.md) abre exceção de janela mínima zero em estágio greenfield, invocada aqui explicitamente: `oferta-curso` não tem integrador institucional, e o único consumidor é o próprio frontend, que regenera os clients a partir do contrato. O media type permanece `v1` — uma `v2` obrigaria a servir duas representações para proteger consumidor que não existe, e a versão antiga carregaria vocabulário morto.
- A oferta passa a exigir dois campos que antes eram um opcional; todo criador de oferta precisa declará-los.

### Neutras

- Não há dado a migrar: a tabela nasce com o modelo novo, sem backfill, sem coluna de transição e sem período de coexistência.

## Confirmação

- Testes de domínio cobrem cada violação de regime e cardinalidade com wire code próprio.
- Três CHECK constraints em `oferta_curso` espelham as invariantes: domínio do regime, domínio de cada turno e cardinalidade por regime.
- `TurnosOferta.TryAnalisar("INTEGRAL", …)` é `false` — teste dedicado impede a reintrodução do token.

## Prós e contras das opções

### Manter `INTEGRAL` como turno e tornar o campo obrigatório

- Bom, porque é a mudança de menor alcance.
- Ruim, porque preserva o defeito central: o rótulo continua sem dizer quais são os dois períodos.
- Ruim, porque mantém no domínio de "período do dia" um token que não é período do dia.

### Substituir o turno único por regime declarado + coleção de turnos

- Bom, porque separa a *quantidade* de turnos (regime) dos turnos *concretos*.
- Bom, porque a coleção é verificável pelo banco em cardinalidade e distinção.
- Ruim, porque quebra o contrato de leitura e obriga o frontend a regerar os clients.

### Inferir o regime pela quantidade de turnos

- Bom, porque dispensa um campo no payload.
- Ruim, porque um turno a mais por engano vira uma oferta integral silenciosamente.
- Ruim, porque o operador perde a possibilidade de declarar intenção e ter o sistema conferindo contra ela.

## Mais informações

- Requisito canônico: `UNI-REQ-0137` — regime de turno da oferta de curso.
- [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) — superseded no ponto de turno; o restante segue vigente.
- [ADR-0028](0028-versionamento-de-api-por-media-type.md) — exceção greenfield invocada para manter o media type `v1`.
- [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md) — as violações de regime e cardinalidade acumulam no mesmo `Result`, sem retorno antecipado.
