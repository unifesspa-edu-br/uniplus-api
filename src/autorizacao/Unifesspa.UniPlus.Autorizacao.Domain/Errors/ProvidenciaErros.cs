namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros da providência que o domínio e os handlers produzem com a mesma mensagem.</summary>
public static class ProvidenciaErros
{
    /// <summary>Já há providência aberta sobre a concessão: a recusa do agregado e a do índice único da gravação concorrente.</summary>
    public static DomainError JaHaAberta() => new(
        ProvidenciaErrorCodes.JaHaAberta,
        "Já existe providência aberta para esta concessão; aguarde o desfecho dela antes de solicitar outra.");
}
