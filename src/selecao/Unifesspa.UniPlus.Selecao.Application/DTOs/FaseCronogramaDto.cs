namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Projeção de leitura de <c>BancaRequerida</c> (Story #851).</summary>
public sealed record BancaRequeridaDto(Guid Id, Guid TipoBancaOrigemId, string Codigo);

/// <summary>Projeção de leitura de <c>ArgsRegraPrazoRecurso</c> (Story #851).</summary>
public sealed record ArgsRegraPrazoRecursoDto(
    decimal PrazoValor,
    string PrazoUnidade,
    string AtoAncoraCodigo,
    decimal? SuspensividadePrimeiraInstanciaValor,
    string? SuspensividadePrimeiraInstanciaUnidade,
    decimal? SuspensividadeSegundaInstanciaValor,
    string? SuspensividadeSegundaInstanciaUnidade);

/// <summary>Projeção de leitura de <c>RegraRecursoFase</c> (0..1, Story #851) — presença = a fase admite recurso.</summary>
public sealed record RegraRecursoFaseDto(Guid Id, ReferenciaRegraDto Regra, ArgsRegraPrazoRecursoDto Args);

/// <summary>Projeção de leitura de <c>FaseCronograma</c> (Story #851) — o eixo temporal do certame.</summary>
public sealed record FaseCronogramaDto(
    Guid Id,
    int Ordem,
    Guid FaseCanonicaOrigemId,
    string Codigo,
    string DonoInstitucional,
    string OrigemData,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    bool ProduzResultado,
    bool ResultadoDefinitivo,
    bool ColetaInscricao,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    string? AtoProduzidoCodigo,
    bool AtoProduzidoEfeitoIrreversivel,
    IReadOnlyList<BancaRequeridaDto> BancasRequeridas,
    RegraRecursoFaseDto? RegraRecurso);
