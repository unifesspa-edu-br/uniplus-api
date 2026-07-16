---
status: "accepted"
date: "2026-07-16"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
  - "P.O. CRCA"
informed:
  - "Equipe Uni+"
---

# ADR-0115: O quadro de vagas é output derivado da configuração de distribuição, materializado e congelado na mesma operação que os insumos

## Contexto e enunciado do problema

O bloco `vagas` do envelope canônico (ADR-0100, ADR-0109) é hoje um dos 5 blocos ainda sem dono: `SnapshotPublicacaoCanonicalizer` grava o literal `{"status":"nao_construido"}`, e `PublicacaoSnapshotPersistenciaTests.Snapshot_ContemBlocosCanonicos` asserta isso como esperado. O que existe hoje são só os **insumos**: `ConfiguracaoDistribuicaoVagas` (uma por oferta de curso) congela `VoBase`, `Pr`, a referência à regra de distribuição e a referência demográfica (snapshot-copy do Censo, ADR-0061), mais as `ModalidadeSelecionada` da oferta. A própria entidade documenta que não modela o `QuadroDeVagas` — "output derivado... responsabilidade do motor de cálculo (incremento futuro)".

Sem o quadro materializado e congelado, o edital publicado não diz quantas vagas há por modalidade, não há como provar que o certame respeitou a Lei 12.711/2012 (redação da Lei 14.723/2023), o gate de consequência do indeferimento (documentos exigidos) não tem a linha `(oferta × modalidade)` sobre a qual raciocinar, e o envelope nunca fecha as suas 17 chaves.

Dois ramos de regra coexistem no catálogo (`rol_de_regras`), já semeados e sem consumidor: `DISTRIB-VAGAS-LEI-12711` (a quantidade de cada sub-reserva é **calculada** pela fórmula legal a partir de `VoBase`/`Pr`/percentuais demográficos) e `DISTRIB-VAGAS-INSTITUCIONAL` (a quantidade de cada modalidade é **fixada** integralmente pelo edital, sem cálculo). Uma terceira regra, `RECONCILIACAO-VAGAS-ART11-PU`, rege a capagem em `VO` na escassez e a prioridade legal entre sub-reservas — também semeada, também órfã.

Falta decidir: (1) onde o quadro vive no agregado, (2) se ele nasce por um comando próprio ou junto com os insumos, e (3) como os dois ramos convivem sob o mesmo tipo de saída sem o código ramificar por tipo de processo.

## Drivers da decisão

- **A chave do quadro é `(oferta de curso × modalidade)`.** A oferta já é a identidade do pai (`ux_configuracoes_distribuicao_vagas_processo_oferta`), e a modalidade já é única dentro dele (`ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada`). Uma segunda fonte de verdade da mesma chave, pendurada em outro lugar, poderia divergir do pai.
- **RN08 (congelamento por edital).** O quadro, uma vez publicado, não pode ser recalculado a partir do cadastro vivo — precisa ser prova reproduzível a partir dos **insumos congelados**, não do cadastro de hoje.
- **A prova não pode ser tautológica.** Se o insumo e o resultado fossem o mesmo dado, "recomputar e comparar" não provaria nada. Insumo (`voBase`, `pr`, referência demográfica, quantidades declaradas) e output (o quadro) precisam ser blocos congelados **separados**.
- **Nenhum código pode ramificar por tipo de processo.** Um PSIQ 100% afirmativo e um SiSU convencional têm de ser cobertos pela **mesma** factory — a diferença vem inteiramente do que o cadastro exige, nunca de um `if (tipo == X)`.
- **O motor de Classificação consome o quadro; não o produz.** O quadro é o estoque inicial de cada concorrência — a fronteira com a cascata de remanejamento (que **move** vaga não preenchida entre modalidades) precisa ficar explícita para não colidir com trabalho futuro.

## Opções consideradas

- **A** — `VagaOfertada` como coleção da raiz `ProcessoSeletivo`, correlacionada a `ConfiguracaoDistribuicaoVagas` por chave estrangeira lateral.
- **B** — `VagaOfertada` como filha de `ConfiguracaoDistribuicaoVagas` (mesmo padrão de `ModalidadeSelecionada`), calculada/fixada dentro da própria factory `Criar`.
- **C** — Comando dedicado (`CalcularQuadroDeVagas`) executado depois de `DefinirDistribuicaoVagas`, com estado intermediário "distribuição definida, quadro pendente".

## Resultado da decisão

**Escolhida: B — `VagaOfertada` é filha de `ConfiguracaoDistribuicaoVagas`, e o quadro nasce dentro da mesma factory `Criar` que valida os insumos**, nunca por um comando separado.

`ConfiguracaoDistribuicaoVagas.Criar` passa a ser o único ponto que produz `VagaOfertada`: resolve o ramo pelo código da regra de distribuição (`Lei12711` ou `Institucional`) e, dentro dele, chama a calculadora pura do ramo federal ou aceita as quantidades fixadas do ramo institucional. Os dois ramos são **dois caminhos dentro da mesma factory**, não dois tipos ou duas hierarquias — a única coisa que varia entre um PSIQ e um SiSU é qual regra o cadastro associa à oferta, nunca o código que a interpreta. Não existe "calcular quadro" como operação de segunda classe: redefinir a distribuição substitui config + modalidades + quadro inteiros, na mesma transação, sem estado intermediário.

O cálculo do ramo federal é um **domain service puro** (`CalculadoraQuadroVagasLei12711`, sem I/O, sem repositório — ADR-0042), porque a fórmula da Lei 12.711/2012 é regra de negócio determinística sobre os insumos já resolvidos, não uma leitura. Quem resolve os insumos (regra de ajuste no catálogo, referência demográfica) é o handler da Application; quem decide o número é o Domain.

## Consequências

### Positivas

- Uma única fonte de verdade para a chave `(oferta × modalidade)` — sem risco de o quadro divergir do pai.
- A garantia RN08 fica estrutural: não existe caminho de código que publique insumo sem quadro, ou quadro sem insumo — nascem e morrem juntos.
- O motor de Classificação ganha uma fronteira de leitura limpa: consome `VagaOfertada` como estoque inicial, sem tocar no cálculo que o produziu.
- A prova de reprodutibilidade (recomputar a partir do congelado e comparar) é genuína, porque insumo e output são blocos distintos no envelope.

### Negativas

- A factory de `ConfiguracaoDistribuicaoVagas.Criar` cresce em responsabilidade — passa a validar insumos **e** produzir o quadro. Mitigado por extrair o cálculo do ramo federal para um serviço puro dedicado, mantendo a factory como orquestradora, não como dona do algoritmo.
- Redefinir a distribuição é sempre "tudo ou nada" — não há como ajustar uma única modalidade sem recompor o quadro inteiro. Aceito deliberadamente: o alternativa (patch incremental) reabriria a possibilidade de estado "meio calculado".

### Neutras

- A regra de ajuste (`RECONCILIACAO-VAGAS-ART11-PU`) é referenciada e congelada por esta decisão, mas os motores de ajuste não-federais que ela também descreve (`REDUZIR_DE`, `REDUZIR_PROPORCIONAL_EM`) não são executados — ficam como referência congelada para incremento futuro, fora do escopo desta ADR.

## Confirmação

- Teste de arquitetura: `CalculadoraQuadroVagasLei12711` não referencia nenhum repositório, `DbContext` ou `TimeProvider` (ArchTest).
- Teste de integração: redefinir a distribuição de uma oferta já publicada substitui as linhas de `vagas_ofertadas` sem deixar órfãs (nenhuma linha do quadro anterior sobrevive).
- Teste de integração: recomputar o quadro a partir dos insumos congelados numa `VersaoConfiguracao` já publicada reproduz exatamente o quadro congelado, byte a byte no bloco `vagas`.
- Teste de domínio: um processo institucional com uma modalidade selecionada sem quantidade fixada é recusado por `ConfiguracaoDistribuicaoVagas.Criar`, nunca chega a gravar `VagaOfertada` parcial.

## Prós e contras das opções

### A — `VagaOfertada` na raiz, correlacionada por chave lateral

- Bom, porque desacopla o ciclo de vida do quadro do ciclo de vida da configuração por oferta.
- Ruim, porque cria uma segunda fonte de verdade para a mesma chave `(oferta × modalidade)` que o pai já garante única — divergência é possível sem um segundo mecanismo de sincronismo.

### B — `VagaOfertada` filha de `ConfiguracaoDistribuicaoVagas` (escolhida)

- Bom, porque a chave do quadro e a chave do pai são a mesma — não há necessidade de garantir sincronismo entre duas tabelas.
- Bom, porque segue o padrão já estabelecido por `ModalidadeSelecionada` no mesmo agregado.
- Ruim, porque a factory do pai cresce — mitigado ao extrair o algoritmo do ramo federal para um serviço puro.

### C — Comando `CalcularQuadroDeVagas` separado

- Bom, porque isola o cálculo como uma operação explícita, auditável isoladamente.
- Ruim, porque introduz um estado intermediário ("distribuição definida, quadro pendente") que não existe hoje e que RN08 não sabe congelar de forma coerente — uma versão poderia, em tese, ser publicada com insumo sem quadro se o segundo comando falhasse ou fosse esquecido.

## Mais informações

- [ADR-0042](0042-application-nao-depende-diretamente-de-dbcontext.md) — Repository + Unit of Work; o Domain não injeta repositório, e o cálculo puro segue essa fronteira
- [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) — a referência demográfica é snapshot-copy por valor, sem FK cross-módulo
- [ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md) — o envelope canônico, seus 17 blocos e a regra de que insumo e output vivem em blocos separados
- [ADR-0102](0102-invariantes-coerencia-processo-guard-rails-422.md) — invariante violada devolve 422 nomeado, nunca 500
- [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md) — a versão congelada é agregado próprio, append-only; é ela que prova a reprodutibilidade do quadro
- [ADR-0109](0109-envelope-canonico-v2-do-congelamento.md) — D1 (bump de `schema_version` por mudança de forma) e "Fora de escopo" (os 7 blocos ainda não construídos, cada um sua story) — `vagas` é um desses 7; esta story não bumpa a versão corrente
- Issue #848 (uniplus-api) — story que consome esta decisão
