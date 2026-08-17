namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Unit of Work específica do módulo OrganizacaoInstitucional. Especializa
/// <see cref="IUnitOfWork"/> para que múltiplos módulos coexistam num processo
/// único sem colisão de registro no container — cada handler injeta a abstração
/// do seu próprio módulo, roteada para o DbContext correspondente.
/// </summary>
public interface IOrganizacaoInstitucionalUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Descarta o rastreamento de qualquer entidade com alteração ainda não
    /// salva (equivalente a <c>ChangeTracker.Clear()</c>).
    /// </summary>
    /// <remarks>
    /// O outbox transacional do Wolverine (<c>AutoApplyTransactions</c>, ADR-0004)
    /// chama <c>SaveChangesAsync</c> nele mesmo DEPOIS que o handler retorna, para
    /// persistir os envelopes de mensagem — independente de o handler ter
    /// devolvido sucesso ou um <c>Result</c> de falha. Se um handler capturar uma
    /// exceção do seu próprio <see cref="IUnitOfWork.SalvarAlteracoesAsync"/> e
    /// devolver a falha SEM chamar este método antes, a entidade Added/Modified
    /// da tentativa que falhou continua rastreada — o <c>SaveChangesAsync</c>
    /// automático do Wolverine tenta gravá-la de novo, a mesma exceção estoura de
    /// novo, mas agora FORA de qualquer <c>try/catch</c> do handler, e vaza como
    /// 500 em vez do <c>DomainError</c> que o handler já tinha traduzido. Chamar
    /// este método dentro do <c>catch</c>, antes de devolver a falha, evita a
    /// segunda tentativa.
    /// </remarks>
    void DescartarAlteracoesNaoSalvas();
}
