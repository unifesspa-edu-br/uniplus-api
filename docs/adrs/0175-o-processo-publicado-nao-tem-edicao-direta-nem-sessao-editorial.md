---
status: "accepted"
date: "2026-11-19"
decision-makers:
  - "Tech Lead"
consulted: []
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0175: O processo publicado não tem edição direta nem sessão editorial; a retificação entra por um envio único

## Contexto e enunciado do problema

A [ADR-0110](0110-retificacao-como-sessao-editorial.md) fez da retificação uma sessão editorial: um rascunho persistido ao lado do processo publicado, editado por rotas de gravação por dimensão com `If-Match`, descartado por uma reposição verificada ou fechado por uma conferência própria. Essa sessão exigiu uma estrutura paralela de estado (o rascunho, a revisão, o `ETag`, a base da retificação), uma segunda via de reidratação da configuração (a reposição, com sombra de verificação) e regras de gravação que só valiam sob a sessão.

A conferência única da configuração ([ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md)) e o documento de configuração versionado ([ADR-0172](0172-esquema-json-versionado-do-documento-de-configuracao.md)) tornam a sessão desnecessária: a retificação passa a ser uma declaração completa da configuração, conferida por inteiro contra a versão vigente e congelada numa versão nova. Manter a sessão deixaria duas formas de retificar e duas formas de conferir a mesma configuração.

## Drivers da decisão

- Uma única forma de retificar, com uma única conferência, a do congelamento.
- O processo publicado é imutável até a versão seguinte: nenhum estado editável paralelo ao congelado.
- A concorrência é decidida pelo bloqueio da linha do processo e pela chave de idempotência, e não por uma revisão que só existia por causa da sessão.
- Sem estrutura, rota ou código de erro sem emissor.

## Opções consideradas

- Manter a sessão editorial e a reposição ao lado do envio único.
- Retirar a sessão e a reposição, e retificar só pelo envio único do documento de configuração.

## Resultado da decisão

**Escolhida:** "Retirar a sessão e a reposição, e retificar só pelo envio único do documento de configuração", porque a conferência do congelamento já cobre tudo o que a sessão conferia, e a sessão só acrescentava estado e vias paralelas.

1. O processo só aceita gravação da configuração em rascunho. Publicado, a gravação é recusada com `mutacao_pos_publicacao_bloqueada`; em qualquer outro estado que não seja rascunho, com `mutacao_fora_de_estado_editavel`. A recusa precede a leitura de cadastros.
2. Não existem abrir, alterar, descartar nem fechar retificação. O processo publicado muda por uma versão nova, congelada pela retificação a partir do documento de configuração enviado de uma vez. O processo não guarda rascunho de retificação, nem revisão, nem `ETag` da configuração.
3. A leitura do envelope publicado permanece: o registro de codecs por versão ([ADR-0110](0110-retificacao-como-sessao-editorial.md) D1, a Emenda 2 e a emenda da [ADR-0137](0137-formularios-por-finalidade-e-modelos-por-tipo-de-processo.md)) continua governando como as versões publicadas são lidas, reidratadas e recanonicalizadas para conferência. A reposição da configuração congelada no processo vivo, com sombra de verificação, deixa de existir.
4. As gravações por dimensão perdem o `If-Match`: só atingem rascunho, e o controle que vale é o bloqueio da linha do processo, a chave de idempotência e a espera esgotada pelo bloqueio. O `If-Match` e o `ETag` ficam na inscrição e nas leituras do certame.
5. Três regras que a sessão aplicava na gravação passam a ser conferidas no congelamento da retificação, contra as versões publicadas: o fato que o formulário usa de outra finalidade tem de ter sido coletado em todas as versões publicadas; o fato já coletado não é retirado; o identificador legível já publicado não muda. A inclusão de motivo depois da publicação, com justificativa, e a sua retirada passam a ser conferidas pelo documento da retificação, e não por rotas de gravação.

## Consequências

### Positivas

- Uma só via de retificação e uma só conferência da configuração.
- Saem do modelo a tabela do rascunho de retificação, a revisão e o `ETag` da sessão, e com eles as rotas, os comandos e os códigos de erro da sessão.
- As rotas de gravação por dimensão ficam sem cabeçalho condicional, com um contrato mais simples.

### Negativas

- Até a retificação pelo documento entrar, o processo publicado não tem como mudar a configuração, apenas retificar o ato.
- O cliente que usava a sessão editorial deixa de ter rascunho de retificação persistido.

### Neutras

- A migration inicial do módulo é regerada sem a tabela do rascunho de retificação.
- A [ADR-0110](0110-retificacao-como-sessao-editorial.md) passa a substituída; o que ela decidiu sobre codecs e identidade das filhas continua valendo como registro, no escopo do item 3.

## Confirmação

Testes de unidade fixam, por dimensão, a recusa em processo publicado e em processo encerrado e a gravação em rascunho sem `If-Match`; fixam também a recusa, no congelamento, do fato ausente de versão publicada e do fato coletado retirado, a conferência do identificador imutável e a da justificativa do motivo. Os testes de arquitetura de catálogo de erros e de baseline do OpenAPI garantem que nenhum código ou rota da sessão sobrevive.

## Prós e contras das opções

### Manter a sessão editorial ao lado do envio único

- Bom, porque preserva o fluxo de edição incremental.
- Ruim, porque duplica o estado, a reidratação e as regras de conferência, e a sessão só edita o que o envio único declara por inteiro.

### Retirar a sessão e retificar só pelo envio único

- Bom, porque há uma conferência e uma via de retificação.
- Ruim, porque o processo publicado não muda a configuração até a retificação pelo documento existir.

## Mais informações

- Substitui a [ADR-0110](0110-retificacao-como-sessao-editorial.md). Emenda a [ADR-0174](0174-conferencia-unica-da-configuracao-do-processo-com-todas-as-causas.md).
- Relacionadas: [ADR-0119](0119-padrao-concorrencia-otimista-handlers-wolverine.md), [ADR-0125](0125-dominio-fonte-unica-validacao-sem-fluentvalidation-duplicado.md), [ADR-0172](0172-esquema-json-versionado-do-documento-de-configuracao.md).
