---
status: "accepted"
date: "2026-07-15"
decision-makers:
  - "Tech Lead"
  - "Product Owner"
consulted:
  - "CEPS"
informed:
  - "Equipe Seleção"
---

# ADR-0114: Ruleset de conformidade legal do processo seletivo

## Contexto e enunciado do problema

O catálogo `ObrigatoriedadeLegal` é vigente desde a ADR-0058: tem CRUD
administrativo, hash content-addressable e histórico forense. Falta, porém, o
contrato que escolhe quais regras daquele catálogo pertencem a um processo
seletivo. O checklist atual de `ProcessoSeletivo.AvaliarConformidade()` cobre
somente itens estruturais; não é nem deve fingir que avalia o catálogo legal.

A seleção não pode depender do relógio do ato de publicação. A mesma
configuração de um certame pode ser publicada e depois retificada em momentos
diferentes, mas a base legal aplicável continua sendo a que vigorava quando as
inscrições do candidato começaram. Também não existe data documental estruturada
no agregado: ela pertence ao PDF e não é interpretada pelo sistema.

Esta decisão fixa o contrato preparatório da avaliação. Ela não cria avaliador,
não altera o gate de publicação e não inclui resultados no envelope canônico;
essas responsabilidades pertencem à #853.

## Drivers da decisão

- Reproduzir a seleção das regras em qualquer momento, sem depender de relógio.
- Não aceitar cadastro para um tipo de processo que não existe.
- Preservar o catálogo como dado configurável: não codificar suposições sobre
  cotas, bônus ou modalidades por tipo de processo.
- Manter o canonicalizador uma projeção pura de entrada única, conforme ADR-0109
  D6.

## Opções consideradas

- Escolher as regras vigentes no relógio do sistema ao publicar.
- Usar a data documental declarada no PDF do edital.
- Usar `DadosEdital.PeriodoInscricaoInicio` como data de referência explícita,
  com união de regras universais e regras do tipo do processo.

## Resultado da decisão

**Escolhida:** usar exclusivamente `DadosEdital.PeriodoInscricaoInicio`
(`DateOnly`) como data jurídica de referência do ruleset. O chamador sempre
fornece essa data; `ObterVigentesParaTipoProcessoAsync` e
`ObterObrigatoriedadesAplicaveisQueryHandler` não leem `TimeProvider`,
`DateTimeOffset.UtcNow` nem `DateTimeOffset.Now`.

O ruleset aplicável é a união das regras com `TipoProcessoCodigo = "*"` e das
regras cujo código é exatamente o nome declarado de `ProcessoSeletivo.Tipo`.
As duas partes obedecem a `VigenciaInicio <= dataReferencia < VigenciaFim`,
sendo `VigenciaFim` nula uma vigência aberta. Regras de outro tipo não entram;
não há fallback, comparação parcial ou comparação case-insensitive.

`TipoProcessoCodigo` substitui diretamente `TipoEditalCodigo` em domínio,
aplicação, API, persistência, payload de hash e histórico forense. O vocabulário
é fechado: aceita `"*"` ou exatamente um nome de `TipoProcesso`, exceto o
sentinela interno `Nenhum`: `SiSU`, `PSIQ`, `PSECampo`, `PSVR`,
`TransferenciaInterna`, `TransferenciaExterna`, `PortadorDiploma` e `Reopcao`.
Isso é uma chave administrativa, diferente do wire format camelCase de enums
HTTP. Não há camada de compatibilidade para o nome ou query string antigo.

O resultado da futura avaliação continua sendo `ResultadoConformidade` e
`RegraAvaliada`. Quando a #853 o congelar, o handler acrescentará um campo a
`EntradaCanonicalizacao`; a assinatura de
`ISnapshotPublicacaoCanonicalizer.Canonicalizar(EntradaCanonicalizacao)`
permanece com um único parâmetro. O canonicalizador não recebe repositório,
relógio ou outro estado externo.

### Reconciliação dos predicados

| Predicado | Alvo e decisão |
| --- | --- |
| `EtapaObrigatoria(TipoEtapaCodigo)` | Mantido; casa com `Etapas[].Nome`, ordinal e case-insensitive. |
| `ModalidadesMinimas(Codigos)` | Mantido; avalia por oferta: toda `ConfiguracaoDistribuicaoVagas` precisa conter todas as modalidades. |
| `DesempateDeveIncluir(Criterio)` | Mantido; casa com `CriteriosDesempate[].Regra.Codigo`. |
| `DocumentoObrigatorioParaModalidade(...)` | Sem alvo hoje; bloqueado por #554, que introduz `DocumentoExigido`. |
| `BonusObrigatorio(ModalidadesAplicaveis)` | Incompatível: `ConfiguracaoBonusRegional` é global e não tem modalidades. **Descartar a variante por amendment da ADR-0058**; degenerá-la para presença/ausência perderia a semântica de `ModalidadesAplicaveis`. A #853 executa esse descarte no avaliador. |
| `AtendimentoDisponivel(Necessidades)` | Mantido; casa com `OfertaAtendimento.TiposDeficiencia[].TipoDeficienciaNome`; a implementação fixa a comparação por nome. |
| `ConcorrenciaDuplaObrigatoria()` | Mantido; não é tautológico. Sem modalidade `CotaReservada`, reprova corretamente; a regra só é cadastrada para tipos em que a dupla concorrência é legalmente exigida. |
| `Customizado(Parametros)` | Mantido como válvula de escape sem alvo fixo. |

## Consequências

### Positivas

- A seleção do ruleset é reproduzível: o mesmo processo e a mesma data elegem
  as mesmas regras, mesmo após mudanças no relógio ou no catálogo futuro.
- O catálogo passa a denunciar cedo uma regra para tipo inexistente com 422,
  em vez de deixá-la silenciosamente sem consumidor.
- A #853 recebe uma query interna pronta, sem controller novo nem dependência
  escondida do canonicalizador.

### Negativas

- O rename altera a chave literal que participa do hash; uma regra com a mesma
  semântica passa a ter outro hash. Não há regra de produção nem seed a migrar,
  portanto a recriação por API é a transição deliberada.
- A #853 precisa executar o descarte da variante de bônus no avaliador e no
  amendment da ADR-0058; nenhum atalho pode ignorar
  `ModalidadesAplicaveis` silenciosamente.

### Neutras

- O filtro `vigentes=true` da listagem administrativa continua respondendo
  "ativo agora" com o relógio da consulta. Ele não é caminho do gate legal.
- `GET /processos-seletivos/{id}/conformidade` segue expondo apenas o checklist
  estrutural até a #853.

## Confirmação

- Testes de domínio aceitam o sentinela e os oito nomes permitidos, e recusam
  `SISU_ANTIGO`, grafia divergente e `Nenhum` com o erro específico.
- Teste de integração confirma união universal + específica, exclusão de outro
  tipo e variação do conjunto em duas datas.
- Testes do handler confirmam a delegação da data `DateOnly` e que datas
  diferentes podem devolver conjuntos diferentes; o fitness test proíbe leitura
  de relógio nesse handler. O fitness test da ADR-0109 preserva a porta do
  canonicalizador com uma entrada única.

## Prós e contras das opções

### Relógio da publicação

- Bom, porque parece simples para um único ato de publicação.
- Ruim, porque duas publicações do mesmo certame poderiam escolher cortes legais
  diferentes sem que o fato jurídico tenha mudado.

### Data documental do PDF

- Bom, porque poderia refletir um ato formal quando disponível.
- Ruim, porque não existe como campo estruturado e exigiria interpretar o PDF;
  a ADR-0104 já separa vigência de configuração e data documental.

### Início das inscrições explícito

- Bom, porque é data estável, administrada no processo e representa quando o
  candidato passou a se submeter às regras.
- Ruim, porque quem monta a avaliação deve transportar a data explicitamente,
  em vez de contar com um default de infraestrutura.

## Mais informações

- ADR-0058 — catálogo de obrigatoriedades legais e predicados tipados.
- ADR-0068 — relógio explícito como dependência, sem fallback oculto.
- ADR-0104 — vigência ordena versões; não interpreta data documental.
- ADR-0109 D6 — envelope canônico extensível por record de entrada única.
- Issue #852 — contrato, rename e seleção do ruleset.
- Issue #853 — avaliador, gate e congelamento do resultado.
