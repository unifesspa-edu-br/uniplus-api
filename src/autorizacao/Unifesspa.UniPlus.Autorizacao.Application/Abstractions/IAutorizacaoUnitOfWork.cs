namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Unidade de trabalho do módulo Autorização. Especializa <see cref="IUnitOfWork"/> para que
/// os módulos coexistam num processo único sem colisão de registro: cada handler injeta a
/// abstração do seu módulo, roteada para o DbContext correspondente.
/// </summary>
public interface IAutorizacaoUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Descarta o rastreamento de qualquer entidade com alteração ainda não salva (equivalente
    /// a <c>ChangeTracker.Clear()</c>).
    /// </summary>
    /// <remarks>
    /// O outbox transacional do Wolverine chama <c>SaveChangesAsync</c> depois que o handler
    /// retorna, mesmo quando ele devolveu falha. Um handler que traduz a exceção do próprio
    /// <see cref="IUnitOfWork.SalvarAlteracoesAsync"/> ou recusa depois de mutar a entidade
    /// rastreada precisa descartar antes de devolver, ou a gravação é tentada de novo fora de
    /// qualquer <c>try/catch</c> e vaza como <c>500</c>.
    /// </remarks>
    void DescartarAlteracoesNaoSalvas();
}
