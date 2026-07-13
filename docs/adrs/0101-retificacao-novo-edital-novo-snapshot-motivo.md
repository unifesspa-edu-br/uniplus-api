---
status: "superseded by ADR-0103"
date: "2026-07-07"
decision-makers:
  - "Tech Lead"
---

# ADR-0101: Retificação de processo publicado é sempre novo Edital + novo snapshot + motivo

> **Superada.** O *comportamento* decidido aqui continua valendo **no escopo em que foi decidido** — retificar o **ato congelante** de um processo publicado produz um novo ato, com motivo obrigatório, que congela uma nova configuração, e o congelado anterior permanece imutável. (A [ADR-0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md) generalizou o alcance: retificar um ato **não** congelante — uma convocação, por exemplo — **não** cria versão de configuração.) O que caducou é o **modelo** em que a decisão foi expressa: `Edital` como entidade da Seleção, `NaturezaEdital` e `SnapshotPublicacao` não existem mais.
>
> A decisão vigente está em **[ADR-0103](0103-ato-normativo-generalizado-retificacao-como-relacao.md)** — a retificação é uma **relação entre atos**, não um tipo de ato, e o ato normativo vive no módulo `Publicacoes` — e em **[ADR-0104](0104-versao-configuracao-como-agregado-proprio.md)** — o congelamento é uma `VersaoConfiguracao` própria, cuja **vigência ordena as versões**. Leia esta ADR como registro histórico do raciocínio, não como contrato.

## Contexto e enunciado do problema

RN08 congela a configuração de negócio do `ProcessoSeletivo` num `SnapshotPublicacao` imutável no momento da publicação ([ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md)). Um certame, uma vez publicado, frequentemente precisa mudar — correção de prazo, ajuste de vaga, adequação a decisão administrativa superveniente. A pergunta é como essa mudança acontece sem violar o congelamento que RN08 exige: editar diretamente o conteúdo publicado apagaria a evidência do que valeu entre a publicação original e a mudança, quebrando a reprodução histórica que os [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md)/[ADR-0076](0076-contrato-snapshot-runtime-espelha-publicacao.md) já garantem para o snapshot vigente em cada instante.

A modelagem da Story #759 já fechou esta decisão sem promovê-la a ADR: mudança em processo publicado exige **retificação** — um novo Edital de natureza `retificacao`, vinculado ao Edital anterior, com motivo obrigatório, que congela um novo snapshot. Falta o registro formal que trava essa decisão e evita reabertura de discussão a cada nova Story que tocar o ciclo de vida do processo.

## Drivers da decisão

- **Reprodução histórica fiel.** O snapshot que governou um ato no passado não pode ser alterado por uma mudança posterior no certame — pré-requisito já estabelecido pelo [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md).
- **Motivação de ato administrativo.** Toda mudança em processo seletivo já publicado é, na prática institucional, um ato formal que exige justificativa registrada — não uma edição silenciosa.
- **Contrato sem ambiguidade.** Campos que só fazem sentido em retificação (edital retificado, motivo) não podem aparecer, opcionalmente, também em Edital de abertura — a ambiguidade abriria espaço para dado inconsistente sem que nenhuma trava o impeça.
- **Escopo por processo.** Uma retificação é sempre interna ao mesmo `ProcessoSeletivo` — retificar cruzando processos não tem sentido de negócio e indicaria erro de referência.

## Opções consideradas

- **A.** Retificação é sempre um novo Edital (natureza `retificacao`) + novo `SnapshotPublicacao` + motivo obrigatório, com constraint de contrato abertura×retificação e trava contra retificação cruzando processos.
- **B.** Edição direta do Edital publicado é permitida, com o histórico de mudanças registrado numa tabela paralela de auditoria.
- **C.** Retificação como atualização in-place do mesmo Edital, com uma coluna de versão incrementada a cada mudança.

## Resultado da decisão

**Escolhida:** "A — retificação como novo Edital + novo snapshot + motivo", porque é a única opção em que o snapshot anterior permanece intocado e a mudança fica auditável como um evento discreto e motivado, sem introduzir uma segunda forma de mutação de conteúdo congelado.

### Forma do contrato

- `NaturezaEdital` é um enum fechado: `{Abertura, Retificacao}`.
- Um Edital de natureza `Retificacao` carrega `EditalRetificadoId` (referência obrigatória ao Edital imediatamente anterior na cadeia) e `MotivoRetificacao` (texto obrigatório, não vazio).
- Um Edital de natureza `Abertura` **não** carrega `EditalRetificadoId` nem `MotivoRetificacao` — ambos ausentes.
- **Constraint de contrato abertura×retificação:** abertura sem os dois campos é o único estado válido para essa natureza; retificação exige os dois campos preenchidos simultaneamente. É contrato de tudo-ou-nada por natureza, não uma combinação livre de opcionais.
- **Retificação não cruza processos.** `EditalRetificadoId` deve referenciar um Edital do **mesmo** `ProcessoSeletivo` — o agregado-raiz (`ProcessoSeletivo`, com seu único repositório) valida essa pertença antes de persistir, porque é ele quem carrega a coleção completa de Editais do processo.
- **O snapshot de retificação preserva todos os blocos canônicos** do contrato definido pelo [ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md) — não é um diff sobre o snapshot anterior, é um snapshot completo e independente — e **acrescenta** um bloco de retificação (motivo + referência ao Edital retificado) ao envelope.
- **O snapshot anterior permanece imutável.** A retificação nunca edita o `SnapshotPublicacao` já emitido — segue o mesmo princípio append-only que rege o restante das entidades forenses do módulo ([ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md)).
- **A cadeia de Editais e snapshots é a trilha auditável.** Cada Edital de retificação aponta para o anterior; percorrer a cadeia a partir do Edital vigente reconstrói integralmente a história de mudanças do processo, cada nó imutável. O seletor de snapshot vigente ([ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md)) já resolve, para qualquer instante, qual nó da cadeia governa — esta ADR não redefine o seletor, apenas garante que a cadeia sempre tem a forma que ele espera.

## Consequências

### Positivas

- Nenhuma mutação silenciosa de processo publicado — toda mudança deixa rastro com motivo.
- A cadeia de Editais/snapshots reconstrói o histórico completo sem depender de tabela de auditoria paralela.
- Consistente com o princípio append-only já aplicado a `snapshot_publicacao` e com o seletor de snapshot vigente por instante.

### Negativas

- Toda mudança pós-publicação, mesmo uma correção trivial de digitação num campo congelável, exige o fluxo completo de retificação — não há atalho para correções menores. Trade-off aceito porque RN08 não distingue "mudança trivial" de "mudança substantiva": qualquer alteração em bloco congelável é, por definição, uma retificação.

### Neutras

- Nenhuma.

## Confirmação

- Teste: um Edital de abertura persistido sem `EditalRetificadoId` e sem `MotivoRetificacao` é aceito.
- Teste: um Edital de retificação sem `MotivoRetificacao` ou sem `EditalRetificadoId` é recusado com erro de domínio.
- Teste: um Edital de abertura que tenta carregar `EditalRetificadoId` ou `MotivoRetificacao` é recusado com erro de domínio.
- Teste: uma retificação cujo `EditalRetificadoId` aponta para um Edital de outro `ProcessoSeletivo` é recusada com erro de domínio.
- Teste: o snapshot de uma retificação contém todos os blocos canônicos do snapshot anterior mais o bloco de retificação; o snapshot anterior permanece byte-a-byte idêntico após a retificação.

## Prós e contras das opções

### A — novo Edital + novo snapshot + motivo (escolhida)

- Bom, porque o snapshot anterior nunca é tocado e a mudança é auditável como evento discreto e motivado.
- Ruim, porque toda mudança, por menor que seja, passa pelo fluxo completo de retificação.

### B — edição direta + tabela de auditoria paralela

- Bom, porque evita criar um novo Edital para mudanças pequenas.
- Ruim, porque o conteúdo "publicado" deixa de ser imutável por definição — a garantia de RN08 passaria a depender inteiramente da tabela de auditoria estar sempre sincronizada, um ponto de falha que o modelo append-only elimina por construção.

### C — atualização in-place com coluna de versão

- Bom, porque simplifica a leitura do estado "atual" (uma linha por Edital, não uma cadeia).
- Ruim, porque a versão anterior deixa de existir como registro completo e auditável por si mesma — reconstituir o snapshot vigente de uma versão antiga exigiria lógica adicional de reversão, em vez de apenas ler um nó imutável da cadeia.

## Mais informações

- [ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md) — princípio append-only que `snapshot_publicacao` também segue.
- [ADR-0075](0075-snapshot-do-ato-resolvido-no-instante.md) — seletor de snapshot vigente, que resolve qual nó da cadeia governa cada instante.
- [ADR-0076](0076-contrato-snapshot-runtime-espelha-publicacao.md) — o runtime lê o snapshot congelado; a retificação é o que faz esse snapshot evoluir de forma auditável.
- [ADR-0100](0100-canonicalizacao-hash-snapshot-publicacao.md) — contrato de canonicalização e hash que o snapshot de retificação também segue.
- [ADR-0046](0046-validacao-de-regras-sem-excecao-result-failure.md) — violação do contrato abertura×retificação é `Result.Failure(DomainError)`, sem exceção.
- Issue #759 (Story) §3 e §6 — cenários BDD do contrato abertura×retificação, promovidos a ADR por esta issue #783 (Task T2).
- Regra de negócio **RN08** (congelamento por snapshot).
