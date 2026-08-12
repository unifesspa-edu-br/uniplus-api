namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Unidade de trabalho específica do módulo Seleção. Especializa o
/// <see cref="IUnitOfWork"/> compartilhado para permitir coexistência de
/// múltiplos módulos num mesmo container DI (cada módulo registra e injeta a
/// própria interface, evitando colisão do registro genérico de
/// <see cref="IUnitOfWork"/>).
/// </summary>
public interface ISelecaoUnitOfWork : IUnitOfWork
{
    /// <summary>
    /// Descarta o rastreamento de qualquer entidade com alteração ainda não
    /// salva (equivalente a <c>ChangeTracker.Clear()</c>).
    /// </summary>
    /// <remarks>
    /// O outbox transacional do Wolverine (<c>AutoApplyTransactions</c>, ADR-0004)
    /// chama <c>SaveChangesAsync</c> nele mesmo DEPOIS que o handler retorna, para
    /// persistir os envelopes de mensagem — independente de o handler ter
    /// devolvido sucesso ou um <c>Result</c> de falha. Se um handler mutar uma
    /// entidade tracked (ex.: <c>EtapaProcesso.AtualizarDados</c> num loop de
    /// reconciliação) e devolver a falha SEM chamar este método antes, a mutação
    /// parcial continua rastreada — o <c>SaveChangesAsync</c> automático do
    /// Wolverine a persiste mesmo assim, e uma requisição rejeitada (422/409)
    /// deixa o agregado mutado de qualquer forma. Chamar este método antes de
    /// devolver qualquer falha ocorrida DEPOIS que a mutação começou evita a
    /// persistência parcial. Mesmo padrão de <c>IConfiguracaoUnitOfWork</c>/
    /// <c>IPublicacoesUnitOfWork</c>.
    /// </remarks>
    void DescartarAlteracoesNaoSalvas();
}
