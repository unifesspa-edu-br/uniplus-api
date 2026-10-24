namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

/// <summary>
/// O resultado não traz um veredicto registrável ("não definido", ou sem o que ele exige). Defeito do contrato
/// que nenhuma reentrega corrige: o arquivo segue em verificação e a mensagem vai para a fila de erro.
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
