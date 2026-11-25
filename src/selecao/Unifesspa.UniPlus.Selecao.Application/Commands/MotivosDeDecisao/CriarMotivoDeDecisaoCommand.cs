namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Payload do <c>POST /api/selecao/admin/motivos-de-decisao</c> (UNI-REQ-0283). Tipo e sentido
/// trafegam como código textual canônico UPPER_SNAKE, e não como valor de enum, para que o wire
/// não dependa da ordem de declaração no C#. A subclassificação só é aceita no tipo que a define
/// (o fundamento, na isenção da taxa).
/// </summary>
public sealed record CriarMotivoDeDecisaoCommand(
    [property: AceitaCodigosDe<TipoMotivoDeDecisao>] string? Tipo,
    string? Codigo,
    string? Descricao,
    [property: AceitaCodigosDe<FundamentoIsencao>] string? Subclassificacao,
    [property: AceitaCodigosDe<SentidoDoMotivo>] string? Sentido) : ICommand<Result<Guid>>;
