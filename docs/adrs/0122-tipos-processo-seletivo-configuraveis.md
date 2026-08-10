---
status: "accepted"
date: "2026-08-10"
decision-makers:
  - "Tech Lead"
  - "Product Owner"
consulted:
  - "CEPS"
informed:
  - "Equipe Seleção"
  - "Equipe Plataforma"
---

# ADR-0122: Tipos de processo seletivo configuráveis

## Contexto e enunciado do problema

`TipoProcesso` era um enum compilado no módulo Seleção. Isso impedia que a
Plataforma registrasse um novo tipo sem release e fazia o ruleset legal validar
contra vocabulário de código, e não contra dado administrativo vigente. Além
disso, uma publicação precisava provar qual tipo vigorava no processo, sem
relê-lo do cadastro mutável.

## Drivers da decisão

- Permitir manutenção administrativa com código estável e rastreável.
- Impedir que desativação ou edição altere processos e publicações existentes.
- Manter a dependência cross-módulo como leitura por contrato, sem FK entre schemas.
- Preservar `*` como aplicabilidade legal universal.

## Opções consideradas

- Manter o enum e criar somente endpoint de leitura.
- Manter os tipos em Seleção com FK direta para o processo.
- Manter os tipos em Configuração, consumir por reader e congelar cópia por valor.

## Resultado da decisão

**Escolhida:** "Manter os tipos em Configuração, consumir por reader e congelar cópia
por valor", porque separa a administração da configuração do processo sem
acoplamento de banco e mantém a evidência histórica autocontida.

`TipoProcesso` passa a ser cadastro de Configuração, com `Codigo`, `Nome`,
descrição opcional e `Ativo`. Apenas `plataforma-admin` pode criar, atualizar ou
desativar. O código é imutável, único inclusive entre itens desativados e `*` é
reservado. A leitura pública lista e obtém somente itens ativos.

Na criação, Seleção recebe `tipoProcessoOrigemId`, resolve um item ativo por
`ITipoProcessoReader` e persiste, sem FK cross-schema, o id e o snapshot
`{codigo, nome}`. Processos existentes são migrados pelos oito códigos legados;
o valor enum `Nenhum` ou outro valor sem mapa interrompe a migration.

`ObrigatoriedadeLegal.TipoProcessoCodigo` tem validação estrutural no domínio e
validação de pertença a um tipo ativo no handler. `*` não consulta os tipos ativos.
Depois de gravada, a regra guarda seu código; a avaliação usa exclusivamente o
snapshot do processo, e não a configuração atual. O envelope canônico inclui o bloco
`tipoProcesso` com id de origem, código e nome.

Esta decisão emenda a ADR-0056 quanto à residência de `TipoProcesso` e emenda a
ADR-0114 quanto ao vocabulário fechado. Ela não altera a decisão temporal do
ruleset desta última.

## Consequências

### Positivas

- Novos tipos não exigem alteração de enum nem deploy de frontend para existir na API.
- Desativar bloqueia novos vínculos, preservando referências e provas anteriores.
- O ruleset e o snapshot publicados permanecem reproduzíveis após mudanças nos tipos ativos.

### Negativas

- A criação do processo e a manutenção de regras fazem leitura cross-módulo adicional.
- O rollback da migration de Seleção é bloqueado se já houver tipo sem equivalente no enum legado.

### Neutras

- Os oito códigos legados são seed inicial, sem proteção especial contra desativação.

## Confirmação

- Testes de API provam GET público ativo e escritas restritas a `plataforma-admin`.
- Testes de handler recusam tipo inexistente ou inativo e congelam a cópia recebida.
- Testes de ruleset aceitam `*`, exigem tipo ativo específico e usam o código do snapshot.
- Round-trip do envelope exige e preserva o bloco `tipoProcesso`.

## Mais informações

- UNI-REQ-0098 — tipos de processo seletivo configuráveis.
- ADR-0056 — Configuração e readers cross-módulo.
- ADR-0061 — referência cross-módulo por snapshot-copy.
- ADR-0109 — envelope canônico do congelamento.
- ADR-0114 — ruleset de conformidade legal.
