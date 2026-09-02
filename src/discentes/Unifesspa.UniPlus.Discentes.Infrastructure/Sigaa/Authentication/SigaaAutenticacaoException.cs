namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

using System;

/// <summary>
/// A API do SIGAA não autenticou o usuário de serviço da sincronização.
/// </summary>
/// <remarks>
/// Sinaliza credencial inválida, usuário desabilitado ou resposta de autenticação
/// malformada — situações que repetir não resolve. Por isso a exceção não é tratada como
/// falha transitória pela política de retentativa: insistir só multiplicaria a recusa.
/// </remarks>
public sealed class SigaaAutenticacaoException : Exception
{
    public SigaaAutenticacaoException()
    {
    }

    public SigaaAutenticacaoException(string message)
        : base(message)
    {
    }

    public SigaaAutenticacaoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
