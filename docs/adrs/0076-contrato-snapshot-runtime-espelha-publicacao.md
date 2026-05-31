---
status: "accepted"
date: "2026-05-31"
decision-makers:
  - "Tech Lead"
---

# ADR-0076: A validação do snapshot lido em runtime reproduz, integralmente, a validação aplicada à configuração na publicação

## Contexto e enunciado do problema

A validação documental lê o snapshot congelado ([ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md)). Se essa leitura conferisse apenas a **estrutura** do snapshot (formato e tipos), mas não a **semântica** — que o gatilho de aplicabilidade referencia fatos e valores válidos do vocabulário do sistema —, um snapshot semanticamente inválido seria avaliado em silêncio: a exigência correspondente simplesmente desapareceria do resultado, mascarando configuração inválida como "sem pendências".

A configuração viva já é validada integralmente no momento da publicação. O que é lido em runtime deve receber o **mesmo rigor**, sob pena de o runtime aceitar como válido o que a publicação rejeitaria.

A questão a decidir é: **qual o contrato de validação do snapshot lido em runtime** — apenas estrutural ou estrutural e semântico, espelhando a validação de publicação.

## Drivers da decisão

- **Não mascarar configuração inválida.** Um snapshot inválido jamais pode produzir resultado vazio silencioso.
- **Consistência com a publicação.** O contrato lido em runtime deve reproduzir a mesma validação aplicada à configuração antes da publicação.
- **Preservar a imunidade da ADR-0070.** A validação semântica deve ler apenas o **vocabulário global do sistema** (catálogo estável de fatos), nunca a configuração por processo do edital — que permanece congelada.

## Opções consideradas

- **A. A validação em runtime reproduz integralmente a validação de configuração** — estrutura e semântica — e emite erro nomeado ao encontrar configuração inválida, nunca resultado vazio silencioso.
- **B. Validar apenas a estrutura** — mascara semântica inválida como ausência de pendências.
- **C. Declarar o contrato de runtime apenas estrutural** — inconsistente com a publicação e aceita o mascaramento.

## Resultado da decisão

**Escolhida:** "A — runtime reproduz integralmente a validação da publicação", porque é a única que impede o mascaramento e mantém runtime e publicação consistentes.

Antes de avaliar as exigências, a validação em runtime confere o snapshot em duas dimensões, reproduzindo a mesma regra aplicada à configuração viva na publicação:

- **Estrutura:** chaves, tipos e identificadores (por exemplo, a forma do identificador de exigência).
- **Semântica:** cada condição do gatilho referencia um fato existente no vocabulário do sistema, com operador e valor compatíveis com o domínio desse fato.

Configuração ausente ou inválida produz **erro nomeado** — nunca resultado vazio. A validação semântica consulta **apenas o vocabulário global do sistema** (catálogo estável de fatos): por ler uma referência estável e global, e não a configuração por processo do edital (que continua congelada), não fere a imunidade da ADR-0070.

## Consequências

### Positivas

- Configuração inválida nunca é mascarada como "sem pendências".
- O contrato de runtime é consistente com o da publicação — o mesmo veredito em ambos.
- Erros explícitos e nomeados em vez de resultado vazio silencioso.

### Negativas

- A validação semântica em runtime passa a consultar o vocabulário global (referência estável), acréscimo de leitura aceito por ser catálogo, não configuração por processo.

## Confirmação

- **Erro nomeado em snapshot inválido:** um snapshot com gatilho estrutural ou semanticamente inválido produz erro nomeado — no envio e no acompanhamento —, nunca resultado vazio.
- **Snapshot válido passa.**
- **Paridade com a publicação:** o veredito (numérico e categórico) da validação em runtime coincide com o da validação aplicada à configuração na publicação.

## Mais informações

- Requisito de rastreabilidade: **UNI-REQ-0063**.
- [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md) — a leitura do snapshot congelado, cuja integridade este contrato de validação assegura sem ferir a imunidade do processo publicado.
- Regra de negócio **RN08** — a configuração por processo permanece congelada; a validação semântica em runtime lê apenas o vocabulário global do sistema.
