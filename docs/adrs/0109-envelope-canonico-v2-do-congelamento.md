---
status: "accepted"
date: "2026-07-13"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
informed:
  - "Equipe Uni+"
---

# ADR-0109: Contrato do envelope canônico do congelamento (v2)

## Contexto e enunciado do problema

O congelamento da configuração (RN08) materializa-se num único artefato: os **bytes canônicos** de `VersaoConfiguracao.ConfiguracaoCongeladaCanonica`, dos quais o hash e o `jsonb` de consulta são derivados dentro da factory. Esses bytes são produzidos pelo canonicalizador, que monta um envelope de **17 chaves**. Sete delas são hoje o literal `{"status":"nao_construido"}`: `vagas`, `documentosExigidos`, `formulario`, `cascataRemanejamento`, `divulgacao`, `cronogramaFases` e `identidadesUnidade`.

**Todas as stories restantes da Feature #40 escrevem nesse envelope.** Sem um contrato fechado, cada uma responderia por conta própria a cinco perguntas que precisam ter **uma** resposta — e a segunda story re-litigaria a decisão da primeira. Só que o envelope é append-only: **o passado não se muta**. Uma resposta errada na primeira story não se corrige na segunda; ela fica congelada em documento com peso jurídico.

E há um agravante verificado: **o OpenAPI não protege o envelope.** `SnapshotVigenteDto.Configuracao` é declarado como `JsonNode`, e o schema gerado na baseline é literalmente `"JsonNode": {}` — um objeto livre. Um stub virando objeto rico, uma chave nova, uma troca de `null` explícito por omissão: **nada disso aparece no diff da baseline**. O envelope — a evidência que sustenta o resultado do certame perante mandado de segurança — era, até esta ADR, **o único contrato do repositório sem gate de regressão**.

## Drivers da decisão

- **Reprodutibilidade.** A mesma configuração tem de produzir sempre os mesmos bytes. É o que permite provar, anos depois, o que valia quando o ato foi praticado.
- **O passado não se muta.** Versões já congeladas não são recalculadas. Qualquer decisão aqui vale para o futuro; o histórico permanece com os seus bytes, o seu hash e a sua `schema_version` originais.
- **Uma resposta, não seis.** Seis stories escrevem no envelope. O contrato precisa preceder todas.
- **Mudança visível.** Se o envelope muda, alguém tem de ver. Um gate que não falha não é gate.

## Resultado da decisão

Fecha-se o contrato do envelope em nove decisões (D1–D9) **antes** de qualquer story escrever nele, e o congelamento passa a ter gate de regressão próprio — golden fixtures byte a byte, com canários que provam que o gate falha quando deve falhar.

### D1 — `schema_version` sobe a cada mudança de **forma**

Acrescentar chave **ou** um stub virar conteúdo real são mudanças de forma. Não há CHECK de `schema_version` no banco: o bump é livre e não pede migration. A versão corrente passa de `"1.0"` para **`"1.1"`**.

**Consequência:** dois envelopes estruturalmente diferentes nunca compartilham a mesma `schema_version` — inclusive dentro da mesma cadeia de versões de um certame, onde a versão 1 (pré-story) e a versão 2 (pós-story) legitimamente convivem.

### D2 — Golden fixtures são obrigatórias, uma por `schema_version`

A fixture compara **byte a byte**. Um teste de política falha o build quando a `schema_version` corrente não tem fixture correspondente — **bumpar sem congelar a forma nova é impossível**.

**Consequência:** o envelope ganha o gate que o OpenAPI não dá. A regeneração é explícita (`UPDATE_ENVELOPE_FIXTURE=1`), e o diff da fixture entra no PR — a mudança do envelope passa a ser **visível na revisão**, que é todo o ponto.

### D3 — A fixture normaliza ids gerados, e só isso

Guids v7 gerados pela entidade variam a cada execução: são identidade técnica, não conteúdo. A fixture os normaliza **declaradamente** e compara todo o resto byte a byte.

**Consequência:** a fixture é possível sem deixar de proteger o que é conteúdo. Três **canários** provam que ela protege de fato: acrescentar uma chave, trocar um stub por objeto, e substituir `null` explícito por omissão — os três **fazem a fixture falhar**. Sem os canários, uma fixture sempre-verde passaria por gate.

### D4 — `null` explícito é a forma canônica da ausência

`null` explícito é preservado — no envelope **e** nos campos de topo do hash de entidade. A **única** exceção é o interior do predicado (`PredicadoObrigatoriedade`), que é serializado com `CanonicalOptions` (`WhenWritingNull`) e portanto omite os seus opcionais.

O item 4 da ADR-0100 ("campo opcional sem valor é omitido") foi lido como regra geral. **Não é** — e nunca foi, nem no caminho de entidade: `HashCanonicalComputer.Compute` escreve `"portariaInternaCodigo": null` e `"vigenciaFim": null` no `JsonObject` de topo, e esses nulls **entram no hash**. `CanonicalOptions` governa a serialização do predicado, não a montagem do payload que o contém. A ADR-0100 recebe uma **emenda** que corrige a descrição.

**Consequência:** a emenda **descreve**, não altera. Nada nos bytes muda. `CanonicalOptions` permanece byte-idêntico — mexer nele, ou passar a omitir os nulls de topo, mudaria os hashes de `ObrigatoriedadeLegal` já gravados e quebraria a `UNIQUE (hash)` parcial e a trilha forense. Um teste congela o hash de uma regra de referência **por valor literal**, para que essa quebra não passe em silêncio. A escolha é coerente com o item 10 da própria ADR-0100 ("todo bloco canônico está presente"): se o bloco está sempre lá, o campo do bloco também está — com `null` quando não tem valor.

### D5 — O gate de conformidade vale para **publicar e retificar**

`ProcessoSeletivo.PendenciaDeConformidade()` é a **fonte única** do checklist. As duas transições a consultam e recusam com o **mesmo** `DomainError`. Os handlers avaliam **antes** de canonicalizar.

**Consequência:** hoje só `Publicar` avaliava. Mas a retificação também abre uma `VersaoConfiguracao` append-only e vinculante — congelar configuração incompleta ali produz um documento irreparável, exatamente como na publicação. E o gate precede a projeção para que um processo não conforme devolva o `DomainError` que o contrato HTTP promete, em vez de estourar em D8.

### D6 — O canonicalizador é uma **projeção pura**, com entrada única

`Canonicalizar(EntradaCanonicalizacao)` — um parâmetro. Sem repositório, sem `DbContext`, sem `TimeProvider`, sem `IServiceProvider`, sem `HttpClient`, e **não é assíncrono**. Travado por fitness test.

**Consequência:** dado que **não pertence ao agregado** — o catálogo de obrigatoriedades legais, o quadro de vagas — entra por um **campo novo no record de entrada**, montado pelo handler, que é quem tem os repositórios (ADR-0042). Injetar um repositório no canonicalizador seria a saída tentadora e errada: inverteria a dependência e quebraria a reprodutibilidade **em silêncio**. Acrescentar dado ao envelope também não pode significar mudar a assinatura da porta a cada story.

### D7 — Duas gramáticas de predicado, deliberadas e disjuntas

1. **Átomo DNF** — `{fato, operador, valor}` sobre o **rol de fatos do candidato**. É a gramática do gatilho da exigência documental e da condição de exibição do formulário. Elas são **a mesma coisa** e compartilham o mesmo value object.
2. **Predicado nomeado + args** — `{"predicado": "...", "args": {...}}` sobre o **estado do certame**. É a gramática da condição da cascata de remanejamento. Entra no envelope como **dado cru**, com validação de forma apenas: o rol de predicados nomeados é fechado, mas não há motor que os avalie nesta fatia.

**Consequência:** a pergunta "o gatilho do documento e a condição da cascata são a mesma gramática?" tem resposta — **não** —, e ela está registrada antes de as duas stories a responderem em separado.

### D8 — Um bloco **real** nunca emite `nao_construido`

O literal é reservado às 7 dimensões sem dono. Se uma dimensão é obrigatória, a sua ausência é **pendência de conformidade**, e o gate (D5) recusa a transição antes de a canonicalização acontecer. Chegar à projeção sem ela é invariante quebrada — **falha alto**.

**Consequência:** antes, `atendimento` e `classificacao` tinham fallback para `nao_construido`. Um processo publicado sem classificação — o bloco que determina o resultado do certame — congelaria `{"status":"nao_construido"}` **em silêncio**, num documento juridicamente vinculante. Era o pior modo de falha possível do envelope.

### D9 — Arrays sem chave natural ordenam pela **chave de conteúdo**

Onde a coleção não tem chave de negócio (as regras de eliminação: cardinalidade múltipla, duas do mesmo código são válidas), a ordenação é pelos **bytes canônicos do próprio item**.

**Consequência:** ordenar por `Id` (Guid v7) era determinístico entre leituras da mesma linha, mas **não entre configurações equivalentes** — as mesmas regras inseridas em ordem inversa recebem Guids distintos e produziriam envelopes distintos para a mesma configuração. A identidade técnica da linha vazava para dentro do hash. Agora o envelope depende **só do que ele diz**.

## Opções consideradas

- **Não bumpar `schema_version` ao preencher um stub** (o conjunto de 17 chaves não muda). Rejeitada: o campo é devolvido ao consumidor em `SnapshotVigenteDto` como discriminante, e dois envelopes estruturalmente diferentes carregariam o mesmo `"1.0"` — dentro da mesma cadeia do mesmo certame.
- **Corrigir o canonicalizador para omitir `null`, cumprindo o item 4 da ADR-0100 à letra.** Rejeitada: exigiria bumpar `algoritmo_hash` e, se aplicada ao utilitário compartilhado, mudaria os hashes de `ObrigatoriedadeLegal` já gravados. Delimitar o item 4 é mais barato e mais honesto — são dois caminhos com duas semânticas, e sempre foram.
- **Um "registro único de dimensões" com teste de reflexão**, para forçar cada story a declarar a dimensão nova num só lugar. Rejeitada: é abstração falsa. Três blocos (`distribuicao`, `modalidades`, `ofertas`) derivam da mesma coleção; período e hashes vêm de parâmetros externos; bônus e classificação são navegações singulares. Reflexão sobre coleções não garantiria `Include`, obrigatoriedade nem canonicalização — daria a sensação de proteção sem a proteção.

## Consequências

**Positivas.** O envelope ganha o primeiro gate de regressão da sua história, e ele é adversarial (os canários). O contrato precede as seis stories que vão escrevê-lo. Dois modos de falha silenciosos morrem: o bloco real congelado como stub (D8) e o Guid vazando para o hash (D9). A retificação passa a ser tão exigente quanto a publicação (D5).

**Negativas.** Cada story que preencher um bloco terá de bumpar a `schema_version` e regenerar a fixture — atrito deliberado: é o preço de a mudança ser visível. As fixtures crescem uma por versão; é histórico, não duplicação.

**Neutras.** Versões já congeladas **não são recalculadas**: uma versão gravada em `schema_version = "1.0"` permanece com os seus bytes, o seu hash e a sua versão originais. O bump vale para o que vier depois.

## Fora de escopo

- O **conteúdo** dos 7 blocos ainda não construídos — cada um é a sua story.
- O **motor** que avalia os predicados de D7. Esta ADR fixa a gramática e o freeze; quem executa é incremento (Inscrição, Homologação, Classificação).
- A **canonicalização portável** (JCS/RFC 8785). O algoritmo corrente (`canonical-json/sha256@v1`) é estável e reproduzível dentro do runtime .NET; a interoperabilidade com outro runtime não é requisito desta fatia.

## Emenda 1 (2026-08-05) — composição hierárquica da ordenação de coleções, e delegação do grafo de dependência

A issue #1069 fechou a ordenação semântica das etapas (`etapas` passa a desempatar por
`Ordem`, depois pelo conteúdo de negócio, nunca só pelo `Id`) e, ao fazê-lo, encontrou uma
leitura ambígua no código que esta emenda corrige na origem: o comentário de classe de
`SnapshotPublicacaoCanonicalizer` rotulava três camadas de ordenação distintas sob o único
nome "D9", quando a decisão original só cobre a terceira. Esta emenda declara a política
corrente por inteiro; não reabre o texto histórico acima.

### E1.1 — D9 continua restrita à chave de conteúdo; a política completa é composição hierárquica

Nenhum array do envelope escolhe UMA das três camadas abaixo — a política aplica-as em
sequência, cada uma decidindo só o que a anterior deixou empatado:

1. **`Ordem` semântica**, quando o campo existe e é atribuído por quem configura — etapas
   (`Ordem` nulo ao fim), critérios de desempate, fases do cronograma, destinos da cascata de
   remanejamento dentro de uma origem, raízes e filhos da árvore de satisfação (aqui a
   unicidade de `Ordem` garantida por índice único dispensa qualquer camada seguinte) e
   `valoresDominioDeclarados` (`Ordem`, depois `Codigo` como desempate de NEGÓCIO, não
   técnico);
2. **identidade de negócio única**, quando existe — oferta por `OfertaCursoOrigemId` e,
   dentro da oferta, modalidade por `Codigo` ordinal (não `ModalidadeOrigemId`: ordenar pelo
   Guid técnico da linha seria exatamente o vazamento de identidade técnica para o hash que
   esta camada existe para evitar); condição/recurso/banca pelos seus `*OrigemId`; exigências
   documentais por fase mais tipo de documento (chave PARCIAL: só reduz o empate ao caso raro
   de duas exigências na mesma fase e do mesmo tipo, sem discriminar a coleção inteira
   sozinha);
3. **chave de conteúdo — esta é a D9**, usada onde não há chave natural SUFICIENTE para
   decidir a posição sozinha: arrays sem nenhuma chave de negócio (regras de eliminação —
   cardinalidade múltipla, duas do mesmo código são válidas —, critérios cumulativos,
   ocorrências esperadas, obrigatoriedades) e, explicitamente, o grupo residual que sobra
   depois de uma identidade PARCIAL — o desempate de exigências com fase e tipo idênticos
   (camada 2), onde a chave de negócio existe mas não é total.

Sobre o resultado das três, um desempate TÉCNICO final por `EntityBase.Id` estabiliza a rara
duplicata verdadeira — só onde o Id já é congelado no envelope (etapa, exigência documental).
Não é código morto: o domínio recusa `Ordem` INFORMADA duplicada, não conteúdo repetido, e o
índice único do banco admite múltiplos `NULL` — duas etapas classificatórias com o mesmo
nome, caráter, peso e nota mínima, ambas sem `Ordem`, são aceitas e precisam de um desempate
alcançável.

### E1.1b — o que a ordem semântica de cada array SIGNIFICA

Registrar a camada não basta — nomear cada array de `Ordem` semântica pelo que a posição dele
**decide no domínio**, não só pela regra técnica que a produz:

| Caminho | O que a ordem significa |
|---|---|
| `etapas` | organização opcional da apresentação — NÃO é parcela do divisor da média (caráter e peso decidem isso) nem precedência de execução; nenhum consumidor a lê como sequência |
| `cascataRemanejamento.ordens[].destinos` | prioridade legal de tentativa de remanejamento — muda para ONDE a vaga é oferecida primeiro, e pode mudar quem é convocado (UNI-REQ-0056) |
| `criteriosDesempate` | refinamento sequencial do empate — o primeiro critério é aplicado antes do seguinte, e só desempata o resíduo que sobrar |
| `cronogramaFases.fases` | ordem cronológica e jurídica — base de janelas, precedências entre fases e prazos de recurso |
| `fatosColetados` | ordem de exibição e coleta no formulário; também limita toda pré-condição a citar só fato ANTERIOR |
| `arvoreSatisfacao` (raízes e filhos) | posição declarada no fluxo documental — a álgebra de satisfação (grupos E/OU) é comutativa, a apresentação ao candidato não |
| `regrasDerivacao[].regras` | ordem configurada de avaliação da derivação, incluindo qual regra é a âncora (incondicional) |
| `documentosExigidos.metadadosFatos[].valoresDominioDeclarados` | ordem de apresentação das opções de um fato de seleção no formulário |
| `grafoDependencia.ordemTopologica` | ordem produtor→consumidor entre campo, fato e exigência — delegada ao domínio, ver E1.2 |

### E1.2 — `grafoDependencia.nos`/`arestas`/`ordemTopologica` é delegação, não uma quarta camada

O grafo de dependência conjunto não é ordenado pela projeção do envelope: o value object de
domínio já devolve nós, arestas e ordem topológica na sua forma canônica (Kahn, com a ordem
de coleta efetiva como prioridade e `(Classe, Codigo)` como desempate), e o canonicalizador só
serializa o que recebe, sem reordenar. Registrar isto como delegação evita a leitura de que
"toda ordenação acontece na projeção": quem reordenasse `ordemTopologica` alfabeticamente, por
engano, destruiria o testemunho produtor→consumidor que o array existe para provar.

### Fora desta emenda

Contagem de blocos/chaves do envelope: fatia de #1083. Política de fixture única versus
fixture-por-`schema_version`: fatia de #1053. Esta emenda não reabre nenhuma das duas.
