---
status: "accepted"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "Encarregada de Proteção de Dados (DPO) — validada pelo Parecer Técnico 002/2026 (08/06/2026)"
informed:
  - "Equipe Uni+"
---

# ADR-0082: Nome social como dado público e nome civil como dado pessoal protegido

## Contexto e enunciado do problema

Um candidato pode registrar um **nome social**. Em pontos públicos do processo — listas de resultado, convocações, classificação — exibir o **nome civil** de quem optou pelo nome social contraria o **Decreto 8.727/2016** (uso do nome social na administração pública federal) e o princípio da **dignidade da pessoa humana** (Constituição, art. 1º, III).

Há uma inversão em relação à intuição comum sobre proteção de dados. O **nome social**, quando é a forma de identificação preferida pelo candidato, é a identificação que **deve** aparecer publicamente — é dado **público** nesse contexto. O **nome civil**, ao contrário, é dado **pessoal** (LGPD) e **não** pode aparecer em local público quando contraria a preferência do titular.

O problema é classificar o nome social e o nome civil, definir como a preferência de identificação é determinada e estabelecer a regra de exibição pública.

## Drivers da decisão

- **Decreto 8.727/2016** — uso do nome social na administração pública federal.
- **Dignidade da pessoa humana** — Constituição, art. 1º, III.
- **LGPD** — o nome civil é dado pessoal; sua divulgação tem base legal e finalidade.
- **Preferência explícita** — a escolha do candidato deve ser persistida e inequívoca, não inferida por heurística.

## Opções consideradas

- **A**: Tratar o nome social como dado protegido (PII) e usar o nome civil nas listas públicas.
- **B**: **Nome social (quando preferido) é dado público; nome civil é dado pessoal** e nunca aparece em local público contra a preferência; a preferência de identificação é **persistida**.
- **C**: Inferir a preferência pela presença do nome social (sem persistir uma escolha explícita).

## Resultado da decisão

**Escolhida:** "B — nome social público quando preferido; nome civil pessoal e protegido", porque é o que respeita o Decreto 8.727/2016 e a dignidade do titular, e porque a preferência precisa ser uma escolha registrada, não uma adivinhação.

- **Listas de classificação e de resultado (públicas):** a identificação é feita pelo **número de inscrição** acompanhado de uma **forma abreviada do nome** — as iniciais dos primeiros nomes seguidas do último sobrenome por extenso (por exemplo, "M. L. Almeida" para "Maria Lima Almeida") —, derivada do **nome social** quando o candidato o prefere. O **nome completo não é exibido** nessas listas (minimização — LGPD, art. 6º, III).
- **Pontos de identificação nominal** (convocação nominal, documento de identificação do candidato, atendimento): usa-se o **nome social** quando o candidato indicou um nome social; usa-se o **nome civil** quando não há nome social ou a preferência é pelo civil. O **nome civil nunca aparece em local público contra a preferência** do titular.
- O **nome social preferido** é classificado como dado **público** (a forma de identificação que o titular escolheu tornar pública).
- O **nome civil** completo é dado **pessoal** (LGPD): visível ao próprio titular, a atores com permissão operacional explícita e a auditores sob escopo e base legal — **nunca** em exposição pública contra a preferência.
- A **preferência de identificação** (social ou civil) é **persistida** como atributo do candidato, **não inferida** pela presença ou ausência do nome social.

Invariantes da preferência:

- se o candidato não tem nome social, a preferência só pode ser **civil**;
- se a preferência é **social**, o nome social não pode estar vazio;
- a transição entre preferências é um ato explícito do titular, registrado.

## Consequências

### Positivas

- Conformidade com o Decreto 8.727/2016 e respeito à dignidade do titular.
- A exposição pública usa sempre a identificação que o titular escolheu; o nome civil fica protegido onde deve.
- A preferência é inequívoca e auditável, por ser persistida.

### Negativas

- Exige uma migração/backfill para atribuir a preferência aos candidatos já cadastrados.
- As projeções públicas precisam usar a identificação derivada, nunca o nome civil diretamente — disciplina nas consultas.

### Neutras

- A forma concreta da persistência da preferência e da identificação derivada é detalhada na spec de implementação; esta ADR fixa a **classificação** e a **regra de exibição**.

## Confirmação

- **Fitness/golden BOPLA test**: uma lista pública de classificação/resultado expõe o número de inscrição e o nome abreviado (iniciais + último sobrenome), nunca o nome completo; um DTO público de candidato **não** contém o nome civil quando a preferência é social; nos pontos de identificação nominal, resolve para o nome social quando há indicação dele.
- **Teste de invariante**: a preferência social exige nome social não vazio; sem nome social, a preferência é civil; a transição é coberta por teste.

## Prós e contras das opções

### A — Nome social protegido como PII; nome civil em listas públicas

- Bom, porque segue a intuição comum de "nome social é dado sensível a ocultar".
- Ruim, porque expõe o nome civil contra a preferência do titular, violando o Decreto 8.727/2016 e a dignidade da pessoa.

### B — Nome social público quando preferido; nome civil pessoal; preferência persistida (escolhida)

- Bom, porque respeita a legislação e a dignidade, e torna a preferência inequívoca e auditável.
- Ruim, porque exige backfill da preferência e disciplina nas projeções públicas.

### C — Inferir a preferência pela presença do nome social

- Bom, porque dispensa um campo de preferência.
- Ruim, porque a inferência é ambígua (um nome social cadastrado não significa que o titular quer usá-lo em tudo) e não registra uma escolha — frágil para um direito do titular.

## Mais informações

- Ancora na [ADR-0081](0081-lgpd-by-design-dto-por-permissao.md): esta é a classificação específica do nome social/civil dentro do controle de proteção por projeção.
- Base legal e normativa: Decreto 8.727/2016; Constituição art. 1º, III; LGPD (Lei 13.709/2018).
- Validada pela **Encarregada de Proteção de Dados (DPO)** da instituição — Parecer Técnico 002/2026 (08/06/2026): nome social confirmado como dado público quando preferido; nome civil como dado pessoal protegido (art. 7º II/III e art. 23 LGPD; Decreto 8.727/2016).
