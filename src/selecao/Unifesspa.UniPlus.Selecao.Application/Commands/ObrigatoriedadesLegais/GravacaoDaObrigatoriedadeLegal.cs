namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Grava a obrigatoriedade legal e traduz a corrida entre a consulta prévia de unicidade e a gravação. Só o banco
/// enxerga a corrida: as constraints únicas parciais do código e do hash da regra ativa disparam, e a recusa sai
/// nomeada. Os endpoints têm chave de idempotência (ADR-0119), então o rastreamento é descartado antes de devolver
/// a recusa: sem isso o Wolverine repete o <c>SaveChanges</c> ao fim do handler e a mesma violação estoura fora do
/// <c>catch</c>, como 500 em vez de 409.
/// </summary>
internal static class GravacaoDaObrigatoriedadeLegal
{
    /// <summary>
    /// Recusa o código de regra que outra regra ativa já usa. É consulta de adiantamento: a corrida entre ela e a
    /// gravação é tratada em <see cref="SalvarAsync"/>.
    /// </summary>
    internal static async Task<Result> ConferirUnicidadeAsync(
        IObrigatoriedadeLegalRepository repository,
        string? regraCodigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        bool duplicada = await repository
            .ExisteRegraCodigoAtivoAsync(regraCodigo!.Trim(), excluirId, cancellationToken).ConfigureAwait(false);

        return duplicada
            ? Result.Failure(RecusasDaObrigatoriedadeLegal.RegraCodigoDuplicada())
            : Result.Success();
    }

    internal static async Task<Result> SalvarAsync(
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ConstraintVioladaNaGravacao.NomeDaConstraintViolada(ex) is { } constraint
            && ConstraintsDaRegraAtiva.EhDeUnicidadeDaRegra(constraint))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(RecusaDaConstraint(constraint));
        }
    }

    private static DomainError RecusaDaConstraint(string constraint) =>
        ConstraintsDaRegraAtiva.EhConflitoDoCodigoDaRegra(constraint)
            ? RecusasDaObrigatoriedadeLegal.RegraCodigoDuplicada()
            : RecusasDaObrigatoriedadeLegal.ConteudoCanonicoDuplicado();
}
