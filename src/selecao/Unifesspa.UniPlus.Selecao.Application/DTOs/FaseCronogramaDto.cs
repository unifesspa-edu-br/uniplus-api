namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

/// <summary>Projeção de leitura de <c>BancaRequerida</c> (Story #851).</summary>
public sealed record BancaRequeridaDto(Guid Id, Guid TipoBancaOrigemId, string Codigo);

/// <summary>Projeção de leitura de <c>ArgsRegraPrazoRecurso</c> (Story #851).</summary>
public sealed record ArgsRegraPrazoRecursoDto(
    decimal PrazoValor,
    UnidadePrazo PrazoUnidade,
    string AtoAncoraCodigo,
    decimal? SuspensividadePrimeiraInstanciaValor,
    UnidadePrazo? SuspensividadePrimeiraInstanciaUnidade,
    decimal? SuspensividadeSegundaInstanciaValor,
    UnidadePrazo? SuspensividadeSegundaInstanciaUnidade);

/// <summary>Projeção de leitura de <c>RegraRecursoFase</c> (0..1, Story #851) — presença = a fase admite recurso.</summary>
public sealed record RegraRecursoFaseDto(Guid Id, ReferenciaRegraDto Regra, ArgsRegraPrazoRecursoDto Args);

/// <summary>Projeção de leitura de <c>FaseCronograma</c> (Story #851) — o eixo temporal do certame.</summary>
public sealed record FaseCronogramaDto(
    Guid Id,
    int Ordem,
    Guid FaseCanonicaOrigemId,
    string Codigo,
    string DonoInstitucional,
    // Token canônico UPPER_SNAKE (`PROPRIA`/`DELEGADA`), o mesmo que o catálogo de fases
    // canônicas publica — a origem deste campo é aquele cadastro, não a escrita daqui.
    string OrigemData,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    bool ProduzResultado,
    bool ResultadoDefinitivo,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    string? AtoProduzidoCodigo,
    bool AtoProduzidoEfeitoIrreversivel,
    IReadOnlyList<BancaRequeridaDto> BancasRequeridas,
    RegraRecursoFaseDto? RegraRecurso);
