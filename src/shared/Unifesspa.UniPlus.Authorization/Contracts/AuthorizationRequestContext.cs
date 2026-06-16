namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Contexto da requisição que o ponto de decisão único recebe (ADR-0078):
/// correlação, origem da chamada, instante de acesso e — <b>somente quando a
/// operação exige</b> — a concessão de dupla aprovação. O serviço de decisão
/// <b>recebe</b> este contrato; ele não o produz.
/// </summary>
public sealed record AuthorizationRequestContext
{
    /// <summary>Identificador de correlação da requisição — string opaca.</summary>
    public string RequestId { get; }

    /// <summary>IP de origem da chamada. Vazio quando não se aplica (ex.: jobs).</summary>
    public string IpOrigem { get; }

    /// <summary>Agente do cliente. Vazio quando não se aplica.</summary>
    public string UserAgent { get; }

    /// <summary>Instante do acesso, sempre em UTC.</summary>
    public DateTimeOffset DataAcesso { get; }

    /// <summary>Identificador da atuação institucional, em sessão delegada. Opcional.</summary>
    public Guid? OnBehalfOfId { get; }

    /// <summary>Canal pelo qual a requisição chegou.</summary>
    public OrigemRequisicao Origem { get; }

    /// <summary>Concessão de dupla aprovação — presente só quando a operação exige.</summary>
    public DualApprovalGrant? DuplaAprovacao { get; }

    private AuthorizationRequestContext(
        string requestId,
        string ipOrigem,
        string userAgent,
        DateTimeOffset dataAcesso,
        Guid? onBehalfOfId,
        OrigemRequisicao origem,
        DualApprovalGrant? duplaAprovacao)
    {
        RequestId = requestId;
        IpOrigem = ipOrigem;
        UserAgent = userAgent;
        DataAcesso = dataAcesso;
        OnBehalfOfId = onBehalfOfId;
        Origem = origem;
        DuplaAprovacao = duplaAprovacao;
    }

    /// <summary>
    /// Constrói um <see cref="AuthorizationRequestContext"/> validado. Rejeita
    /// <paramref name="requestId"/> em branco e <paramref name="onBehalfOfId"/>
    /// informado como <see cref="Guid.Empty"/>. O <paramref name="requestId"/> é
    /// preservado <b>verbatim</b> (identificador opaco de correlação, não se
    /// normaliza). <paramref name="dataAcesso"/> é normalizada para UTC com
    /// <see cref="DateTimeOffset.ToUniversalTime"/>, preservando o instante e
    /// garantindo o invariante de fuso. <paramref name="ipOrigem"/> e
    /// <paramref name="userAgent"/> nulos viram string vazia — são telemetria,
    /// não exigidos. A dupla aprovação é opcional (ausente quando a operação não
    /// a exige).
    /// </summary>
    public static Result<AuthorizationRequestContext> From(
        string? requestId,
        DateTimeOffset dataAcesso,
        OrigemRequisicao origem,
        string? ipOrigem = null,
        string? userAgent = null,
        Guid? onBehalfOfId = null,
        DualApprovalGrant? duplaAprovacao = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return Result<AuthorizationRequestContext>.Failure(new DomainError(
                AuthorizationErrorCodes.AuthorizationRequestContextRequestIdObrigatorio,
                "Identificador de correlação da requisição é obrigatório."));
        }

        if (onBehalfOfId is { } id && id == Guid.Empty)
        {
            return Result<AuthorizationRequestContext>.Failure(new DomainError(
                AuthorizationErrorCodes.AuthorizationRequestContextOnBehalfOfInvalido,
                "OnBehalfOfId informado não pode ser Guid.Empty — use um identificador real ou nulo."));
        }

        return Result<AuthorizationRequestContext>.Success(new AuthorizationRequestContext(
            requestId,
            ipOrigem ?? string.Empty,
            userAgent ?? string.Empty,
            dataAcesso.ToUniversalTime(),
            onBehalfOfId,
            origem,
            duplaAprovacao));
    }
}
