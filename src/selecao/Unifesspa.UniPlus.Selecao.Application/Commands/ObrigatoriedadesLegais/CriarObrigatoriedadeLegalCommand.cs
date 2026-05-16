namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Command que cria uma <c>ObrigatoriedadeLegal</c> a partir do payload do
/// <c>POST /api/selecao/admin/obrigatoriedades-legais</c>. Reflete o input
/// integral da forma plena (ADR-0058 + Emenda 1) — full-replace por design.
/// </summary>
/// <remarks>
/// <see cref="AreasDeInteresse"/> usa <c>IReadOnlySet&lt;string&gt;</c> de
/// códigos uppercase ASCII; o handler converte para
/// <c>HashSet&lt;AreaCodigo&gt;</c> via <c>AreaCodigo.From</c>, validando
/// shape no caminho. Ambos vazios (sem proprietário, sem áreas) modelam
/// regra universal/global.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "AtoNormativoUrl é payload textual de citação normativa "
        + "(DOI, URN, IRI) — preserva fidelidade do valor original.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Construtor do record propaga o tipo string do payload — ver justificativa acima.")]
public sealed record CriarObrigatoriedadeLegalCommand(
    string TipoEditalCodigo,
    CategoriaObrigatoriedade Categoria,
    string RegraCodigo,
    PredicadoObrigatoriedade Predicado,
    string DescricaoHumana,
    string BaseLegal,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string? AtoNormativoUrl,
    string? PortariaInternaCodigo,
    string? Proprietario,
    IReadOnlySet<string> AreasDeInteresse) : ICommand<Result<Guid>>;
