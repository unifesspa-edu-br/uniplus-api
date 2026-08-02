namespace Unifesspa.UniPlus.Configuracao.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// UnitOfWork do módulo Configuracao. Especialização de <see cref="IUnitOfWork"/>
/// que dá um tipo de registro próprio ao módulo — garante o isolamento da
/// transação no monólito modular, onde vários módulos coexistem no mesmo
/// container e um <see cref="IUnitOfWork"/> compartilhado colidiria no
/// contêiner de DI (o último registro venceria).
/// </summary>
public interface IConfiguracaoUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Força a checagem imediata de constraints <c>DEFERRABLE</c> pendentes na
    /// transação corrente (equivalente a <c>SET CONSTRAINTS ALL IMMEDIATE</c>).
    /// </summary>
    /// <remarks>
    /// O outbox transacional do Wolverine (<c>UseEntityFrameworkCoreTransactions</c>
    /// + <c>AutoApplyTransactions</c>, ADR-0004) abre a transação ANTES do handler
    /// rodar e só comita DEPOIS do handler retornar — uma exclusion/unique constraint
    /// <c>DEFERRABLE INITIALLY DEFERRED</c> só é checada nesse commit externo, fora do
    /// escopo de qualquer <c>try/catch</c> do handler. Chamar este método logo após
    /// <see cref="IUnitOfWork.SalvarAlteracoesAsync"/> força a checagem AINDA dentro
    /// do handler, onde a exceção pode ser traduzida para um <c>DomainError</c>
    /// (ex.: <c>ExclusionConstraintViolation</c>) em vez de vazar como 500 do commit
    /// do Wolverine.
    /// </remarks>
    Task ForcarChecagemImediataDeConstraintsAsync(CancellationToken cancellationToken = default);
}
