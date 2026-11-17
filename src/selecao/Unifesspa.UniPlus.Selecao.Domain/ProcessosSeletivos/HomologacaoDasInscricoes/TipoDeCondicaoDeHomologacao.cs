namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// O que o sistema sabe apurar de uma inscrição para homologá-la. Só entra aqui o que já é apurável:
/// condição que o sistema ainda não apura é recusada, nunca guardada para depois (UNI-REQ-0265).
/// </summary>
public enum TipoDeCondicaoDeHomologacao
{
    /// <summary>A inscrição tem resultado no retorno do INEP da edição indicada.</summary>
    ResultadoNoRetornoDoInep = 1,
}

/// <summary>A ponte entre o tipo da condição e o código que o documento da configuração publica.</summary>
public static class TiposDeCondicaoDeHomologacao
{
    private const string CodigoResultadoNoRetornoDoInep = "RESULTADO_NO_RETORNO_DO_INEP";

    public static string Codigo(this TipoDeCondicaoDeHomologacao tipo) => tipo switch
    {
        TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep => CodigoResultadoNoRetornoDoInep,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de condição de homologação desconhecido."),
    };

    /// <summary>O tipo do código; recusa, como condição não disponível, o código ausente ou que o sistema ainda não apura.</summary>
    public static Result<TipoDeCondicaoDeHomologacao> DoCodigo(string? codigo) => codigo switch
    {
        CodigoResultadoNoRetornoDoInep => Result<TipoDeCondicaoDeHomologacao>.Success(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep),
        _ => Result<TipoDeCondicaoDeHomologacao>.Failure(new DomainError(
            HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel,
            string.IsNullOrWhiteSpace(codigo)
                ? "O tipo da condição precisa ser informado."
                : $"A condição '{codigo}' ainda não é apurada pelo sistema.")),
    };
}
