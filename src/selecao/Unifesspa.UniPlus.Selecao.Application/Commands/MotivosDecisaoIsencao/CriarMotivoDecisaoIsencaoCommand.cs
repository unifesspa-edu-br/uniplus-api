namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Payload do <c>POST /api/selecao/admin/motivos-decisao-isencao</c>
/// (UNI-REQ-0120). Fundamento e resultado permitido trafegam como código
/// textual canônico UPPER_SNAKE, e não como valor de enum, para que o wire não
/// dependa da ordem de declaração no C#.
/// </summary>
public sealed record CriarMotivoDecisaoIsencaoCommand(
    string? Codigo,
    string? Descricao,
    string? Fundamento,
    string? ResultadoPermitido) : ICommand<Result<Guid>>;
