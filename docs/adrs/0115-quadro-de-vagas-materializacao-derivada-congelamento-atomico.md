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

O bloco `vagas` do envelope canônico (ADR-0100, ADR-0109) é hoje um dos 5 blocos ainda sem dono: `SnapshotPublicacaoCanonicalizer` grava o literal `{"status":"nao_construido"}`, e `SnapshotPublicadoPersistidoTests.Snapshot_Contem33BlocosCanonicos` asserta isso como esperado. O que existe hoje são só os **insumos**: `ConfiguracaoDistribuicaoVagas` (uma por oferta de curso) congela as vagas ofertadas base, o percentual da reserva legal, a referência à regra de distribuição e a referência demográfica (snapshot-copy do Censo, ADR-0061), mais as `ModalidadeSelecionada` da oferta. A própria entidade documenta que não modela o `QuadroDeVagas` — "output derivado... responsabilidade do cálculo do quadro (incremento futuro)".

Sem o quadro materializado e congelado, o edital publicado não diz quantas vagas há por modalidade, não há como provar que o certame respeitou a Lei 12.711/2012 (redação da Lei 14.723/2023), a verificação de consequência do indeferimento (documentos exigidos) não tem a linha `(oferta × modalidade)` sobre a qual raciocinar, e o envelope nunca fecha as suas 17 chaves.

Dois ramos de regra coexistem no catálogo (`regras_do_catalogo`), já semeados e sem consumidor: `DISTRIBUICAO_LEI_12711` (a quantidade de cada sub-reserva é **calculada** pela fórmula legal a partir das vagas ofertadas base, do percentual da reserva legal e dos percentuais demográficos) e `DISTRIBUICAO_INSTITUCIONAL` (a quantidade de cada modalidade é **fixada** integralmente pelo edital, sem cálculo). Uma terceira regra, `AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO`, rege a capagem nas vagas ofertadas base na escassez e a prioridade legal entre sub-reservas — também semeada, também órfã.

Falta decidir: (1) onde o quadro vive no agregado, (2) se ele nasce por um comando próprio ou junto com os insumos, e (3) como os dois ramos convivem sob o mesmo tipo de saída sem o código ramificar por tipo de processo.

## Drivers da decisão

- **A chave do quadro é `(oferta de curso × modalidade)`.** A oferta já é a identidade do pai (`ux_configuracoes_de_vagas_processo_oferta`), e a modalidade já é única dentro dele (`ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada`). Uma segunda fonte de verdade da mesma chave, pendurada em outro lugar, poderia divergir do pai.
- **RN08 (congelamento por edital).** O quadro, uma vez publicado, não pode ser recalculado a partir do cadastro vivo — precisa ser prova reproduzível a partir dos **insumos congelados**, não do cadastro de hoje.
- **A prova não pode ser tautológica.** Se o insumo e o resultado fossem o mesmo dado, "recomputar e comparar" não provaria nada. Insumo (vagas ofertadas base, percentual da reserva legal, referência demográfica, quantidades declaradas) e output (o quadro) precisam ser blocos congelados **separados**.
- **Nenhum código pode ramificar por tipo de processo.** Um PSIQ 100% afirmativo e um SiSU convencional têm de ser cobertos pela **mesma** factory — a diferença vem inteiramente do que o cadastro exige, nunca de um `if (tipo == X)`.
- **A classificação dos candidatos consome o quadro; não o produz.** O quadro é o estoque inicial de cada concorrência — a fronteira com a cascata de remanejamento (que **move** vaga não preenchida entre modalidades) precisa ficar explícita para não colidir com trabalho futuro.

## Opções consideradas

- **A** — `VagaOfertada` como coleção de `ProcessoSeletivo`, correlacionada a `ConfiguracaoDistribuicaoVagas` por chave estrangeira lateral.
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
- A classificação dos candidatos ganha uma fronteira de leitura limpa: consome `VagaOfertada` como estoque inicial, sem tocar no cálculo que o produziu.
- A prova de reprodutibilidade (recomputar a partir do congelado e comparar) é genuína, porque insumo e output são blocos distintos no envelope.

### Negativas

- A factory de `ConfiguracaoDistribuicaoVagas.Criar` cresce em responsabilidade — passa a validar insumos **e** produzir o quadro. Mitigado por extrair o cálculo do ramo federal para um serviço puro dedicado, mantendo a factory como orquestradora, não como dona do algoritmo.
- Redefinir a distribuição é sempre "tudo ou nada" — não há como ajustar uma única modalidade sem recompor o quadro inteiro. Aceito deliberadamente: o alternativa (patch incremental) reabriria a possibilidade de estado "meio calculado".

### Neutras

- A regra de ajuste (`AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO`) é referenciada e congelada por esta decisão, mas as regras de ajuste do quadro não-federais que ela também descreve (`REDUZIR_DE`, `REDUZIR_PROPORCIONAL_EM`) não são executadas — ficam como referência congelada para incremento futuro, fora do escopo desta ADR.

## Confirmação

- Teste de arquitetura: `CalculadoraQuadroVagasLei12711` não referencia nenhum repositório, `DbContext` ou `TimeProvider` (ArchTest).
- Teste de integração: redefinir a distribuição de uma oferta já publicada substitui as linhas de `vagas_ofertadas` sem deixar órfãs (nenhuma linha do quadro anterior sobrevive).
- Teste de integração: recomputar o quadro a partir dos insumos congelados numa `VersaoConfiguracao` já publicada reproduz exatamente o quadro congelado, byte a byte no bloco `vagas`.
- Teste de domínio: um processo institucional com uma modalidade selecionada sem quantidade fixada é recusado por `ConfiguracaoDistribuicaoVagas.Criar`, nunca chega a gravar `VagaOfertada` parcial.

## Prós e contras das opções

### A — `VagaOfertada` em `ProcessoSeletivo`, correlacionada por chave lateral

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

## Emenda 1 (2026-09-03) — o ramo é um predicado sobre o código, não dois literais

A decisão fala em "resolve o ramo pelo código da regra de distribuição (`Lei12711` ou
`Institucional`)" — correto quando só duas regras de distribuição existiam. O catálogo passou
a ter cinco: duas do ramo federal (`DISTRIBUICAO_LEI_12711` e a variação
`DISTRIBUICAO_LEI_12711_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA`, que soma uma décima modalidade ao rol sem mudar a
fórmula) e três de quadro fixo (`DISTRIBUICAO_INSTITUCIONAL`, `DISTRIBUICAO_POR_ACRESCIMO_INDIGENA_E_QUILOMBOLA` e
`DISTRIBUICAO_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA`).

O texto original já descrevia corretamente **o mecanismo** — "a única coisa que varia entre
um PSIQ e um SiSU é qual regra o cadastro associa à oferta, nunca o código que a interpreta"
— só a exemplificação de dois literais ficou estreita. `ConfiguracaoDistribuicaoVagas.Criar`
decide o ramo por `RegraDistribuicaoVagasCodigo.EhRamoFederal(codigo)` (calcula pela
calculadora) e `EhQuadroFixo(codigo)` (aceita as quantidades fixadas), partição fechada sobre
todo código do catálogo — não por igualdade com um único código de cada lado. A opção B
("dois caminhos dentro da mesma factory") continua a decisão vigente; o que muda é só quantos
códigos cada caminho reconhece.

## Emenda 2 (2026-11-16) — o cadastro de reserva por chamada não faz parte do quadro

**Contexto.** O processo declara, para cada chamada com cadastro de reserva, quantas pessoas da reserva convoca por vaga (UNI-REQ-0354, UNI-REQ-0355). Nesta ADR e no código do quadro, "reserva" já designa a reserva legal e as sub-reservas da Lei 12.711/2012, o que tornaria o termo ambíguo.

**Decisão.** O cadastro de reserva por chamada é declaração própria do processo, indexada pelo número da chamada, fora da configuração da distribuição. Não é insumo nem saída do quadro: não altera o bloco `vagas` nem o hash do congelamento. As vagas que ele multiplica são as da chamada, informadas por quem convoca, e a declaração nunca lê o quadro. No código o nome é sempre "cadastro de reserva", nunca "reserva" sozinho.

**Consequências.** O quadro segue sendo o estoque da classificação. A pessoa do cadastro de reserva é convocada além das vagas, sem ocupá-las. A separação é garantida por regra de arquitetura nos dois sentidos: o cadastro de reserva não depende do quadro de vagas nem dos tipos que o materializam, e eles não dependem dele.

## Emenda 3 (2026-11-16) — o quadro é de dois ramos, e cada um declara o que o envelope grava

**Contexto.** O quadro foi descrito como um tipo único de saída. Os dois ramos, porém, produzem coisas diferentes: o da Lei 12.711/2012 calcula as quantidades e pode conferi-las contra a reserva legal; o de quadro fixo recebe as quantidades do edital e só pode ter sido ajustado por uma regra de ajuste do quadro. Um tipo único obrigava campos sem sentido em um dos lados (percentual da reserva legal num quadro que não o usa, conferência da reserva legal num quadro que não a calcula).

**Decisão.** O quadro tem um tipo por ramo: o quadro da lei de cotas carrega a conferência da reserva legal; o quadro fixado no edital carrega o ajuste que o reduziu, quando houve. O ramo é resolvido pelo código da regra de distribuição (`RegraDistribuicaoVagasCodigo.RamoDe`). O envelope grava, por oferta, `vagasOfertadasBase`, o `percentualDaReservaLegal` (ausente no quadro fixado), o `ajusteDoQuadro` e o `totalDeVagasDoEdital`; a leitura recalcula o quadro e recusa o envelope cujo bloco `vagas` diverge do recalculado. A regra de ajuste do quadro só vale no quadro fixado: um ajuste do quadro declarado na distribuição pela Lei de Cotas é recusado.

**Correção da Emenda 1.** A Emenda 1 listava como terceira regra de quadro fixo a de código `DISTRIB-VAGAS-COM-PCD-PURO`. O código vigente do catálogo é `DISTRIBUICAO_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA`, e a regra não é "PcD puro": o rol fechado é ampla concorrência e pessoa com deficiência, e a montagem é a do quadro fixado no edital, como a das outras duas.

**Consequências.** O recálculo na leitura continua provando o quadro a partir dos insumos congelados; a distinção por ramo evita campo sem sentido no envelope e torna a divergência detectável por ramo.
