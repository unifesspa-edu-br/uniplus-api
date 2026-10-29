namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Obtém um perfil pelo Id. Falha com <c>nao_encontrado</c> quando inexistente.</summary>
public sealed record ObterPerfilQuery(Guid Id) : IQuery<Result<PerfilDto>>;
