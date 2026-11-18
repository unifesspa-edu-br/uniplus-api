namespace Unifesspa.UniPlus.Kernel.Results;

/// <summary>Compõe resultados na ordem das partes, interrompendo na primeira recusa.</summary>
public static class EncadeamentoDeResultados
{
    /// <summary>Continua com a próxima parte quando esta foi aceita.</summary>
    public static Result<TSaida> Encadear<T, TSaida>(this Result<T> resultado, Func<T, Result<TSaida>> proxima)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return resultado.Match(proxima, Result<TSaida>.Failure);
    }

    /// <summary>Compõe o valor final quando esta foi a última parte aceita.</summary>
    public static Result<TSaida> Compor<T, TSaida>(this Result<T> resultado, Func<T, TSaida> compor)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(compor);
        return resultado.Match(valor => Result<TSaida>.Success(compor(valor)), Result<TSaida>.Failure);
    }
}
