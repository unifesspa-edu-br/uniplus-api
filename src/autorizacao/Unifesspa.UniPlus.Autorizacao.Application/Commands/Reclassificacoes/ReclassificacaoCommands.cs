namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Decide uma etapa da liberação da permissão reclassificada para as concessões da lista ou para todas as que a aguardam.</summary>
public sealed record DecidirReclassificacaoCommand(Guid ReclassificacaoId, DecidirReclassificacaoRequest Dados)
    : ICommand<Result<DecisaoDaReclassificacaoDto>>;
