---
status: "accepted"
date: "2026-05-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0065: LocalOferta como entidade flat, uma entrada por local de oferta (endereço e-MEC)

## Contexto e enunciado do problema

O wizard de Configuração de Edital precisa de um catálogo de **locais onde os cursos são ofertados** — campi sede, unidades fora de sede e ofertas por convênio de interiorização. O protótipo legado tratava "campus" e "cidade" como o mesmo achatado, o que não reflete a realidade regulatória: a Unifesspa (IES 18440) opera múltiplos endereços por município, cada um com cadastro próprio no e-MEC.

A modelagem deste catálogo influencia diretamente o que o edital aponta como local de oferta de cada vaga e como a auditoria reconstrói, anos depois, onde uma vaga foi ofertada. A pergunta concreta da deliberação foi: o local de oferta deve ser **uma entrada por endereço cadastrado** (espelhando o e-MEC), **uma entrada por campus com subunidades aninhadas**, ou uma **hierarquia entre locais**?

A evidência canônica (cadastro e-MEC da IES 18440, consultado em 2026-05-19) refutou a hipótese inicial de "ato único por campus": a IES lista **13 endereços distintos**, cada um com código próprio — Marabá com 3 endereços (`1064323` Unidade I com `Polo=A`, `1066314` Unidade II, `1072603` Unidade III), Santana do Araguaia com 3, Xinguara com 2, e os demais municípios (Rondon do Pará, São Félix do Xingu, Canaã dos Carajás, Jacundá, Parauapebas) com 1 cada. Cada Unidade é um endereço regulatório irmão sob a IES, não uma subunidade aninhada.

## Drivers da decisão

- **Espelhar o cadastro regulatório**: o modelo deve refletir o e-MEC, fonte de verdade do MEC, sem inventar agregações que o cadastro não declara.
- **Rastreabilidade**: cada local de oferta precisa ser rastreável diretamente a um código de cadastro federal quando existir.
- **Forward-compat com Ingresso e módulos futuros**: o catálogo é cross-módulo (usado por Seleção hoje, Ingresso depois) e deve viver em local neutro.
- **Evitar overengineering**: hierarquia entre locais não tem suporte no e-MEC (todos os endereços são irmãos sob a IES) e adicionaria complexidade sem ganho.

## Opções consideradas

- **Entidade flat, uma entrada por endereço e-MEC** — cada endereço cadastrado vira um `LocalOferta`; a agregação visual ("Campus de Marabá ▸ Unidades I/II/III") é responsabilidade da camada de apresentação.
- **Uma entrada por campus com subunidades aninhadas** — um `LocalOferta` "Campus de Marabá" contendo Unidades I/II/III como filhos.
- **Hierarquia recursiva entre locais** — `LocalOferta` com auto-referência multinível (campus → unidade → sala).

## Resultado da decisão

**Escolhida:** "Entidade flat, uma entrada por local de oferta", porque espelha exatamente o cadastro e-MEC da IES 18440, mantém rastreabilidade direta ao código federal e evita inventar uma hierarquia que o cadastro regulatório não declara.

`LocalOferta` é catálogo cross-módulo e, por consistência com a [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md), vive no módulo **Parametrizacao**. O endereço físico é modelado como o value object `Endereco` da [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md), embedded na entidade. Forma da entidade (em termos de domínio):

- `Codigo` — slug interno estável (`MARABA_UI`, `MARABA_UII`, …).
- `CodigoEmec` — código do endereço no e-MEC, **opcional**: presente nos endereços cadastrados (sede e fora de sede), ausente nas ofertas por convênio que herdam o código do campus responsável via `OfertaCurso`.
- `Nome` — rótulo institucional (`"Campus de Marabá — Unidade I"`).
- `Tipo` — discriminador `TipoLocalOferta`: `CampusSede`, `CampusForaDeSede`, `CursoForaDeSede`, `PoloEad`, `ConvenioInteriorizacao`, `Outro`.
- `Endereco` — value object da ADR-0056 (logradouro, CEP, latitude/longitude quando disponíveis).
- `CidadeId` — referência ao município (vários `LocalOferta` podem compartilhar a mesma cidade).
- `CampusResponsavelId` — auto-referência opcional ao campus responsável (null para o campus sede principal).
- `PrincipalEmMunicipio` — flag que marca o endereço com `Polo=A` no e-MEC (ex.: Unidade I em Marabá).
- `AtoRegulatorioMec`, `BaseLegal` — referências regulatórias opcionais.

A agregação de apresentação ("Campus de Marabá" agrupando suas Unidades) é um `GROUP BY CidadeId + PrincipalEmMunicipio` na camada de leitura, **não** uma estrutura no modelo de escrita. O rótulo canônico de UI do catálogo é **"Locais de Oferta"** — técnico-neutro, cobre os seis tipos sem colidir com "Polo" (regulatório EaD) ou "Unidade" (entidade institucional da [ADR-0055](0055-organizacao-institucional-bounded-context.md)).

## Consequências

### Positivas

- O catálogo é uma cópia 1:1 do cadastro e-MEC nos endereços com código, com rastreabilidade direta via `CodigoEmec`.
- Sem hierarquia no modelo de escrita: leitura e migração são simples; cada local evolui de forma independente.
- Ofertas por convênio de interiorização (Jacundá, Parauapebas, Canaã não-Pepeti) entram como `ConvenioInteriorizacao`, sem forçar um código e-MEC inexistente.
- Catálogo em Parametrizacao fica disponível para Ingresso e módulos futuros sem reescrita.

### Negativas

- A agregação "campus com suas unidades" passa a ser responsabilidade da apresentação — exige consistência nas telas que listam locais por município.
- O significado exato de `Polo=A` no e-MEC ainda não foi confirmado institucionalmente (ver pendências).

### Neutras

- A flag `PrincipalEmMunicipio` adiciona uma coluna cuja semântica final depende de confirmação da PROEG.

## Confirmação

- Seed inicial do catálogo reproduz os endereços e-MEC da IES 18440 (com `CodigoEmec` preenchido) mais os locais de convênio de interiorização.
- Nenhuma entidade `LocalOferta` referencia outra como pai a não ser via `CampusResponsavelId` (auto-referência simples, sem multinível).

## Mais informações

- Relaciona-se com a [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) — `OfertaCurso.LocalOfertaId` aponta sempre para um `LocalOferta` específico.
- Endereço físico via value object `Endereco` da [ADR-0056](0056-modulo-configuracao-e-read-side-via-reader.md); referência de município segue o catálogo `Cidade`.
- **Pendências a confirmar com PROEG/Procuradoria (não bloqueiam o modelo):** significado de `Polo=A` no e-MEC; classificação regulatória (`Tipo`) definitiva de Jacundá/Parauapebas/Canaã (provisoriamente `ConvenioInteriorizacao`).
- **Origem:** deliberação técnica de modelagem do Módulo Configuração de Edital conduzida pelo Tech Lead (2026-05-19), validada contra o cadastro e-MEC da IES 18440 e confirmada com a equipe CTIC com conhecimento operacional dos sistemas legados; rascunhos de trabalho não publicados.
