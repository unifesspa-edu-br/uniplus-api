namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Lê a situação da instalação, só para quem a iniciou.</summary>
public sealed record ObterInstalacaoQuery(Guid Id) : IQuery<Result<InstalacaoDto>>;
