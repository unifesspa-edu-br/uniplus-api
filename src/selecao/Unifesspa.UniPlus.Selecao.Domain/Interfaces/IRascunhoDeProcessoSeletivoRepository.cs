namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

/// <summary>
/// Repositório de <see cref="RascunhoDeProcessoSeletivo"/>, independente de <see cref="IProcessoSeletivoRepository"/>:
/// o rascunho é um agregado próprio, com a linha dele coordenando quem o salva, exclui e publica.
/// </summary>
public interface IRascunhoDeProcessoSeletivoRepository
{
    /// <summary>O rascunho sem rastreamento, para leitura; nulo quando não existe.</summary>
    Task<RascunhoDeProcessoSeletivo?> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// O rascunho rastreado, lido depois de bloquear a linha com <c>FOR UPDATE</c> na transação ambiente: as operações
    /// concorrentes sobre o mesmo rascunho esperam aqui, por um tempo limitado. Nulo quando não existe.
    /// </summary>
    Task<RascunhoDeProcessoSeletivo?> ObterParaMutacaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloqueia a linha com <c>FOR SHARE</c>, que convive com outro bloqueio compartilhado e espera o exclusivo, e devolve se o
    /// rascunho existe. O que depende do rascunho existir se grava sob esse bloqueio.
    /// </summary>
    Task<bool> BloquearCompartilhadoAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarAsync(RascunhoDeProcessoSeletivo rascunho, CancellationToken cancellationToken = default);

    /// <summary>Apaga a linha na gravação da unidade de trabalho; o rascunho não é soft-deletable.</summary>
    void Remover(RascunhoDeProcessoSeletivo rascunho);
}
