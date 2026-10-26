namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using ValueObjects;

/// <summary>
/// As inscrições enviadas de um processo que escolheram uma edição do ENEM. Rascunho e inscrição
/// cancelada não contam.
/// </summary>
public interface IInscritosDaEdicaoDoEnem
{
    /// <summary>As inscrições da edição, identificadas pelo índice cego do CPF, sem decifrar nada.</summary>
    Task<IReadOnlyList<InscritoDaEdicao>> ListarAsync(
        Guid processoSeletivoId, string edicaoCodigo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os CPFs (onze dígitos) das inscrições da edição, decifrados. Só o envio ao INEP precisa deles.
    /// </summary>
    Task<IReadOnlyList<string>> ListarCpfsAsync(
        Guid processoSeletivoId, string edicaoCodigo, CancellationToken cancellationToken = default);
}
