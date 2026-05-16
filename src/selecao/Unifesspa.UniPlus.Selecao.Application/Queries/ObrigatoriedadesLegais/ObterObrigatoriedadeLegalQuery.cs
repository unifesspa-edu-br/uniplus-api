namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Lê uma <c>ObrigatoriedadeLegal</c> ativa pelo Id. Retorna
/// <see langword="null"/> quando não existe ou foi soft-deleted —
/// controller traduz para <c>404 Not Found</c>.
/// </summary>
public sealed record ObterObrigatoriedadeLegalQuery(Guid Id) : IQuery<ObrigatoriedadeLegalDto?>;
