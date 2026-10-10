namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Envia ou corrige a inscrição (UNI-REQ-0084): diferente de <see cref="SalvarInscricaoCommand"/>,
/// exige obrigatórios, restrições, impedimentos, grupos e termos — a validação completa do veredito
/// de submissão (issue #1859).
/// </summary>
public sealed record SubmeterInscricaoCommand(
    Guid ProcessoSeletivoId, InscricaoRequest Request, PrecondicaoIfMatch Precondicao) : ICommand<Result<ComprovanteInscricaoDto>>;
