namespace Unifesspa.UniPlus.Verificador.Antivirus;

/// <summary>O que o clamd concluiu sobre o conteúdo. A assinatura só vai para o log do verificador.</summary>
public readonly record struct ResultadoDoClamd(bool Infectado, string? Assinatura);

public interface IClamd
{
    /// <exception cref="ClamdIndisponivelException">O clamd não respondeu; vale tentar de novo.</exception>
    /// <exception cref="ClamdErroPermanenteException">O clamd respondeu algo que não é OK nem FOUND; tentar de novo não adianta.</exception>
    Task<ResultadoDoClamd> VerificarAsync(Stream conteudo, CancellationToken cancellationToken);

    /// <summary>A data da base de assinaturas em uso (comando <c>VERSION</c>).</summary>
    Task<DateTimeOffset> LerDataDaBaseAsync(CancellationToken cancellationToken);
}

/// <summary>Base das falhas do clamd.</summary>
public abstract class ClamdException : Exception
{
    protected ClamdException()
    {
    }

    protected ClamdException(string message)
        : base(message)
    {
    }

    protected ClamdException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>O clamd fora do ar, sem resposta ou com a conexão interrompida.</summary>
public sealed class ClamdIndisponivelException : ClamdException
{
    public ClamdIndisponivelException()
    {
    }

    public ClamdIndisponivelException(string message)
        : base(message)
    {
    }

    public ClamdIndisponivelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// O clamd respondeu, mas recusou o conteúdo (por exemplo, <c>INSTREAM size limit exceeded</c>). O
/// mesmo conteúdo dará a mesma resposta: não é indisponibilidade e não se reprocessa.
/// </summary>
public sealed class ClamdErroPermanenteException : ClamdException
{
    public ClamdErroPermanenteException()
    {
    }

    public ClamdErroPermanenteException(string message)
        : base(message)
    {
    }

    public ClamdErroPermanenteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
