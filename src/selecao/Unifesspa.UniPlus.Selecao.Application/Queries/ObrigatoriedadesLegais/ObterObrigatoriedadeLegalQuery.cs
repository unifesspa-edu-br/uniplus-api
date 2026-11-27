namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Lê uma <c>ObrigatoriedadeLegal</c> ativa pelo Id. Recusa com o erro
/// de regra não encontrada quando ela não existe ou foi desativada.
/// </summary>
public sealed record ObterObrigatoriedadeLegalQuery(Guid Id) : IQuery<Result<ObrigatoriedadeLegalDto>>;
