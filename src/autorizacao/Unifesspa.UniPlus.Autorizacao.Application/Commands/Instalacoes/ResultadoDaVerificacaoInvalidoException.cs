namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

/// <summary>
/// O resultado do verificador não tem a forma que o contrato promete (veredicto indefinido, ou "verificado" sem
/// o SHA-256). É defeito do contrato, e nenhuma reentrega o corrige.
/// </summary>
public sealed class ResultadoDaVerificacaoInvalidoException : Exception
{
    public ResultadoDaVerificacaoInvalidoException()
    {
    }

    public ResultadoDaVerificacaoInvalidoException(string message)
        : base(message)
    {
    }

    public ResultadoDaVerificacaoInvalidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
