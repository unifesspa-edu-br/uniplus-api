---
status: "proposed"
date: "2026-10-10"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0143: A API avalia sob demanda o que depende dos fatos do candidato

## Contexto e enunciado do problema

As respostas do candidato determinam as modalidades em que ele concorre (UNI-REQ-0076) e os documentos que o edital exige dele em cada fase (UNI-REQ-0069, UNI-REQ-0077). Ele precisa conhecer as duas coisas enquanto preenche a inscrição, para conferir e, se for o caso, corrigir as respostas. O comprovante registra o que o envio decidiu.

## Drivers da decisão

- Cada regra tem uma única implementação.
- A matriz de derivação e a árvore de exigências não vão ao navegador (ADR-0131).
- O servidor é a autoridade sobre o que é aceito e o que é exigido (ADR-0139).

## Opções consideradas

- **A.** O front recebe a matriz e a árvore e as interpreta.
- **B.** A API avalia sob demanda, a partir das respostas do candidato e da versão do processo que a inscrição fixou.
- **C.** Mostrar só no comprovante, depois do envio.

## Resultado da decisão

**Escolhida:** "B". A mesma avaliação responde ao candidato durante o preenchimento, acompanha o rascunho e decide o envio, que grava no comprovante as modalidades em que ele concorre.

## Consequências

### Positivas

- O candidato confere modalidades e documentos antes de enviar.
- O envio de documentos poderá usar a mesma avaliação.

### Negativas

- A consulta depende da rede a cada mudança relevante de resposta.
- As respostas do titular vão ao servidor numa consulta que não grava, sob o mesmo acesso com que ele grava o rascunho.

## Prós e contras das opções

### A. Interpretar no front

- Bom, porque responde sem rede.
- Ruim, porque leva a matriz e a árvore ao navegador e cria uma segunda implementação de duas regras.

### B. Avaliar sob demanda na API

- Bom, porque mantém uma implementação de cada regra.
- Ruim, porque depende da rede.

### C. Só no comprovante

- Bom, porque não exige consulta.
- Ruim, porque o candidato só descobre depois de enviar.

## Mais informações

- [ADR-0131](0131-portal-como-bff-publico-de-dominio.md), [ADR-0139](0139-interpretador-de-regras-de-formulario-no-front.md), [ADR-0141](0141-respostas-da-inscricao-em-tabela.md).
