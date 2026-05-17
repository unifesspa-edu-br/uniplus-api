namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Command que atualiza uma <c>ObrigatoriedadeLegal</c> existente. Semântica
/// full-replace (ADR-0058 Emenda 1): todos os campos são aplicados
/// literalmente — caller deve repassar valores correntes dos opcionais que
/// quer preservar. URI estável (mesmo <see cref="Id"/>); auditoria via
/// <c>obrigatoriedade_legal_historico</c>.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "AtoNormativoUrl é payload textual de citação normativa.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Construtor do record propaga o tipo string do payload — ver justificativa acima.")]
public sealed record AtualizarObrigatoriedadeLegalCommand(
    Guid Id,
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
    HashSet<string> AreasDeInteresse) : ICommand<Result>;
