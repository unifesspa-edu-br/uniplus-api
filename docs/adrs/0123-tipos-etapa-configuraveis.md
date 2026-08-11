---
status: "accepted"
date: "2026-08-11"
decision-makers:
  - "Tech Lead"
  - "Product Owner"
consulted:
  - "CEPS"
informed:
  - "Equipe Seleção"
  - "Equipe Plataforma"
---

# ADR-0123: Tipos de etapa configuráveis

## Contexto e enunciado do problema

`AvaliadorConformidadeLegal.AvaliarEtapaObrigatoria` comparava `EtapaProcesso.Nome`
— texto livre editável pelo administrador — contra `PredicadoObrigatoriedade
.EtapaObrigatoria.TipoEtapaCodigo`. Uma edição de rótulo podia alterar
indevidamente o resultado de conformidade legal, e duas etapas com o mesmo
propósito institucional (ex. "Prova Objetiva" e "1ª Fase — Prova") não tinham
identidade estável em comum. O enum `TipoEtapa` que a comparação deveria ter
usado estava órfão: declarado, nunca referenciado como tipo em nenhum lugar do
código.

## Drivers da decisão

- Fazer a conformidade legal depender de identidade estável, não de rótulo editorial.
- Permitir manutenção administrativa do vocabulário de tipos de etapa sem release.
- Manter a dependência cross-módulo como leitura por contrato, sem FK entre schemas.
- Não construir camada de compatibilidade para dado que não existe: o sistema não
  está em produção em nenhum ambiente.

## Opções consideradas

- Promover o enum `TipoEtapa` a entidade dentro do próprio módulo Seleção.
- Manter os tipos em Configuração, consumir por reader e congelar cópia por valor
  — mesmo padrão já estabelecido para `TipoProcesso` (ADR-0122).
- Vincular `EtapaProcesso` a `FaseCanonica` (também em Configuração) em vez de um
  cadastro próprio.

## Resultado da decisão

**Escolhida:** "Manter os tipos em Configuração, consumir por reader e congelar
cópia por valor", pelo mesmo motivo da ADR-0122 — separa a administração do
vocabulário da configuração do processo, sem acoplamento de banco, e mantém a
evidência histórica autocontida. A opção de vincular a `FaseCanonica` foi
descartada porque fase é estrutura **temporal** (o eixo do cronograma) e etapa é
componente **pontuável** (peso, caráter, nota mínima) — grandezas distintas
(ADR-0113).

`TipoEtapa` passa a ser cadastro de Configuração, com `Codigo`, `Nome`, descrição
opcional e `Ativo`. Apenas `plataforma-admin` pode criar, atualizar ou desativar.
O código é imutável e único inclusive entre itens desativados, comparado com
`StringComparison.Ordinal`. Diferente de `TipoProcesso`, não há token reservado
equivalente a `*`: todo predicado `EtapaObrigatoria` referencia um tipo
específico, nunca uma aplicabilidade universal. A leitura pública lista e obtém
somente itens ativos.

Toda definição de etapa (`DefinirEtapasCommand`) passa a exigir
`tipoEtapaOrigemId`. O handler resolve um item ativo por `ITipoEtapaReader` e
`EtapaProcesso` persiste, sem FK cross-schema, o snapshot `{origemId, codigo,
nome}` — campo **obrigatório desde a primeira migration**, sem coluna nullable
transitória: ao contrário da migração de `TipoProcesso` (enum fechado de 8
valores com mapeamento determinístico), não havia dado legado real a
preservar — o enum `TipoEtapa` nunca esteve vinculado a nenhuma linha de
`EtapaProcesso`, e o sistema não tem certame publicado em ambiente nenhum.
`AvaliarEtapaObrigatoria` passa a comparar `EtapaProcesso.TipoEtapa.Codigo`
contra `PredicadoObrigatoriedade.EtapaObrigatoria.TipoEtapaCodigo`, nunca mais o
rótulo editorial. `ObrigatoriedadeLegal` com esse predicado tem validação de
pertença a um tipo ativo no handler, na criação e na atualização; depois de
gravada, a regra guarda seu código, e a avaliação usa exclusivamente o snapshot
da etapa, não a configuração atual.

O envelope canônico avança de `0.0.7` para `0.0.8`: cada item do bloco `etapas`
ganha o snapshot aninhado `tipoEtapa` (mesmo shape do bloco de topo
`tipoProcesso`), sem criar novo bloco (permanecem 24). Sem produção em nenhum
ambiente, o avanço não cria decodificador de compatibilidade retroativa — mesma
política já aplicada ao bump anterior (0.0.6→0.0.7, ADR-0122): fixture nova,
`0.0.7` deixa de ser reconhecida.

O enum `TipoEtapa.cs`, órfão, é removido no mesmo incremento. Uma fitness
function (`SelecaoSemRamificacaoPorTipoEtapaTests`) trava as duas regressões
possíveis: o enum voltar a existir como fonte de verdade, e o avaliador voltar a
acessar `EtapaProcesso.Nome`.

Esta decisão emenda a ADR-0056 quanto à residência de `TipoEtapa` e emenda a
ADR-0114 quanto à comparação de `EtapaObrigatoria`. Ela não altera a decisão
temporal do ruleset da ADR-0114.

## Consequências

### Positivas

- A conformidade legal por etapa obrigatória deixa de depender do texto digitado
  pelo administrador no rótulo editorial da etapa.
- Novos tipos de etapa não exigem alteração de enum nem deploy para existir na API.
- Desativar um tipo bloqueia novos vínculos, preservando referências e provas
  já congeladas.

### Negativas

- A definição de etapas e a manutenção de `ObrigatoriedadeLegal` com predicado
  `EtapaObrigatoria` fazem leitura cross-módulo adicional.
- O snapshot obrigatório sem fase de transição significa que qualquer banco local
  ou de homologação com dado incompatível com o novo schema precisa ser recriado
  ao adotar esta mudança — aceitável apenas porque não há produção.

### Neutras

- Os sete códigos legados (`PROVA_OBJETIVA`, `REDACAO`, `ENTREVISTA`,
  `ANALISE_HISTORICO`, `BANCA_HETEROIDENTIFICACAO`, `ANALISE_DOCUMENTAL`,
  `NOTA_ENEM`) são seed inicial, sem proteção especial contra desativação —
  mesmo tratamento dado aos oito códigos legados de `TipoProcesso` (ADR-0122).

## Confirmação

- Testes de API provam GET público ativo e escritas restritas a `plataforma-admin`.
- Testes de handler recusam tipo inexistente ou inativo, tanto na definição de
  etapa quanto na criação/atualização de `ObrigatoriedadeLegal`.
- Testes do avaliador provam que o rótulo editorial não interfere na conformidade
  e que nome igual ao código não substitui a identidade do tipo.
- Fitness function trava a recriação do enum e o retorno da comparação por nome.
- Round-trip do envelope `0.0.8` exige e preserva o bloco `etapas[].tipoEtapa`.

## Mais informações

- UNI-REQ-0015, UNI-REQ-0087 — requisitos de produto relacionados.
- ADR-0056 — Configuração e readers cross-módulo.
- ADR-0061 — referência cross-módulo por snapshot-copy.
- ADR-0109 — envelope canônico do congelamento.
- ADR-0113 — fase × etapa: eixo temporal e eixo de pontuação.
- ADR-0114 — ruleset de conformidade legal.
- ADR-0122 — tipos de processo seletivo configuráveis (precedente estrutural direto).
