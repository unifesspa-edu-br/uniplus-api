namespace Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Unidade de trabalho do escopo: a gravação de todos os módulos que compartilham o contexto de persistência.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta as alterações ainda não salvas de todo o escopo (equivalente a <c>ChangeTracker.Clear()</c>).
    /// </summary>
    /// <remarks>
    /// O outbox transacional do Wolverine (ADR-0004) chama <c>SaveChangesAsync</c> depois que o handler retorna, mesmo
    /// quando ele devolveu uma falha: um <c>Result.Failure</c> não desfaz o que foi mutado. Quem traduz a exceção do
    /// próprio <see cref="SalvarAlteracoesAsync"/> ou recusa depois de mutar uma entidade rastreada descarta antes de
    /// devolver, ou a gravação é tentada de novo fora de qualquer <c>try/catch</c> e vaza como 500.
    /// </remarks>
    void DescartarAlteracoesNaoSalvas();

    /// <summary>
    /// Força a checagem imediata das constraints <c>DEFERRABLE</c> pendentes na transação corrente
    /// (<c>SET CONSTRAINTS ALL IMMEDIATE</c>).
    /// </summary>
    /// <remarks>
    /// A transação do outbox abre antes do handler e só confirma depois que ele retorna: uma constraint
    /// <c>DEFERRABLE INITIALLY DEFERRED</c> só seria checada nesse commit externo, fora de qualquer <c>try/catch</c>.
    /// Chamar este método logo após <see cref="SalvarAlteracoesAsync"/> traz a checagem para dentro do handler, onde a
    /// exceção vira <c>DomainError</c>.
    /// </remarks>
    Task ForcarChecagemImediataDeConstraintsAsync(CancellationToken cancellationToken = default);
}
