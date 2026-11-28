namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere o ato que um novo ato corrige (ADR-0103; UNI-REQ-0156): é do mesmo processo, é da mesma
/// classe de congelamento e ainda não foi corrigido por ninguém.
/// </summary>
/// <remarks>
/// A conferência dá a recusa nomeada no caso comum, antes de qualquer escrita. A correção de um ato que o
/// endpoint administrativo de Publicações já corrigiu é recusada pelo registro do ato, na mesma transação, e a que
/// só a gravação enxerga, pela unicidade.
/// </remarks>
public sealed class ConferenciaDoAtoCorrigido(
    IAtoDoProcessoRepository atoDoProcessoRepository,
    IProcessoSeletivoRepository processoSeletivoRepository)
{
    /// <returns>
    /// Os atos da cadeia de correção que termina no ato corrigido — a linhagem a que o novo ato passa a
    /// pertencer; vazio quando o ato não corrige nenhum.
    /// </returns>
    internal async Task<Result<IReadOnlyList<Guid>>> ConferirAsync(
        Guid processoSeletivoId,
        Guid? atoCorrigidoId,
        CancellationToken cancellationToken)
    {
        if (atoCorrigidoId is not { } corrigidoId)
        {
            return Result<IReadOnlyList<Guid>>.Success([]);
        }

        return await ConferirPertencimentoAsync(processoSeletivoId, corrigidoId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A conferência do ato que um documento avulso corrige: o ato é conferido e, só então, a cadeia dele é lida para
    /// saber se é a publicação de um item do edital. Ato recusado não tem cadeia a ler.
    /// </summary>
    internal async Task<IReadOnlyList<FieldError>> ConferirParaDocumentoAvulsoAsync(
        Guid processoSeletivoId, Guid atoCorrigidoId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Guid>> cadeia = await ConferirAsync(processoSeletivoId, atoCorrigidoId, cancellationToken).ConfigureAwait(false);
        if (cadeia.IsFailure)
        {
            return cadeia.Errors;
        }

        Result item = await NaoEhItemDoEditalAsync(cadeia.Value!, cancellationToken).ConfigureAwait(false);

        return item.Errors;
    }

    private async Task<Result<IReadOnlyList<Guid>>> ConferirPertencimentoAsync(
        Guid processoSeletivoId, Guid corrigidoId, CancellationToken cancellationToken)
    {
        Guid? processoDoCorrigido = await atoDoProcessoRepository.ObterProcessoDoAtoAsync(corrigidoId, cancellationToken).ConfigureAwait(false);
        if (processoDoCorrigido is null)
        {
            return Falha(AtoDoProcessoErrorCodes.AtoCorrigidoNaoEncontrado,
                "O ato não é um ato publicado por um processo seletivo.");
        }
        return processoDoCorrigido != processoSeletivoId
            ? Falha(AtoDoProcessoErrorCodes.AtoCorrigidoDeOutroProcesso,
                "O ato é de outro processo seletivo, e um ato só corrige ato do próprio processo.")
            : await ConferirClasseAsync(processoSeletivoId, corrigidoId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<IReadOnlyList<Guid>>> ConferirClasseAsync(
        Guid processoSeletivoId, Guid corrigidoId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> atosQueCongelam = await processoSeletivoRepository
            .ObterAtosCriadoresAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        return atosQueCongelam.Contains(corrigidoId)
            ? Falha(AtoDoProcessoErrorCodes.AtoCorrigidoDeOutraClasse,
                "O ato congela a configuração do processo e só se corrige por retificação do edital, com nova versão: o documento avulso não altera a configuração.")
            : await ConferirCabecaAsync(corrigidoId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<IReadOnlyList<Guid>>> ConferirCabecaAsync(Guid corrigidoId, CancellationToken cancellationToken)
    {
        Guid? corretor = await atoDoProcessoRepository.ObterAtoQueCorrigeAsync(corrigidoId, cancellationToken).ConfigureAwait(false);
        return corretor is not null
            ? Falha(AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido, MensagemDoAtoJaCorrigido())
            : Result<IReadOnlyList<Guid>>.Success(
                await atoDoProcessoRepository.ListarCadeiaDeCorrecaoAsync(corrigidoId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// O item que o edital declara só se corrige pela publicação do item: é ela que exige a confirmação da
    /// política de divulgação, fecha o preliminar depois do definitivo e confere a cadeia do item. Corrigir
    /// o resultado por documento avulso contornaria as três.
    /// </summary>
    /// <param name="cadeiaDoAtoCorrigido">A cadeia que <see cref="ConferirAsync"/> devolveu; vazia quando não corrige.</param>
    internal async Task<Result> NaoEhItemDoEditalAsync(
        IReadOnlyCollection<Guid> cadeiaDoAtoCorrigido,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cadeiaDoAtoCorrigido);
        if (cadeiaDoAtoCorrigido.Count == 0)
        {
            return Result.Success();
        }

        bool ehItemDoEdital = await atoDoProcessoRepository
            .ExisteItemDoEditalEntreAsync(cadeiaDoAtoCorrigido, cancellationToken)
            .ConfigureAwait(false);
        return ehItemDoEdital
            ? Result.ValidationFailure([new FieldError(AtoDoProcesso.CampoDoAtoCorrigido, new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoEhItemDoEdital,
                "O ato corrigido é a publicação de um item do edital, como um resultado, e só se corrige pela publicação do próprio item, que confirma a política de divulgação e confere a cadeia de correções."))])
            : Result.Success();
    }

    public static string MensagemDoAtoJaCorrigido() =>
        "O ato já foi corrigido. A cadeia de correção é linear: a correção seguinte aponta o ato mais recente da cadeia.";

    private static Result<IReadOnlyList<Guid>> Falha(string codigo, string mensagem) =>
        Result<IReadOnlyList<Guid>>.ValidationFailure([new FieldError(AtoDoProcesso.CampoDoAtoCorrigido, new DomainError(codigo, mensagem))]);
}
