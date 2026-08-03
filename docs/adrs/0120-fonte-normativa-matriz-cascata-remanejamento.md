---
status: "accepted"
date: "2026-08-03"
decision-makers:
  - "Tech Lead (CTIC)"
consulted:
  - "P.O. CEPS"
informed:
  - "Equipe Uni+"
---

# ADR-0120: Fonte normativa da matriz de remanejamento de cotas e seu alcance institucional

## Contexto e enunciado do problema

A Story #575 modela a cascata de remanejamento das oito modalidades federais da Lei 12.711/2012
(red. Lei 14.723/2023): quando uma cota reservada não é preenchida, a vaga é redirecionada por uma
sequência **ordenada e semântica** de modalidades de destino, até terminar em ampla concorrência.

A lei fixa a lógica de agrupamento (perfis reservados → escola pública → ampla concorrência, art. 3º,
§1º) mas não a ordem célula a célula entre as oito modalidades dentro de cada grupo. Sem essa ordem
fixada por norma, o `esquema_args` da regra `REMANEJ-CASCATA-LEI-12711` (rol_de_regras, Story #772)
não tem fonte a citar — e o campo `BaseLegal` do seed é, por convenção do projeto (§ Padrões
obrigatórios), preenchimento obrigatório antes de qualquer regra normativa entrar em produção.

A DoR da #575 exigia essa conferência antes de codar o seed. Esta ADR registra o resultado.

## Fonte normativa confirmada

**Portaria MEC nº 704, de 17 de outubro de 2025** (Diário Oficial da União, publicada em 20/10/2025,
Edição 200, Seção 1, p. 36-37). O **Art. 1º** desta portaria insere o **art. 20-A** na **Portaria
Normativa MEC nº 18, de 11 de outubro de 2012** (a mesma norma já citada como base legal de
`DISTRIB-VAGAS-LEI-12711` no seed do rol de regras) — não na Portaria Normativa MEC nº 21/2012 (Sisu),
que a mesma Portaria 704/2025 também altera, mas em dispositivos distintos (cadastro socioeconômico,
pesos/notas mínimas, oferta pós-lista de espera).

O art. 20-A determina, para vaga reservada da Lei 12.711/2012 sem candidato inscrito: primeiro aos
autodeclarados pretos/pardos/indígenas/quilombolas ou pessoas com deficiência, depois aos egressos de
escola pública, **"de acordo com a ordem disposta no Anexo a esta Portaria"**, por fim à ampla
concorrência.

O Anexo — "Ordem para destinação das vagas remanescentes na chamada regular e na lista de espera do
Sisu" — traz a matriz 8×7 completa, com os mesmos códigos já em uso no seed `DISTRIB-VAGAS-LEI-12711`
(`LB_PPI`, `LB_Q`, `LB_PCD`, `LB_EP`, `LI_PPI`, `LI_Q`, `LI_PCD`, `LI_EP`), em caixa alta, sem
divergência de grafia a conciliar:

| Vaga remanescente | 1º | 2º | 3º | 4º | 5º | 6º | 7º |
|---|---|---|---|---|---|---|---|
| `LB_PPI` | `LB_Q` | `LB_PCD` | `LB_EP` | `LI_PPI` | `LI_Q` | `LI_PCD` | `LI_EP` |
| `LB_Q` | `LB_PPI` | `LB_PCD` | `LB_EP` | `LI_PPI` | `LI_Q` | `LI_PCD` | `LI_EP` |
| `LB_PCD` | `LB_PPI` | `LB_Q` | `LB_EP` | `LI_PPI` | `LI_Q` | `LI_PCD` | `LI_EP` |
| `LB_EP` | `LB_PPI` | `LB_Q` | `LB_PCD` | `LI_PPI` | `LI_Q` | `LI_PCD` | `LI_EP` |
| `LI_PPI` | `LB_PPI` | `LB_Q` | `LB_PCD` | `LB_EP` | `LI_Q` | `LI_PCD` | `LI_EP` |
| `LI_Q` | `LB_PPI` | `LB_Q` | `LB_PCD` | `LB_EP` | `LI_PPI` | `LI_PCD` | `LI_EP` |
| `LI_PCD` | `LB_PPI` | `LB_Q` | `LB_PCD` | `LB_EP` | `LI_PPI` | `LI_Q` | `LI_EP` |
| `LI_EP` | `LB_PPI` | `LB_Q` | `LB_PCD` | `LB_EP` | `LI_PPI` | `LI_Q` | `LI_PCD` |

O fallback terminal é a ampla concorrência (`AC`) em toda linha — confirmado tanto pelo art. 20-A,
inciso I ("sendo, por fim, destinadas aos estudantes em ampla concorrência"), quanto pelo item 2 do
Anexo ("as vagas restantes serão disponibilizadas aos estudantes da ampla concorrência").

Conferida linha a linha, célula a célula, contra o `esquema_args` que a #575 semeia em
`REMANEJ-CASCATA-LEI-12711 v1` — a matriz é idêntica.

## Opções consideradas

O Anexo da Portaria 704/2025, no seu texto literal, regula o remanejamento na chamada regular e na
lista de espera **do Sisu**. A Unifesspa, ao ofertar vagas fora do Sisu sob o mesmo regime de reserva
da Lei 12.711/2012 (`DISTRIB-VAGAS-LEI-12711`), não está automaticamente obrigada a seguir esse Anexo
por processos seletivos próprios — a matriz precisa de uma decisão de alcance, não só de fonte.

- **A. Restringir a matriz ao Sisu** e deixar processos federais fora do Sisu sem `esquema_args`
  citável, até que surja norma institucional própria. **Rejeitada:** bloquearia `RN-CASCATA-5` para
  todo processo de regime federal fora do Sisu (ex.: Educação do Campo) sem necessidade — é a mesma
  lei (12.711/2012) e a mesma lógica de agrupamento (art. 3º, §1º) que a matriz do Anexo apenas
  explicita célula a célula; não há motivo jurídico para tratar o regime federal fora do Sisu como
  uma cascata diferente.
- **B. Adotar a matriz do Anexo por prática institucional própria, para toda oferta que usa
  `DISTRIB-VAGAS-LEI-12711`, dentro ou fora do Sisu — a escolha desta ADR.** Mesma razão pela qual a
  Portaria Normativa MEC 18/2012 já é adotada como base do cálculo de distribuição de vagas
  reservadas em processos fora do Sisu (`DISTRIB-VAGAS-LEI-12711`, Story #772): a norma nasce no
  contexto do Sisu, mas a Unifesspa já a usa como referência para o regime federal em geral, por não
  haver prática institucional divergente.

## Resultado da decisão

**Escolhida: opção B.** Não há registro de outra ordem já praticada pela Unifesspa em edital fora do
Sisu que divirja desta. Não havendo norma institucional própria conflitante, a matriz do Anexo é a
fonte para toda oferta que usa `DISTRIB-VAGAS-LEI-12711`, dentro ou fora do Sisu — decisão de negócio
explícita, não presumida. Se a Unifesspa vier a adotar ordem própria divergente para processos fora
do Sisu, é decisão de negócio nova, registrada como nova versão da regra — não uma correção desta ADR.

## Consequências

- `REMANEJ-CASCATA-LEI-12711 v1` (rol_de_regras) semeia esta matriz no `esquema_args`, com
  `BaseLegal` citando esta ADR e a Portaria MEC 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37).
- `RN-CASCATA-5` (Story #575) compara o payload aplicado — fallback + todos os destinos — célula a
  célula contra este `esquema_args`; divergência é recusada, mesmo com a forma da sequência válida.
- Uma eventual mudança na lei ou no Anexo da Portaria gera **nova versão** da regra (`v2`), nunca
  reescrita da `v1` já congelada em `VersaoConfiguracao` publicada (RN08, append-only).

## Referências

- Portaria MEC nº 704, de 17 de outubro de 2025 (DOU 20/10/2025, Seção 1, p. 36-37) — art. 1º, art.
  20-A e Anexo.
- Portaria Normativa MEC nº 18, de 11 de outubro de 2012 (redação dada pela Portaria MEC 704/2025).
- Lei 12.711/2012, art. 3º, §1º (red. Lei 14.723/2023).
- Story #575 (cascata de remanejamento) e Story #772 (rol_de_regras, `REMANEJ-CASCATA-LEI-12711`).
