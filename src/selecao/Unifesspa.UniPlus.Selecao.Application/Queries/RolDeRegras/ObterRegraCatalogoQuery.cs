namespace Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Obtém uma entrada do catálogo pela identidade <c>(codigo, versao)</c> — a mesma tripla que
/// um rascunho referencia, o que permite reler a definição exata que ele aponta depois de um
/// refresh, sem adivinhar qual versão era.
/// </summary>
public sealed record ObterRegraCatalogoQuery(
    string Codigo,
    string Versao) : IQuery<Result<RegraCatalogoDto>>;
