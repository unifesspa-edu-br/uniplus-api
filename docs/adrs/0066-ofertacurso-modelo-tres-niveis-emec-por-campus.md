---
status: "accepted"
date: "2026-05-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0066: Modelo de oferta em três níveis — Curso curricular, OfertaCurso regulatória e código e-MEC por campus

## Contexto e enunciado do problema

O wizard de Configuração de Edital precisa expressar **quais cursos têm vagas em quais locais, sob qual modalidade**. O protótipo legado misturava num único conceito de "curso" três coisas distintas: a matriz curricular, a instância regulatória (onde/como o curso é ofertado) e o registro nacional (código e-MEC). Essa mistura gera inconsistências reais: "Engenharia Civil" é ofertada em Marabá (instituto IGE) e em Santana do Araguaia (instituto IEA) — mesmo nome de curso, unidades ofertantes diferentes, e códigos e-MEC diferentes.

A deliberação precisou responder a duas perguntas acopladas:

1. Qual a granularidade do "curso" que o edital referencia — a matriz curricular ou a oferta concreta?
2. Onde vive o `e_mec_codigo` — é atributo estável do curso, ou varia por local de oferta?

Quatro fontes institucionais autoritativas (endereços e-MEC da IES 18440; campo `codigoIES` do COC; campos `cod_curso`/`nome_oficial`/`unidade` do SIGAA; campo `cod_emec` do registro de diploma) foram cruzadas em 2026-05-19. Elas provaram que o código e-MEC **varia por campus-sede** e é herdado pelos convênios: Engenharia Civil → `1262444` (Marabá/IGE) e `1276153` (Santana/IEA); História Licenciatura → `1262485` (Marabá) e `1270446` (Xinguara); Matemática Licenciatura → `12037` (Marabá) e `1270326` (Santana).

## Drivers da decisão

- **Consistência regulatória**: o edital deve apontar para uma oferta estável e auditável, não para um conceito ambíguo que confunde matriz, local e código.
- **Granularidade mista real**: a unidade ofertante (instituto acadêmico responsável) varia por local para o mesmo curso — isso é característica da oferta, não inconsistência do curso.
- **Evidência sobre inferência**: a localização do `e_mec_codigo` deve seguir as quatro fontes legadas, não suposição.
- **Forward-compat**: novos formatos pedagógicos, turnos e polos devem tocar apenas a oferta, mantendo a matriz curricular estável.

## Opções consideradas

- **Modelo de dois níveis** — `Curso` carregando local, modalidade e `e_mec_codigo`, com uma linha por combinação. Foi o ponto de partida do protótipo.
- **Modelo de três níveis com `e_mec_codigo` no Curso** — separar `Curso` (curricular) de `OfertaCurso` (regulatória), mantendo o código e-MEC como atributo único do curso.
- **Modelo de três níveis com `e_mec_codigo` na OfertaCurso** — separar curricular de regulatório e mover o código e-MEC para a oferta, refletindo a variação por campus comprovada nas fontes.

## Resultado da decisão

**Escolhida:** "Modelo de três níveis com `e_mec_codigo` na OfertaCurso" (`Curso → OfertaCurso → Turma`), porque é o único que reconcilia a granularidade mista observada (mesmo curso, institutos e códigos e-MEC diferentes por campus) com a estabilidade da matriz curricular.

Como catálogo cross-módulo, `Curso` e `OfertaCurso` vivem no módulo **Parametrizacao** ([ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)). A referência de `OfertaCurso` à `Unidade` ofertante atravessa o bounded context `OrganizacaoInstitucional` ([ADR-0055](0055-organizacao-institucional-bounded-context.md)) e, por isso, segue o snapshot-copy da [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) (cópia imutável dos dados relevantes da unidade, sem FK cross-banco).

**`Curso` — curricular puro (~40 entradas):**

- `Codigo`, `Nome`, `Grau`.
- Opcionalmente `TitulacaoFeminino`/`TitulacaoMasculino` (do SIGAA, para diploma e para a RN02 de `docs/visao-do-projeto.md` — nome social).
- `CargaHorariaTotal`, `DuracaoSemestresPadrao`, `DescricaoCurricular`, `Ativo`.
- **Sem `e_mec_codigo`.**

**`OfertaCurso` — instância regulatória (cerca de uma centena de entradas reais):**

- `CursoId` — referência à matriz curricular.
- `LocalOfertaId` — referência ao `LocalOferta` específico ([ADR-0065](0065-localoferta-flat-um-por-endereco-emec.md)).
- `UnidadeOfertante` — snapshot-copy ([ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md)) do instituto/faculdade responsável (varia por campus — granularidade mista confirmada pelo campo `unidade` do SIGAA).
- `Modalidade` — `Regular`, `FormaPara`, `Parfor`, `Pronera`, `Pepeti`, `ConvenioOutro`, `Outro`.
- `FormatoPedagogico` — `Presencial` (default, não-nulo), `Semipresencial`, `Ead`.
- `Turno` — `Matutino`, `Vespertino`, `Noturno`, `Integral`.
- `EMecCodigo` — código e-MEC **por campus-sede** (do registro de diploma/COC), herdado pelos convênios do mesmo campus.
- `CodigoSga` — código no Sistema de Gestão Acadêmica (do `cod_curso` do SIGAA). Nome **vendor-neutral** (ver a convenção de nome na seção "Mais informações") para não acoplar o domínio ao SIGAA.
- `VagasAnuaisAutorizadas`.
- `BaseLegal` — **obrigatório quando `Modalidade != Regular`** (programa-mãe, ex.: "Convênio Forma Pará nº 004/2020").
- `AtoAutorizacaoMec` — **opcional** (ato específico da oferta, ex.: "Carta de aceite Prefeitura de Almeirim 2024").
- `VigenciaInicio`, `VigenciaFim`, `Ativo`.

**`Turma`** é o terceiro nível e fica **fora do MVP** (integração SIGAA futura).

O edital referencia `OfertaCurso` (estável entre semestres), não `Curso`. A junção edital↔oferta carrega `VagasOfertadas ≤ OfertaCurso.VagasAnuaisAutorizadas`.

A pesquisa sobre os programas itinerantes confirmou um padrão **híbrido nos quatro** (Forma Pará, PARFOR, Pronera, Pepeti): sempre há um programa-mãe (guarda-chuva, em `BaseLegal`) mais um ato específico por oferta (opcional, em `AtoAutorizacaoMec`). O `e_mec_codigo` permanece atributo do registro nacional por campus; a oferta adiciona o local de funcionamento.

## Consequências

### Positivas

- A granularidade mista (institutos diferentes por campus para o mesmo curso) deixa de ser inconsistência e vira característica da oferta.
- O `e_mec_codigo` reflete as quatro fontes legadas (varia por campus-sede), eliminando a contradição de tratá-lo como único por curso.
- O edital aponta para algo estável (`OfertaCurso`); novos formatos/turnos/polos tocam só a oferta.
- As quatro fontes legadas cruzadas tornam-se base de migração de dados para produção.

### Negativas

- Migração dos dados legados exige separar cerca de 40 cursos curriculares das ofertas regulatórias, com matching COC↔SIGAA imperfeito (parte das ofertas ainda sem `CodigoSga`).
- Snapshot-copy da unidade ofertante ([ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md)) exige disciplina de rebinding quando a unidade muda — trade-off já assumido pela ADR-0061.

### Neutras

- O terceiro nível (`Turma`) fica reservado mas não implementado no MVP.

## Confirmação

- `e_mec_codigo` **não** existe em `Curso`; existe em `OfertaCurso`. Verificável por inspeção do modelo e por teste de migração que confirma códigos distintos para o mesmo curso em campi diferentes (ex.: Engenharia Civil `1262444` vs `1276153`).
- `BaseLegal` é obrigatório sempre que `Modalidade != Regular` — candidato a invariante validado no domínio.
- `OfertaCurso.UnidadeOfertante` é value object (snapshot), não FK cross-banco ([ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md)).

## Mais informações

- **Convenção de nome vendor-neutral:** `e_mec_codigo` mantém nome próprio (registro nacional estável), mas `CodigoSga` (SGA = Sistema de Gestão Acadêmica) é genérico para não acoplar o domínio ao SIGAA.
- Os sete cursos que a busca pública não localizou foram completados pelo registro de diploma (Biologia Lic. `1442854`, Jornalismo `1276154`, Engenharia Florestal `1457294`, Arquitetura `1483808`, Zootecnia `1276151`, Medicina Veterinária `1276152`).
- Refina a [ADR-0065](0065-localoferta-flat-um-por-endereco-emec.md): convênios de interiorização (Jacundá, Parauapebas, Canaã não-Pepeti) têm `LocalOferta.Tipo = ConvenioInteriorizacao`, com o vínculo acadêmico em `OfertaCurso.UnidadeOfertante`.
- **Pendências a confirmar com PROEG/Procuradoria (não bloqueiam):** granularidade exata da autorização PARFOR (turma vs lote de edital); Pronera (1 projeto = 1 ou N turmas); financiador do Pepeti.
- **Origem:** deliberação técnica de modelagem do Módulo Configuração de Edital conduzida pelo Tech Lead (2026-05-19), com confirmação operacional da equipe CTIC sobre os legados COC/UDOCS, validada contra quatro fontes institucionais (endereços e-MEC da IES 18440, COC, SIGAA, registro de diploma); rascunhos de trabalho não publicados.
