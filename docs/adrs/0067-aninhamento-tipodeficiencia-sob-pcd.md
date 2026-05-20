---
status: "accepted"
date: "2026-05-19"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0067: Aninhamento de TipoDeficiencia sob a condição PCD na oferta de atendimento especializado

## Contexto e enunciado do problema

Ao configurar um edital, o CEPS define qual **atendimento especializado** o processo oferece. Essa oferta tem três dimensões distintas que o protótipo legado tratava de forma plana e ambígua:

1. As **condições** aceitas para solicitar atendimento (PCD, dislexia, TDAH, lactante, etc.).
2. Os **tipos de deficiência** reconhecidos — que só fazem sentido **dentro** da condição PCD.
3. Os **recursos de acessibilidade** oferecidos (ledor, prova ampliada, intérprete de Libras, tempo adicional, etc.), independentes das condições.

O termo institucional adotado no Uni+ é **"atendimento especializado"** (alinhado ao INEP / Edital ENEM 52/2025), descartando "atendimento diferenciado/especial" e "necessidade especial". Há três entidades de domínio distintas no tema: `TipoDeficiencia`, `RecursoAcessibilidade` e `SolicitacaoAtendimentoEspecializado` (esta última no lado do candidato, na inscrição).

O problema de modelagem: `TipoDeficiencia` é uma **dimensão ortogonal** à lista de condições (cada uma é uma entidade independente), ou é um **sub-detalhamento da condição PCD** (só existe quando PCD é aceita)? A escolha errada permite estados inválidos — por exemplo, tipos de deficiência selecionados sem que PCD seja uma condição aceita, ou PCD aceita sem nenhum tipo de deficiência reconhecido.

## Drivers da decisão

- **Impossibilitar estado inválido por construção**, em vez de depender de validação defensiva espalhada.
- **Alinhamento com modelos brasileiros consolidados** de atendimento especializado em processos seletivos.
- **UX coerente**: a tela não deve pedir ao operador que mantenha duas listas sincronizadas manualmente.
- **Integridade de auditoria (RN08)**: o que foi ofertado precisa ser reconstruível na publicação do edital.

## Opções consideradas

- **PCD derivada dos tipos de deficiência** — a condição PCD é inferida quando há ao menos um tipo de deficiência selecionado; não existe flag PCD própria.
- **TipoDeficiencia como dimensão ortogonal** — lista de tipos de deficiência independente da lista de condições, com validação cruzada garantindo coerência.
- **TipoDeficiencia aninhada sob PCD** — os tipos de deficiência vivem dentro de um bloco `detalhes_pcd` que só existe quando PCD está entre as condições aceitas.

## Resultado da decisão

**Escolhida:** "TipoDeficiencia aninhada sob PCD", porque torna o estado inválido **inalcançável por construção** e alinha-se aos modelos brasileiros de referência (INEP/ENEM, banca Cebraspe/UnB e a estrutura da LBI — Lei 13.146/2015).

Forma canônica da oferta de atendimento especializado do edital:

```text
AtendimentoEspecializadoOferta {
  condicoesAceitas: CondicaoAtendimentoEspecializado[]   // ex.: PCD, DISLEXIA, TDAH, LACTANTE…
  detalhesPcd: { tiposDeficiencia: TipoDeficiencia[] } | null   // existe apenas se PCD ∈ condicoesAceitas
  recursosOferecidos: RecursoAcessibilidade[]            // independentes das condições
}
```

**Invariantes estruturais** (o modelo não admite estado inválido):

- `PCD ∈ condicoesAceitas` ⟺ `detalhesPcd.tiposDeficiencia.Count ≥ 1`
- `PCD ∉ condicoesAceitas` ⟹ `detalhesPcd` é null/ausente
- `TipoDeficiencia` jamais aparece fora de `detalhesPcd`

**Mapeamento para o `uniplus-api`:**

- `detalhesPcd` é um **value object** (sem identidade própria).
- `TipoDeficiencia` é uma **entidade dependente** — não tem identidade fora do contexto da oferta PCD; o catálogo de tipos vive em Parametrizacao (junto a `NecessidadeEspecial`/`RecursoAcessibilidade`, [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md)), mas no edital só aparece aninhado.
- A invariante `OBRIGATORIEDADE_PCD_COERENTE` é validada no domínio na construção/edição da oferta.
- **Snapshot RN08**: na publicação do edital, `detalhesPcd.tiposDeficiencia` é denormalizado no snapshot de governança (consistente com [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md)), preservando a integridade mesmo que um `TipoDeficiencia` seja inativado depois.

## Consequências

### Positivas

- Estado inválido (tipos sem PCD, ou PCD sem tipos) é inalcançável — elimina uma classe inteira de validação defensiva.
- A tela de configuração renderiza um sub-bloco condicional sob PCD, sem listas paralelas a sincronizar.
- O snapshot na publicação mantém a oferta reproduzível para auditoria retrospectiva.

### Negativas

- A migração de dados legados precisa tratar o caso patológico "PCD marcado sem tipos": registra-se uma observação de migração e um banner de pendência — **nunca** se preenche tipo arbitrariamente.
- Atrela a evolução de `TipoDeficiencia` ao contexto PCD; um eventual uso de tipos de deficiência fora de PCD exigiria reabrir esta decisão.

### Neutras

- `RecursoAcessibilidade` permanece uma lista independente, sem acoplamento às condições.

## Confirmação

- Invariante de domínio `OBRIGATORIEDADE_PCD_COERENTE` (teste de unidade cobrindo os três casos: PCD com tipos, PCD sem tipos rejeitada, tipos sem PCD rejeitados).
- Inspeção do modelo confirma que `TipoDeficiencia` só é alcançável via `detalhesPcd`.

## Mais informações

- Vocabulário canônico: **"atendimento especializado"** (INEP/Edital ENEM 52/2025); três entidades distintas — `TipoDeficiencia`, `RecursoAcessibilidade`, `SolicitacaoAtendimentoEspecializado`.
- Catálogos de referência em Parametrizacao: ver [ADR-0056](0056-parametrizacao-modulo-e-read-side-carve-out.md).
- Snapshot na publicação: ver [ADR-0061](0061-referencia-cross-modulo-via-snapshot-copy.md) e RN08 de `docs/visao-do-projeto.md`.
- **Origem:** deliberação técnica de modelagem do Módulo Configuração de Edital conduzida pelo Tech Lead (2026-05-19), fundamentada em pesquisa sobre modelos brasileiros de atendimento especializado (INEP/ENEM, Cebraspe/UnB, LBI Lei 13.146/2015); rascunhos de trabalho não publicados.
