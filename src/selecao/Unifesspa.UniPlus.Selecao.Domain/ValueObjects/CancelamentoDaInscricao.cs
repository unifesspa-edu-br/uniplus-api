namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed record CancelamentoDaInscricao
{
    private CancelamentoDaInscricao(DateTimeOffset? canceladaEm, string? canceladaPor, string? motivoDoCancelamento)
    {
        CanceladaEm = canceladaEm;
        CanceladaPor = canceladaPor;
        MotivoDoCancelamento = motivoDoCancelamento;
    }

    public DateTimeOffset? CanceladaEm { get; }
    public string? CanceladaPor { get; }
    public string? MotivoDoCancelamento { get; }

    public static CancelamentoDaInscricao? Reidratar(DateTimeOffset? canceladaEm, string? canceladaPor, string? motivoDoCancelamento) =>
        canceladaEm is null && canceladaPor is null && motivoDoCancelamento is null
            ? null
            : new(canceladaEm, canceladaPor, motivoDoCancelamento);

    public const string MotivoPadraoDoCancelamento = "a pedido do candidato";
    public const int TamanhoMaximoDoMotivoDoCancelamento = 500;

    public static Result<CancelamentoDaInscricao> Registrar(string? motivo, string ator, DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ator);
        string motivoDito = string.IsNullOrWhiteSpace(motivo) ? MotivoPadraoDoCancelamento : motivo.Trim();
        return motivoDito.Length > TamanhoMaximoDoMotivoDoCancelamento
            ? Result<CancelamentoDaInscricao>.Failure(new DomainError(InscricaoErrorCodes.MotivoDoCancelamentoLongo,
                $"O motivo do cancelamento aceita até {TamanhoMaximoDoMotivoDoCancelamento} caracteres."))
            : Result<CancelamentoDaInscricao>.Success(new(instante, ator, motivoDito));
    }
}
