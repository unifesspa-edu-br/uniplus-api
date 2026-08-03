namespace Unifesspa.UniPlus.Publicacoes.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// UnitOfWork do módulo Publicações. Especialização sem novos membros de
/// <see cref="IUnitOfWork"/> que dá um tipo de registro próprio ao módulo —
/// garante o isolamento da transação no monólito modular, onde vários módulos
/// coexistem no mesmo container e um <see cref="IUnitOfWork"/> compartilhado
/// colidiria no contêiner de DI (o último registro venceria).
/// </summary>
public interface IPublicacoesUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Descarta o rastreamento de entidades Added/Modified de uma tentativa de
    /// <see cref="IUnitOfWork.SalvarAlteracoesAsync"/> que falhou. Necessário
    /// no catch de handlers que traduzem exceção de conflito (exclusion
    /// constraint, concorrência otimista) em <c>DomainError</c>: sem isso, o
    /// <c>SaveChangesAsync</c> automático do outbox do Wolverine
    /// (<c>AutoApplyTransactions</c>, ADR-0004) tenta gravar as mesmas
    /// entidades de novo depois que o handler retorna, e a mesma exceção
    /// estoura fora de qualquer catch — 500 em vez do <c>DomainError</c> já
    /// traduzido.
    /// </summary>
    void DescartarAlteracoesNaoSalvas();
}
