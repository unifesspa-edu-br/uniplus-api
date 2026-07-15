---
status: "accepted"
date: "2026-07-14"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
---

# ADR-0112: Fronteira do append-only na correção do catálogo de regras

## Contexto e enunciado do problema

O catálogo `rol_de_regras` (Story #772) é uma biblioteca de regras tipadas,
versionadas e content-addressable que a configuração do Processo Seletivo
referencia por `(codigo, versao, hash)`. A configuração congelada de um certame
(`VersaoConfiguracao`, ADR-0104) preserva essa referência para tornar o resultado
reprodutível e auditável (RN08).

Entre as 18 regras semeadas, `RECURSO-MULTI-INSTANCIA` conflacionava dois
conceitos: a **gestão** de uma segunda instância de recurso — que o Uni+ não
exerce, pois o julgamento em instância superior corre fora do sistema — e a
**janela de suspensividade**, que é real e precisa ficar congelada por edital. O
nome da regra negava o próprio conteúdo que deveria sobrar depois da correção.

A pergunta é: pode-se **corrigir** uma linha do seed substituindo-a (removê-la e
publicar outra com nome honesto), ou o append-only do catálogo obriga a
acrescentar sempre uma nova versão, preservando a linha errada para sempre? A
resposta não pode ser um caso a caso — precisa ser uma regra explícita, para não
virar precedente de mutação arbitrária do catálogo.

## Drivers da decisão

- Reprodutibilidade e auditabilidade da configuração congelada (RN08).
- Evitar poluir o catálogo com vocabulário que nenhuma configuração jamais usou.
- Não abrir precedente para mutar regras das quais um certame já depende.
- A verificação da fronteira deve ser mecânica, não um julgamento humano.

## Opções consideradas

- **Substituição incondicional** — corrigir a linha sempre que se julgar errada.
- **Append-only estrito desde sempre** — nunca remover; toda correção é uma nova
  versão que coexiste com a anterior.
- **Substituição condicionada ao não-congelamento** — corrigível por substituição
  enquanto nenhuma configuração congelada referenciar a linha; a partir do
  primeiro congelamento que a referencie, append-only estrito.

## Resultado da decisão

**Escolhida:** "Substituição condicionada ao não-congelamento", porque o
append-only existe para proteger **fatos congelados**, e uma linha de seed que
nenhuma `VersaoConfiguracao` referencia ainda não é fato — é vocabulário, e
vocabulário errado se corrige.

A fronteira é esta: enquanto **nenhuma** configuração congelada referenciar uma
linha do catálogo por `(codigo, versao)`, essa linha pode ser corrigida por
substituição (remoção da entrada errada e publicação da correta, reusando o `Id`
técnico do seed para manter o diff mínimo). A partir do **primeiro** congelamento
que a referencie, a linha vira fato e vale **append-only estrito** (RN08): o
passado não se muta, e qualquer evolução é uma nova versão que coexiste com a
anterior. A verificação é mecânica — cruza-se o conjunto de `VersaoConfiguracao`
com o `(codigo, versao)` que se pretende substituir; havendo qualquer referência,
a substituição é proibida.

No caso concreto que motivou esta ADR, `RECURSO-MULTI-INSTANCIA` tinha zero
referências congeladas (nenhuma `VersaoConfiguracao`, nenhum consumidor de
`TipoRegra.RegraPrazoRecurso`, sem produção), e por isso foi substituída por
`RECURSO-PRAZO-ANCORADO-EM-ATO` — que preserva a janela de suspensividade, agora
por instância, e ancora o prazo no instante de publicação de um ato.

## Consequências

### Positivas

- O catálogo não carrega vocabulário morto: uma regra que nunca foi usada não
  precisa sobreviver como registro histórico enganoso.
- A proteção do append-only permanece intacta onde importa — a partir do primeiro
  congelamento, a linha é imutável.
- A fronteira é objetiva e testável, não um julgamento subjetivo por PR.

### Negativas

- Cria uma janela em que o comportamento de correção difere (substituição antes,
  append-only depois) — exige que a verificação seja executada, não presumida.
- Reusar o `Id` técnico numa substituição pode surpreender quem espera que `Id`
  seja identidade de negócio; a identidade de negócio é `(codigo, versao)`.

### Neutras

- A regra vale para o `rol_de_regras`; outros catálogos append-only decidem sua
  própria política, ainda que este seja o precedente natural.

## Confirmação

- Teste de integração `RegraCatalogoSeed_SubstituirRegraReferenciada_Falha`:
  falha se alguma `VersaoConfiguracao` referenciar a regra que o seed substitui —
  a trava que torna esta fronteira executável, e não conselho.
- Fitness test `Migrations_NaoCriamGatilhoSobreRolDeRegras`: o append-only é por
  convenção, sem gatilho de banco (coerente com o XML-doc corrigido de
  `RegraCatalogo` e com `RegraCatalogoConfiguration`).

## Prós e contras das opções

### Substituição incondicional

- Bom, porque é o diff mais simples.
- Ruim, porque permitiria mutar uma regra da qual um certame já depende, quebrando
  a reprodutibilidade (RN08).

### Append-only estrito desde sempre

- Bom, porque a regra é uniforme e não tem janela de exceção.
- Ruim, porque preserva vocabulário nunca usado como se fosse fato, e o nome
  errado de uma regra ficaria no catálogo para sempre.

### Substituição condicionada ao não-congelamento

- Bom, porque protege o que é fato e corrige o que ainda é só vocabulário.
- Ruim, porque introduz uma condição que precisa ser verificada mecanicamente
  antes de cada substituição.

## Mais informações

- RN08 (congelamento de parâmetros por edital).
- ADR-0104 (a vigência da configuração ordena as versões).
- Story #772 (a biblioteca `rol_de_regras`).
