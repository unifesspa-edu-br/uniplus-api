namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Remove (soft-delete) a Instituição — recadastramento institucional. Libera a
/// criação de uma nova Instituição, pois o registro removido não conta para o
/// limite singleton (CA-05).
/// </summary>
public sealed record RemoverInstituicaoCommand(Guid Id) : ICommand<Result>;
