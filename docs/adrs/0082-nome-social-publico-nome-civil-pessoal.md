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

## Emenda 1 (2026-08-11) — exceção justificada ao nome completo e piso técnico do default (UNI-REQ-0050)

Esta emenda reconcilia a ADR com o vocabulário de divulgação pública que a Story #563 (UNI-REQ-0050) implementou
e que já está em `main` (`ConfiguracaoDivulgacao`, `RegrasDeNomeAbreviado`). O enunciado original desta ADR vedava
o nome completo em listas públicas sem ressalva alguma. O Parecer Técnico 002/2026 da Encarregada de Proteção de
Dados — a mesma fonte que valida esta ADR — é mais específico: no §4 ("Da divulgação de listas públicas"), recomenda
número de inscrição + nome abreviado como o mais adequado, e admite a exceção apenas "caso exista norma específica
do processo seletivo que imponha divulgação nominal", recomendando nesse caso "que seja adotada a forma menos
invasiva possível, evitando-se exposição integral do nome civil". As duas afirmações não coexistiam sem
qualificação; o achado foi registrado na issue #1081. Esta emenda resolve o lado da ADR desse achado — a
reconciliação do registro correspondente do UNI-REQ-0050 no portal (`uniplus-developers`) é tratada à parte.

**Trechos substituídos.** O primeiro item da lista em "Resultado da decisão" ("Listas de classificação e de
resultado") é qualificado por 1.1 a 1.3. A frase "O nome completo não é exibido nessas listas" deixa de ser
categórica e passa a ser a regra-padrão, com a exceção de 1.1. O primeiro bullet da seção "Confirmação" é
substituído por 1.4. O restante da ADR permanece vigente sem alteração — em especial, a classificação de nome
social como dado público e nome civil como dado pessoal, e a persistência da preferência, não mudam.

### 1.1 — Vocabulário de divulgação: piso obrigatório + exceção nominal justificada

A identificação numa lista pública de classificação/resultado usa o vocabulário fechado de
`ConfiguracaoDivulgacao` (UNI-REQ-0050): três tokens, dos quais `numero_inscricao` é **sempre presente** (piso
obrigatório) e os dois tokens nominais são **mutuamente exclusivos entre si** — não há três formas independentes,
há três *conjuntos* válidos:

| Conjunto publicado | Justificativa |
|---|---|
| `{ numero_inscricao }` | não exigida — é o default de menor exposição |
| `{ numero_inscricao, nome_abreviado }` | não exigida — iniciais dos primeiros nomes + último sobrenome por extenso (regra vigente desta ADR, §"Resultado da decisão"), padrão institucional recomendado quando o processo amplia além do piso |
| `{ numero_inscricao, nome }` | **obrigatória**, registrada e congelada por processo (RN08) |

"Não exigida" não é "proibida": `ConfiguracaoDivulgacao.Criar` não rejeita uma justificativa informada nos dois
primeiros conjuntos — só a exige quando `nome` está presente. Uma justificativa opcional, quando informada nesses
conjuntos, é normalizada e congelada como qualquer outra.

A exceção do nome completo **não é uma escolha livre de conveniência administrativa**: só é admissível quando há
**norma específica daquele processo seletivo** que imponha a identificação nominal (Parecer 002/2026, §4) —
condição que a justificativa registra. O caso concreto que motivou o registro do vocabulário, citado literalmente
na Story #563: processos do tipo PSIQ (Indígena/Quilombola), em que a divulgação nominal "pode ser necessária por
acessibilidade e identificação cultural" — apontado pelo P.O., com a orientação-padrão da DPO permanecendo a
minimização para os demais casos.

**Lacuna registrada, não resolvida por esta emenda.** `ConfiguracaoDivulgacao.Criar` valida apenas a **forma** da
justificativa — presença, até 1000 caracteres, normalização NFC — não confere se o texto de fato cita uma norma
existente e aplicável àquele certame. `DefinirConfiguracaoDivulgacaoCommand` não carrega identidade de usuário, e
`ConfiguracaoDivulgacao` (`EntityBase`) não registra quem grava o rascunho — a plataforma não preserva essa
identidade. `ProcessoSeletivoController` inteiro é `[Authorize(Roles = "plataforma-admin")]`, o que restringe o
acesso à rota a essa role, mas não constitui, por si, um gate de mérito sobre a norma citada.

O gate de mérito desta ADR é o **publicador**: é na publicação que `PublicarProcessoSeletivoCommandHandler`
congela a identidade do ator responsável — `VersaoConfiguracao.AtorUsuarioSub` ("sub do usuário que publicou —
evidência forense de autoria"), campo distinto de `AtoCriadorId`/`AtoCriadorHash` (identidade e integridade do
**ato**, não da pessoa). Antes de publicar um processo com `nome` na divulgação, é o `plataforma-admin` que
publica quem deve conferir a existência e a aplicabilidade da norma citada na justificativa, e a impossibilidade
de atender à finalidade por forma menos invasiva (1.2) — responsabilidade de governança, não decorrente do
`[Authorize]` da rota. A trilha resultante identifica **quem publicou** sob qual justificativa; **não** identifica
quem redigiu o rascunho, se essa pessoa for outra. Uma validação estruturada (referência formal à norma, segunda
aprovação antes da publicação) fica fora do escopo desta emenda e da Story #563; se vier a ser necessária, é
trabalho de acompanhamento a registrar em issue própria.

### 1.2 — Necessidade e proporcionalidade, não conformidade automática por token

A leitura original desta ADR ("Resultado da decisão") não distinguia piso técnico de prática recomendada: descrevia
a identificação pública, sem exceção, como "número de inscrição acompanhado de uma forma abreviada do nome". Esta
emenda **substitui** essa leitura por duas camadas — o default operacional de um processo sem
`ConfiguracaoDivulgacao` explícita passa a ser `numero_inscricao` sozinho (a menor exposição possível), e "número +
nome abreviado" vira a **prática institucional recomendada** quando o processo amplia a divulgação, não mais o
único padrão normativo. A consequência é deliberada: um processo publicado sem configuração explícita deixa de
seguir o resultado que a redação original descrevia — passa a divulgar só o número.

Nenhum dos três conjuntos de 1.1 tem conformidade com a LGPD garantida automaticamente pela sua simples escolha —
o princípio da necessidade (art. 6º, III) é relacional à finalidade de cada certame, não uma propriedade fixa do
token:

- `numero_inscricao` é o piso de menor exposição; sua suficiência depende de a finalidade do certame (identificar
  o candidato numa lista pública) ser atendida só com o número — o que é o caso típico, e por isso é o default.
- `nome_abreviado` é a ampliação recomendada quando o piso não atende à finalidade daquele certame — recomendação
  institucional (Parecer 002/2026, §4), não teste de proporcionalidade automático por si só.
- `nome` completo não "sai" da minimização por definição do token: quando há norma específica do certame que o
  impõe e a finalidade não pode ser atendida por forma menos invasiva, a divulgação integral pode ser, para aquele
  caso, o mínimo necessário. O próprio Parecer recomenda "que seja adotada a forma menos invasiva possível" mesmo
  sob a exceção — é essa avaliação, específica do certame, que a justificativa registrada documenta.

### 1.3 — A exceção nunca autoriza nome civil contra a preferência do titular

Os tokens `nome_abreviado` e `nome` operam sobre o **nome de exibição já resolvido pela preferência persistida**
desta ADR (§"Resultado da decisão") — nome social quando o candidato o prefere, nome civil só na ausência de
preferência social. A exceção de 1.1 amplia **o que** é publicado (a forma nominal), nunca **qual** nome é
publicado: não cria uma segunda via para expor o nome civil de quem prefere nome social. O próprio Parecer 002/2026
é explícito nesse sentido — mesmo sob norma que imponha divulgação nominal, recomenda "evitando-se exposição
integral do nome civil" (§4). Uma projeção pública que resolvesse `nome`/`nome_abreviado` para o nome civil contra
a preferência do titular violaria o núcleo desta ADR e o Decreto 8.727/2016, independentemente de haver
justificativa registrada para a ampliação.

### 1.4 — Fitness/golden test corrigido

O bullet correspondente em "Confirmação" passa a ser: uma lista pública de classificação/resultado expõe sempre o
número de inscrição — sozinho (default) ou acompanhado de uma forma nominal, o nome abreviado (iniciais + último
sobrenome, justificativa não exigida) ou, mediante justificativa obrigatória e registrada, o nome completo —, **nunca as duas
formas nominais simultaneamente**, e a forma nominal usada resolve sempre pela preferência persistida (nome social
quando preferido). O restante da confirmação (DTO público sem nome civil contra a preferência; resolução nominal
para nome social nos pontos de identificação) não muda.

### Mais informações da emenda

- Implementação: `ConfiguracaoDivulgacao` (`Selecao.Domain/Entities`) e `RegrasDeNomeAbreviado`
  (`Selecao.Domain/Services`) — issue #563 (UNI-REQ-0050).
- Fonte primária da exceção: Parecer Técnico 002/2026 da Encarregada de Proteção de Dados,
  `uniplus-developers/docs/lgpd/2º_PARECER_TECNICO-_PROTECAO_DE_DADOS_assinado.pdf`, §4 "Da divulgação de listas
  públicas" — "[...] caso exista norma específica do processo seletivo que imponha divulgação nominal,
  recomenda-se que seja adotada a forma menos invasiva possível, evitando-se exposição integral do nome civil."
- A regra de abreviação vigente (`RegrasDeNomeAbreviado.Vigente`, "iniciais_mais_ultimo_sobrenome") é identificador
  versionado e congelado por publicação (RN08): uma mudança futura na regra nunca reinterpreta um certame já
  publicado — recebe identificador novo.
