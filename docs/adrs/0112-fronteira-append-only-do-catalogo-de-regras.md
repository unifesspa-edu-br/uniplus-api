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

O catálogo `regras_do_catalogo` (Story #772) é uma biblioteca de regras tipadas,
versionadas e content-addressable que a configuração do Processo Seletivo
referencia por `(codigo, versao, hash)`. A configuração congelada de um certame
(`VersaoConfiguracao`, ADR-0104) preserva essa referência para tornar o resultado
reprodutível e auditável (RN08). A definição inclui a `declaracao_da_regra`, cujo
conteúdo integra o hash da regra.

Entre as 18 regras semeadas, `RECURSO-MULTI-INSTANCIA` conflacionava dois
conceitos: a **gestão** de uma segunda instância de recurso — que o Uni+ não
exerce, pois o julgamento em instância superior corre fora do sistema — e a
**período de suspensividade**, que é real e precisa ficar congelada por edital. O
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

<!-- Cláusula alterada pela Emenda 3 (2026-11-20), no fim deste documento: sai o prazo de efeito suspensivo por instância da regra de prazo. -->
No caso concreto que motivou esta ADR, `RECURSO-MULTI-INSTANCIA` tinha zero
referências congeladas (nenhuma `VersaoConfiguracao`, nenhum consumidor de
`TipoRegra.RegraPrazoRecurso`, sem produção), e por isso foi substituída por
`RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO` — que preserva a período de suspensividade, agora
por instância, e inicia a contagem do prazo na publicação de um ato.

## Consequências

### Positivas

- O catálogo não carrega vocabulário morto: uma regra que nunca foi usada não
  precisa sobreviver como registro histórico enganoso.
- A proteção do append-only permanece intacta onde importa — a partir do primeiro
  congelamento, a linha é imutável.
- A fronteira é objetiva e testável, não um julgamento subjetivo por PR.

### Negativas

- Cria uma período em que o comportamento de correção difere (substituição antes,
  append-only depois) — exige que a verificação seja executada, não presumida.
- Reusar o `Id` técnico numa substituição pode surpreender quem espera que `Id`
  seja identidade de negócio; a identidade de negócio é `(codigo, versao)`.

### Neutras

- A regra vale para o `regras_do_catalogo`; outros catálogos append-only decidem sua
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

- Bom, porque a regra é uniforme e não tem período de exceção.
- Ruim, porque preserva vocabulário nunca usado como se fosse fato, e o nome
  errado de uma regra ficaria no catálogo para sempre.

### Substituição condicionada ao não-congelamento

- Bom, porque protege o que é fato e corrige o que ainda é só vocabulário.
- Ruim, porque introduz uma condição que precisa ser verificada mecanicamente
  antes de cada substituição.

## Mais informações

- RN08 (congelamento de parâmetros por edital).
- ADR-0104 (a vigência da configuração ordena as versões).
- Story #772 (a biblioteca `regras_do_catalogo`).

## Emenda 1 (2026-09-03) — reescrita de versão, não só de conteúdo, enquanto não há produção

A fronteira original tratava a correção como **substituição de conteúdo sob a mesma
identidade** `(codigo, versao)` — o exemplo concreto (`RECURSO-MULTI-INSTANCIA` →
`RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO`) preserva `codigo` e `versao`, só troca o `Id` técnico e o
conteúdo. A reescrita das cinco regras de `REGRA_DISTRIBUICAO_VAGAS` para uma única versão
`v1` é uma correção mais ampla: além de reescrever conteúdo (o rol de modalidades admitidas
de cada regra), ela **muda a identidade `(codigo, versao)`** de duas delas —
`DISTRIBUICAO_LEI_12711` e `DISTRIBUICAO_INSTITUCIONAL` saem de `v2` para `v1`.

A fronteira já cobria isso em espírito — "enquanto nenhuma configuração congelada referenciar
uma linha do catálogo por `(codigo, versao)`, essa linha pode ser corrigida por substituição"
não distingue substituir o conteúdo de substituir a versão — mas o texto original só
exemplificava o caso mais estreito. Esta emenda o torna explícito: **a fronteira do
append-only é sobre referência congelada, não sobre qual campo da linha muda**. Sem
produção, `(DISTRIBUICAO_INSTITUCIONAL, v1)` pode passar a designar conteúdo diferente do
que `v1` designava antes de ter sido apagada por uma migration anterior — o par não estava
congelado em nenhuma `VersaoConfiguracao`, então reusar a identidade não viola RN08. A
verificação mecânica continua a mesma: cruzar `VersaoConfiguracao` (e o rascunho vivo em
`configuracoes_de_vagas`, ADR-0061) contra `(codigo, versao)` antes de qualquer
`DELETE`/`INSERT` que mude a linha.

Esta emenda também autoriza o que a fronteira original não previa: a migration que aplica a
reescrita pode descartar **processos inteiros** que referenciem a regra substituída — não só
a linha do catálogo. Sem produção, o `versoes_configuracao → processos_seletivos` sendo
`DeleteBehavior.Restrict` deixaria o processo publicado sem versão alguma se a migration
apagasse só o catálogo; a emenda estende a fronteira para cobrir a cadeia inteira (versão
congelada → filhos por cascade → processo → ato correspondente em `publicacoes`, referência
por valor sem FK, ADR-0061) como uma única operação legítima enquanto não há produção.

## Emenda 2 (2026-11-13) — a guarda do append-only é conferida no banco

Com a migration única e os scripts, a guarda de que nenhum gatilho incide sobre o `regras_do_catalogo` passa a ser um teste de integração que confere o catálogo do banco depois da subida; o append-only continua por referência congelada. Ver [ADR-0170](0170-contexto-de-persistencia-unico-para-os-modulos-do-monolito.md).

## Emenda 3 (2026-11-20) — a regra de prazo deixa de registrar efeito suspensivo por instância

A [ADR-0177](0177-efeito-suspensivo-por-periodo-segunda-instancia-e-guarda-da-vaga.md) tira da regra `RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO` o prazo de efeito suspensivo por instância, que o sistema não usava para calcular quanto dura o bloqueio e só comparava ao conferir a retificação; ao sair o campo, essa parte da comparação deixa de existir, e a forma de comparar, na retificação, as novas declarações da configuração do recurso fica para o detalhamento da issue que as cria. Em seu lugar, a configuração do recurso declara a escolha do efeito suspensivo e a guarda da vaga, e o prazo de interposição da 2ª instância. A decisão desta ADR sobre o caso concreto permanece como registro. A correção da regra segue a fronteira desta ADR e foi conferida: nenhuma versão publicada da configuração a referencia. A migration inicial só semeia a linha da regra no catálogo, nenhuma versão de configuração é semeada, e testes e exemplos citam o código sem serem versão publicada. A regra é, portanto, corrigida por substituição, com o mesmo código e a mesma versão. A partir da primeira versão publicada que a referenciar, vale o append-only estrito.
