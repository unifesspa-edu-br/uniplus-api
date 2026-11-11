---
status: "accepted"
date: "2026-11-11"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0160: Supressão de aviso do analisador proibida

## Contexto e enunciado do problema

A [ADR-0117](0117-politica-de-analise-estatica-e-supressao.md) escolhe o mecanismo de supressão pelo alcance da exceção e admite `[SuppressMessage]` no símbolo e `#pragma warning disable` em uma ou duas linhas, sempre com justificativa. Na prática, a supressão virou o caminho padrão: centenas de atributos calam o mesmo aviso pelo mesmo motivo, e avisos que apontavam design ruim foram silenciados em vez de corrigidos. Cada supressão nova repete a anterior, e a revisão não tem critério para recusá-la.

## Drivers da decisão

- O aviso do analisador aponta um problema do código; calá-lo esconde o problema.
- Uma regra que não se aplica ao projeto é decisão de projeto, tomada uma vez e num lugar só.
- A revisão precisa de critério objetivo para recusar supressão.

## Opções consideradas

- Manter a supressão por alcance da ADR-0117.
- Proibir a supressão por símbolo e por linha, mantendo o desligamento da regra por projeto ou por camada no `.editorconfig`.
- Proibir também o desligamento no `.editorconfig`.

## Resultado da decisão

**Escolhida:** proibir a supressão por símbolo e por linha, mantendo o desligamento por projeto ou por camada no `.editorconfig`, porque o aviso pontual se corrige no código e a regra que não se aplica ao projeto continua sendo decisão legítima, registrada com justificativa.

- Fica proibido calar aviso com `[SuppressMessage]`, `GlobalSuppressions.cs`, `#pragma warning disable` ou `<NoWarn>`.
- O aviso se corrige no código. Quando o analisador revela design ruim, o design é refeito.
- Desligar no `.editorconfig` uma regra que não se aplica ao projeto inteiro ou a uma camada inteira (código com o mesmo papel, como testes, controllers ou configuração do EF, selecionado por glob de caminho) continua permitido, com comentário justificando. Seção que isenta um arquivo ou um tipo específico, e não uma camada inteira, é supressão disfarçada e também é proibida.
- Código gerado (migrations do EF) segue fora: as supressões vêm da ferramenta.

Esta ADR emenda a ADR-0117 na tabela de mecanismos por alcance e na regra de desempate, a [ADR-0048](0048-controllers-mvc-public-com-ca1515-suprimido.md) na forma de tratar o CA1515 dos controllers (a camada de controllers, que o MVC exige pública, é decisão de camada no `.editorconfig`, e não atributo em cada classe), a [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md) na supressão do CA1062 no domínio e a [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md) na supressão do CA1707 nas classes Avro. Vale para código novo ou alterado; as supressões existentes são dívida, paga por módulo.

## Consequências

### Positivas

- A revisão recusa toda supressão nova sem discussão caso a caso.
- O aviso que revela design ruim leva à correção do design.

### Negativas

- As supressões existentes precisam ser removidas, e parte delas exige mudança estrutural, como a visibilidade dos handlers.

### Neutras

- O desligamento de regra no `.editorconfig` continua com o critério de projeto ou camada da ADR-0117.

## Confirmação

A revisão de PR recusa supressão nova. Quando um módulo zera a dívida, o CI passa a barrar supressão naquele módulo.

## Mais informações

- [ADR-0117](0117-politica-de-analise-estatica-e-supressao.md): política de análise estática, emendada aqui.
- [ADR-0048](0048-controllers-mvc-public-com-ca1515-suprimido.md): controllers públicos, emendada aqui quanto ao mecanismo.
- [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md): validação no domínio, emendada aqui quanto à supressão do CA1062.
- [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md): classes Avro, emendada aqui quanto à supressão do CA1707.
- [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md): limites de tamanho e complexidade.
