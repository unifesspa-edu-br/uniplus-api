---
status: "proposed"
date: "2026-11-10"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0159: Ação interna da fase decidida na unidade responsável pela fase

## Contexto e enunciado do problema

A unidade responsável pelo processo seletivo cadastra, publica e retifica os dados gerais e o cronograma. A unidade responsável por cada fase (`FaseCronograma.UnidadeResponsavel`) conduz as ações internas da fase, mesmo quando não é a dona do processo: publica os documentos dela, designa as bancas dela, julga os recursos dela e conduz chamada, habilitação e matrícula. A etapa segue a unidade da fase que a contém (UNI-REQ-0329 a UNI-REQ-0343).

Hoje toda decisão de acesso da Seleção monta o resource com a unidade dona. A unidade da fase é uma cópia congelada no cronograma e nenhum ponto de acesso a lê. Como o motor exige concessão na mesma unidade do resource, sem herança ([ADR-0079](0079-hierarquia-institucional-sem-heranca-de-permissao.md)), quem conduz uma fase de outra unidade precisaria de concessão na unidade dona, o que contraria a regra de negócio.

## Drivers da decisão

- Manter o motor de autorização como está: uma concessão comparada com uma unidade.
- Decidir pela unidade que o edital publicou, e não pela do rascunho de uma retificação em curso.
- Não haver fase que se resolve "quase": a ação de fase ou decide na unidade da fase, ou recebe a resposta de quem não encontra.

## Opções consideradas

- Manter a decisão na unidade dona e exigir concessão da unidade da fase na dona.
- Herdar a permissão pela hierarquia ou por escopo de auditoria.
- Decidir a ação interna na unidade da fase, lida da versão publicada vigente.

## Resultado da decisão

**Escolhida:** "decidir a ação interna na unidade da fase, lida da versão publicada vigente", porque preserva o motor e a ADR-0079 e põe a decisão onde a regra de negócio a coloca.

- A Seleção monta o resource da ação da fase com a unidade da fase no lugar da dona, sempre com o `ProcessoId`, para que o impedimento por inscrição valha. A etapa decide pela unidade da fase que a contém. A ação que por natureza não tem fase decide na dona.
- A unidade da fase e a fase de uma etapa são lidas do envelope da versão publicada vigente, nunca das entidades vivas durante a sessão de retificação. A troca de unidade de uma fase que não começou passa a valer quando a retificação fecha.
- A ação de fase cuja fase não se resolve (inexistente, de outro processo ou sem unidade na versão vigente) recebe o 404 do resource inexistente, sem recorrer à dona, sem consultar o ponto de decisão e sem registro. A negativa por falta de permissão na unidade da fase e o impedimento por inscrição são registrados; a consulta de capacidade não registra; a falha ao publicar a negativa responde 503 ([ADR-0153](0153-projecao-por-protecao.md)).
- A publicação e a retificação recusam a etapa que não declara a fase; o rascunho a aceita. A retificação que troca a unidade de uma fase iniciada é recusada. Fase iniciada é a que tem a janela publicada começada ou, sem janela, a que já tem ato ou registro; a encerrada conta como iniciada (UNI-REQ-0332 e UNI-REQ-0333).
- A permissão `selecao:documentos-da-fase:publicar` entra no catálogo ([ADR-0151](0151-catalogo-de-permissoes-em-codigo-por-modulo.md)) e é concedida pelo fluxo da [ADR-0152](0152-governanca-das-concessoes-de-acesso.md).

## Consequências

### Positivas

- A unidade que conduz a fase atua com concessão na própria unidade, sem herança e sem escopo de auditoria.
- O motor não muda; o Ingresso e as demais frentes reutilizam a mesma peça.

### Negativas

- Cada operação de fase precisa ser migrada para a peça, uma a uma, em issues próprias.
- Etapas sem fase declarada deixam de ser publicáveis.

## Confirmação

Testes diretos da peça de decisão e dos leitores da versão vigente, e teste de integração da sessão de retificação aberta, do fechamento e da troca de unidade de fase iniciada.

## Prós e contras das opções

### Decidir na dona

- Ruim, porque exige concessão na unidade dona para quem conduz a fase de outra unidade.

### Herdar pela hierarquia ou por auditoria

- Ruim, porque reintroduz a herança que a ADR-0079 rejeitou.

### Decidir na unidade da fase

- Bom, porque a decisão acompanha a regra de negócio sem tocar no motor.
- Ruim, porque cada operação precisa declarar se é de fase.

## Mais informações

- Complementa a [ADR-0079](0079-hierarquia-institucional-sem-heranca-de-permissao.md), a [ADR-0104](0104-versao-configuracao-como-agregado-proprio.md), a [ADR-0113](0113-fase-x-etapa-eixo-temporal-e-eixo-de-pontuacao.md) e a [ADR-0153](0153-projecao-por-protecao.md).
