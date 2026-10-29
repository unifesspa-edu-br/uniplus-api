namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Resume o código de confirmação da instalação, preso à instalação a que pertence. O resumo
/// é o que se grava e se compara; o código em claro só existe no e-mail enviado.
/// </summary>
public interface IResumoDeCodigoDeConfirmacao
{
    Task<byte[]> ResumirAsync(Guid instalacaoId, string codigo, CancellationToken cancellationToken = default);
}
