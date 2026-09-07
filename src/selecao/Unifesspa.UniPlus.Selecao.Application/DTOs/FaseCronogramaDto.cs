namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

/// <summary>Projeção de leitura de <c>ProdutoDaFase</c> — o que a fase publica e o papel de cada publicação.</summary>
/// <param name="Papel">Token canônico UPPER_SNAKE (<c>PRELIMINAR</c>/<c>DEFINITIVO</c>), ou nulo quando o ato não é resultado.</param>
public sealed record ProdutoDaFaseDto(Guid Id, string AtoCodigo, string? Papel);

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
    // Derivado dos produtos declarados — a fase produz resultado quando publica ao menos um
    // produto com papel. Projetado por conveniência de quem lê; não é campo de escrita.
    bool ProduzResultado,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    IReadOnlyList<ProdutoDaFaseDto> Produtos,
    string? FaseConcluinteCodigo,
    bool EmiteParecerIndividual,
    IReadOnlyList<BancaRequeridaDto> BancasRequeridas,
    RegraRecursoFaseDto? RegraRecurso);
