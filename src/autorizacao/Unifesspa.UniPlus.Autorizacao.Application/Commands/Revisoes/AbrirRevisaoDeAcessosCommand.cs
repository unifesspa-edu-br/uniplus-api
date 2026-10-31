namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Abre a revisão dos acessos: um item por concessão ativa de perfil pessoal ou sensível.</summary>
public sealed record AbrirRevisaoDeAcessosCommand : ICommand<Result<RevisaoDeAcessosDto>>;
