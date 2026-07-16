namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Veredicto de conformidade legal de um <c>ProcessoSeletivo</c> contra o
/// catálogo <c>ObrigatoriedadeLegal</c> vigente na data de corte informada
/// (Story #853, CA-16) — recurso distinto de
/// <see cref="ConformidadeProcessoSeletivoDto"/> (checklist estrutural).
/// </summary>
public sealed record ConformidadeLegalProcessoSeletivoDto(
    Guid ProcessoSeletivoId,
    DateOnly DataReferencia,
    IReadOnlyList<RegraAvaliadaDto> Regras,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Projeção pública de <see cref="RegraAvaliada"/> — mesma fonte que o gate
/// de <c>Publicar</c>/<c>Retificar</c>/<c>FecharRetificacao</c> usa (CA-16:
/// nunca duas leituras em paralelo).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "Espelha ObrigatoriedadeLegal.AtoNormativoUrl — payload textual de auditoria.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Pareado com a justificativa de CA1056 acima.")]
public sealed record RegraAvaliadaDto(
    Guid RegraId,
    string RegraCodigo,
    CategoriaObrigatoriedade Categoria,
    string TipoProcessoCodigoAvaliado,
    PredicadoObrigatoriedade Predicado,
    bool Aprovada,
    string? Motivo,
    string BaseLegal,
    string? AtoNormativoUrl,
    string? PortariaInterna,
    string DescricaoHumana,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string Hash);
