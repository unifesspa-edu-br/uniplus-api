---
status: "accepted"
date: "2026-10-05"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0139: O front interpreta as regras do formulário publicadas pela API, e a API valida tudo no envio

## Contexto e enunciado do problema

O formulário do candidato muda conforme as respostas: um campo aparece só para quem mora em aldeia, uma seção só para quem concorre por renda, a composição familiar aceita quantos integrantes a regra admitir. Hoje só o `AvaliadorFormulario` sabe decidir isso, e o front só fica sabendo quando chama a API. A pré-visualização mostra todos os campos de uma vez e não decide nada até o clique; a inscrição real teria de ir ao servidor a cada resposta.

A [ADR-0135](0135-projeto-compartilhado-de-regras-sobre-fatos.md) e a [ADR-0137](0137-formularios-por-finalidade-e-modelos-por-tipo-de-processo.md) fixam um único avaliador para as duas pontas do backend. A [ADR-0131](0131-portal-como-bff-publico-de-dominio.md) separa o que é público do que é interno no contrato do certame, e mantém as regras de derivação do lado interno.

## Drivers da decisão

- O candidato precisa ver o formulário reagir às próprias respostas, sem ir ao servidor a cada tecla.
- O front não pode ter regra de negócio própria: o que vale é o que a configuração declara.
- O servidor continua sendo a única autoridade sobre o que é aceito.
- Duas implementações da mesma semântica divergem se nada as amarra.

## Opções consideradas

- **A.** Avaliar na API a cada resposta, com espera entre teclas.
- **B.** O front interpreta as regras publicadas no formulário renderizável; a API revalida tudo no envio.
- **C.** Compilar o avaliador do servidor para rodar no navegador.

## Resultado da decisão

**Escolhida:** "B", porque dá ao candidato um formulário que reage na hora sem dar ao front nenhuma regra própria: ele só interpreta o vocabulário do contrato, e o que decide continua declarado na configuração e conferido pelo servidor.

A semântica passa a existir em duas implementações. Três coisas as mantêm iguais:

- **O formulário renderizável é completo.** Um conversor de mão dupla no projeto de regras converte a definição avaliável em renderizável e o renderizável de volta em definição. Para as mesmas respostas, as duas definições têm de dar a mesma avaliação. Regra nova que não chegue ao contrato derruba esse teste.
- **Um corpus de casos é compartilhado.** Cada caso traz o formulário renderizável, as respostas e o resultado esperado. O mesmo arquivo roda contra o avaliador do servidor e contra o interpretador do front.
- **A API revalida no envio.** O interpretador antecipa; a recusa do servidor é a palavra final.

Para isso, o contrato público do formulário passa a declarar os derivados e os agregados **que o próprio formulário cita**, com o fechamento das dependências, além do domínio dos fatos citados, dos pressupostos e dos códigos com que o avaliador identifica etapa e termo. Quem preenche já observa o comportamento do formulário diante dessas regras, então publicá-las não revela nada novo. O que decide o resultado do certame e não é citado pelo formulário continua interno, como manda a ADR-0131.

## Consequências

### Positivas

- O formulário reage às respostas no navegador, na simulação e na inscrição real, com o mesmo interpretador.
- Simular um formulário a partir de um arquivo JSON, sem cadastro, passa a ser possível no front e na API.
- A completude do contrato vira teste automático, e não revisão manual.

### Negativas

- Toda mudança na semântica do avaliador passa a exigir a mesma mudança no interpretador e um caso no corpus.
- O contrato público do formulário cresce e muda de versão.

## Confirmação

- O teste de ida e volta do conversor passa sobre os cenários do avaliador e do corpus.
- Um teste de contrato garante que nenhum derivado não citado pelo formulário aparece no contrato público.
- O corpus roda nos testes da API e do front; caso que passa num lado e falha no outro bloqueia o merge.

## Prós e contras das opções

### A. Avaliar na API a cada resposta

- Bom, porque mantém uma implementação só.
- Ruim, porque faz o formulário depender da rede a cada resposta e não permite simular um arquivo sem servidor.

### B. Interpretar no front as regras publicadas

- Bom, porque reage na hora e reaproveita o mesmo interpretador na simulação e na inscrição.
- Ruim, porque cria uma segunda implementação, que só fica fiel com o corpus e o conversor de mão dupla.

### C. Compilar o avaliador para o navegador

- Bom, porque seria uma implementação só.
- Ruim, porque leva o runtime .NET ao navegador por uma função pequena e amarra o front ao ciclo de build do backend.

## Mais informações

- Issues: uniplus-api#1808 e uniplus-web#1027.
- [ADR-0131](0131-portal-como-bff-publico-de-dominio.md), [ADR-0135](0135-projeto-compartilhado-de-regras-sobre-fatos.md), [ADR-0137](0137-formularios-por-finalidade-e-modelos-por-tipo-de-processo.md), [ADR-0138](0138-grupo-repetivel-como-fonte-das-instancias-de-exigencia.md).
