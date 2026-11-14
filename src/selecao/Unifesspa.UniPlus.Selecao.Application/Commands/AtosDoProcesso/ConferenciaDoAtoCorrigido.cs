namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

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
internal static class ConferenciaDoAtoCorrigido
{
    /// <returns>
    /// Os atos da cadeia de correção que termina no ato corrigido — a linhagem a que o novo ato passa a
    /// pertencer; vazio quando o ato não corrige nenhum.
    /// </returns>
    public static async Task<Result<IReadOnlyList<Guid>>> ConferirAsync(
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        Guid processoSeletivoId,
        Guid? atoCorrigidoId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        if (atoCorrigidoId is not { } corrigidoId)
        {
            return Result<IReadOnlyList<Guid>>.Success([]);
        }

        Guid? processoDoCorrigido = await atoDoProcessoRepository
            .ObterProcessoDoAtoAsync(corrigidoId, cancellationToken)
            .ConfigureAwait(false);
        if (processoDoCorrigido is null)
        {
            return Falha(AtoDoProcessoErrorCodes.AtoCorrigidoNaoEncontrado,
                $"O ato {corrigidoId} não é um ato publicado por um processo seletivo.");
        }

        // A correção herda os vínculos do ato corrigido no registro central: aceitar o ato de outro
        // processo ligaria o novo ato a dois processos.
        if (processoDoCorrigido != processoSeletivoId)
        {
            return Falha(AtoDoProcessoErrorCodes.AtoCorrigidoDeOutroProcesso,
                $"O ato {corrigidoId} é de outro processo seletivo, e um ato só corrige ato do próprio processo.");
        }

        // Os atos que criam versão são os que congelam configuração: o tipo deles foi conferido como
        // congelante ao publicar. O ato avulso nunca congela, e a classe do corretor tem de coincidir
        // com a do corrigido (congelamento dos parâmetros na publicação, UNI-REQ-0019).
        IReadOnlyList<Guid> atosQueCongelam = await processoSeletivoRepository
            .ObterAtosCriadoresAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (atosQueCongelam.Contains(corrigidoId))
        {
            return Falha(AtoDoProcessoErrorCodes.AtoCorrigidoDeOutraClasse,
                $"O ato {corrigidoId} congela a configuração do processo e só se corrige por retificação do edital, com nova versão: o documento avulso não altera a configuração.");
        }

        Guid? corretor = await atoDoProcessoRepository
            .ObterAtoQueCorrigeAsync(corrigidoId, cancellationToken)
            .ConfigureAwait(false);
        if (corretor is not null)
        {
            return Falha(AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido, MensagemDoAtoJaCorrigido(corrigidoId, corretor));
        }

        return Result<IReadOnlyList<Guid>>.Success(
            await atoDoProcessoRepository.ListarCadeiaDeCorrecaoAsync(corrigidoId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// O item que o edital declara só se corrige pela publicação do item: é ela que exige a confirmação da
    /// política de divulgação, fecha o preliminar depois do definitivo e confere a cadeia do item. Corrigir
    /// o resultado por documento avulso contornaria as três.
    /// </summary>
    /// <param name="cadeiaDoAtoCorrigido">A cadeia que <see cref="ConferirAsync"/> devolveu; vazia quando não corrige.</param>
    public static async Task<Result> NaoEhItemDoEditalAsync(
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IReadOnlyCollection<Guid> cadeiaDoAtoCorrigido,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(cadeiaDoAtoCorrigido);

        if (cadeiaDoAtoCorrigido.Count == 0)
        {
            return Result.Success();
        }

        bool ehItemDoEdital = await atoDoProcessoRepository
            .ExisteItemDoEditalEntreAsync(cadeiaDoAtoCorrigido, cancellationToken)
            .ConfigureAwait(false);

        return ehItemDoEdital
            ? Result.Failure(new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoEhItemDoEdital,
                "O ato corrigido é a publicação de um item do edital, como um resultado, e só se corrige pela publicação do próprio item, que confirma a política de divulgação e confere a cadeia de correções."))
            : Result.Success();
    }

    public static string MensagemDoAtoJaCorrigido(Guid atoId, Guid? corretor) =>
        (corretor is null ? $"O ato {atoId} já foi corrigido" : $"O ato {atoId} já foi corrigido pelo ato {corretor}")
        + ". A cadeia de correção é linear: a correção seguinte aponta o ato mais recente da cadeia.";

    private static Result<IReadOnlyList<Guid>> Falha(string codigo, string mensagem) =>
        Result<IReadOnlyList<Guid>>.Failure(new DomainError(codigo, mensagem));
}
