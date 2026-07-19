---
status: "accepted"
date: "2026-07-19"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0116: Fato multi-fonte — origem, ponto de resolução, binding e `FatoValorDominio`

## Contexto e enunciado do problema

A ADR-0111 fixou o vocabulário fechado de fatos do candidato com três eixos
ortogonais ao domínio: `Natureza` (origem do dado), `Cardinalidade` e o próprio
`Dominio`. A análise dos editais reais de 2027 (change
`documentos-exigidos-cobertura-editais`, Story #917) mostrou que esse modelo
precisa de mais três capacidades para expressar requisitos documentais sem
código por-tipo: (a) um **ponto de resolução** — a fase em que o valor do fato
fica conhecido, para recusar no cadastro uma condição que cita um fato ainda
não resolvido na fase da exigência; (b) um **binding** concreto — a referência
de onde/como o valor é produzido, para que "novo fato sem código" seja real e
não apenas metadado decorativo; (c) **descrição por valor** de um domínio
categórico, para orientar a escolha do candidato em fatos `DECLARADO` (ex.:
o que é "TEA"), modelada como entidade filha `FatoValorDominio`.

Esta ADR refina a ADR-0111 nesses três pontos e ajusta a nomenclatura do eixo
de origem — sem alterar sua governança (seed-governado, sem CRUD, ADR-0111
continua valendo integralmente nesse aspecto).

## Drivers da decisão

- Um gatilho não pode citar um fato cujo valor só é conhecido numa fase
  posterior à da exigência — sem isso, a exigência ficaria condicionada a um
  dado que não existe ainda no momento em que precisaria ser avaliado.
- "Novo fato usável sem código" exige que o fato declare **de onde** seu valor
  vem (o binding), não só metadado de classificação.
- Fatos `DECLARADO` (seleção do candidato) precisam de descrição por valor —
  requisito de acessibilidade/clareza (o público inclui comunidades rurais e
  tradicionais, editais de Campo/PSIQ).

## Opções consideradas

- **Origem:** manter `NaturezaFato` como está (`BrutoInformado`/`DeVontade`/
  `Derivado`) e tratar `DERIVADO`/`DECLARADO`/`INTEGRACAO` como vocabulário só
  de documentação, com tradução implícita — **rejeitada**: cria uma camada de
  tradução permanente entre o código e o contrato/wire (fechado em PR4), sem
  benefício, e o Uni+ prefere mudar o código a manter tradução.
- **Origem:** renomear tokens sem reclassificar nenhum fato — **rejeitada**
  (achado do Codex): perderia a distinção real entre fatos computados
  (`FAIXA_ETARIA`, `RENDA_PER_CAPITA`) e fatos respondidos diretamente
  (os demais sete), que hoje colapsam sob o mesmo token `BrutoInformado` só
  porque a distinção não importava para o resolvedor até agora.
- **Binding:** string livre sem gramática — **rejeitada** (achado do Codex):
  não sustenta "sem código" de forma verificável nem barra incoerência entre
  binding e origem.
- **Binding:** shape JSON completo de wire nesta ADR — **rejeitada**: o shape
  de wire é fechado na Story #919 (PR4); esta ADR só fixa a coerência mínima
  no domínio.

## Resultado da decisão

**Origem (renomeia e reclassifica `Natureza`→`Origem`):** o enum
`NaturezaFato` (`BrutoInformado`/`DeVontade`/`Derivado`) torna-se `OrigemFato`
(`Derivado`/`Declarado`/`Integracao`), refletindo exatamente o vocabulário do
design da Story #917. `DeVontade` nunca foi semeado — é removido sem custo.
Reclassificação real dos nove fatos (não uma renomeação mecânica):

| Fato | Origem nova | Justificativa |
|---|---|---|
| `FAIXA_ETARIA` | `Derivado` | Computada a partir da data de nascimento — o motor deriva o valor, o candidato não a responde diretamente |
| `RENDA_PER_CAPITA` | `Derivado` | Computada como razão renda familiar ÷ integrantes — o fato em si é uma saída, não uma resposta direta |
| `COR_RACA`, `QUILOMBOLA`, `PCD`, `EGRESSO_ESCOLA_PUBLICA`, `SEXO`, `MODALIDADE`, `CONDICAO_ATENDIMENTO` | `Declarado` | Resposta/seleção direta do candidato no cadastro de inscrição |
| `NACIONALIDADE`, `TIPO_DEFICIENCIA` (novos) | `Declarado` | Seleção direta do candidato sobre o domínio do fato |

`Integracao` fica reservada, sem fato semeado (fonte externa futura, ex.:
SIGAA, issue #874). Esta reclassificação **não** afeta o resolvedor — a
origem nunca alimenta a avaliação do predicado (invariante que a ADR-0111 já
estabelecia para o eixo `Natureza`); o efeito é só descritivo/de binding.

**Ponto de resolução:** `FatoCandidato.PontoResolucao: string`, validado
contra `FaseCanonicaCatalogo.EhCanonico(...)` (o conjunto fechado de catorze
códigos, sem depender de linha viva em `fase_canonica` — decopla do fato de a
tabela ser povoada operacionalmente pós-deploy). Todos os fatos desta colheita
(os nove existentes + `NACIONALIDADE`/`TIPO_DEFICIENCIA`) resolvem em
`INSCRICAO`. O *gate* que recusa uma condição citando fato de fase posterior
(comparando o ponto de resolução contra a fase da exigência, via a tabela de
precedência entre fases já existente — migration
`AddPrecedenciaFaseEAtributosFaseCanonica` — nunca por posição em lista, que
não é ordenada semanticamente) é implementado na Story #916 (PR2), que
consome este campo.

**Binding:** `FatoCandidato.Binding: string`, formato `"{PREFIXO}:{REFERENCIA}"`
com prefixo coerente com a origem: `Derivado`→`ATRIBUTO_CANDIDATO:`;
`Declarado`→`CAMPO_INSCRICAO:`; `Integracao`→`INTEGRACAO:`. A factory recusa
prefixo incoerente com a origem declarada. O shape de wire completo (JSON
estruturado por origem) é fechado na Story #919 (PR4); aqui fixa-se só a
coerência mínima que sustenta a promessa de "sem código".

**`FatoValorDominio`:** entidade filha de `FatoCandidato`
(`Id, FatoCandidatoId, Codigo, Descricao, Ordem, Ativo`), unicidade
`(FatoCandidatoId, Codigo)` (código normalizado trim, comparação ordinal),
`Descricao` obrigatória quando o fato pai é `Declarado`. Adicionada só pelo
agregado `FatoCandidato.AdicionarValorDominio(...)` — o único que conhece a
`Origem` do pai (para exigir descrição) e os irmãos já adicionados (para a
unicidade). Substitui o array `jsonb ValoresDominio` **somente** para os fatos
categóricos **estáticos** desta leva (`COR_RACA`, `SEXO`, `NACIONALIDADE`).
Fatos categóricos de **escopo-processo** (`MODALIDADE`, `CONDICAO_ATENDIMENTO`,
`TIPO_DEFICIENCIA`) continuam sem `ValoresDominio`/`FatoValorDominio` — seu
domínio vem de cadastro vivo + projeção do processo, nunca duplicado no
catálogo de fatos ("não dois catálogos": a fonte de `TIPO_DEFICIENCIA` é o
cadastro `TipoDeficiencia` já existente, projetado por `tipoDeficienciaIds`,
mesmo mecanismo de `OfertaAtendimentoEspecializado`/`OfertaTipoDeficiencia`).

**`TipoDeficiencia` (cadastro CRUD existente em Configuração) ganha:**

- `Permanente: bool?` (nullable — `null` = ainda não classificado pelo CEPS,
  distinto de `false` = classificado como não-permanente; a taxonomia
  concreta é refinamento residual, task 0.1 da change, e não bloqueia este
  modelo). `NOT NULL` fica para quando a classificação estiver completa.
- `Descricao` passa de opcional a **obrigatória** — agora serve também como a
  descrição por valor exigida pela spec para o fato `TIPO_DEFICIENCIA`
  (`Declarado`), exposta via `ITipoDeficienciaReader`/`TipoDeficienciaView`.

## Consequências

### Positivas

- O eixo de origem no código passa a usar exatamente o vocabulário do
  contrato (`DERIVADO`/`DECLARADO`/`INTEGRACAO`), eliminando uma tradução
  permanente entre modelo interno e wire.
- `PontoResolucao`/`Binding` tornam "novo fato sem código" verificável, não
  apenas descritivo.
- `FatoValorDominio` fecha a lacuna de descrição por valor sem duplicar
  catálogo para fatos de escopo-processo.

### Negativas

- Reclassificar `FAIXA_ETARIA`/`RENDA_PER_CAPITA` como `Derivado` é uma
  migration de dados (não só rename) — exige `UpdateData` explícito por linha.
- `TipoDeficiencia.Descricao` obrigatória é breaking para qualquer linha
  existente sem descrição — aceitável só por não haver dado real em produção
  (pré-produção).

### Neutras

- `Integracao` fica sem fato semeado até a integração SIGAA (#874) existir.

## Confirmação

- Teste de unidade cobre a reclassificação de origem dos nove fatos contra a
  tabela desta ADR.
- Teste de unidade cobre a coerência prefixo-de-binding × origem (recusa
  binding com prefixo incoerente).
- Teste de integração confere o seed do catálogo (11 fatos) contra esta ADR,
  incluindo `PontoResolucao`/`Binding`/`FatoValorDominio`.

## Mais informações

- ADR-0111 (vocabulário fechado de fatos do candidato — continua vigente para
  domínio, cardinalidade e governança seed-only; esta ADR refina só origem,
  ponto de resolução, binding e descrição por valor).
- ADR-0056 (leitor cross-módulo `IXxxReader`).
- ADR-0061 (snapshot-copy cross-módulo).
- Change OpenSpec `documentos-exigidos-cobertura-editais`, Story
  [#917](https://github.com/unifesspa-edu-br/uniplus-api/issues/917).
