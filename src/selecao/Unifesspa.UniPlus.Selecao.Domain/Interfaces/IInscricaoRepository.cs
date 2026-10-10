namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Repositório da inscrição do candidato (issue #1858, ADR-0121).</summary>
public interface IInscricaoRepository
{
    Task<Inscricao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Localiza a inscrição pelo dono (CPF) num processo — a mesma chave do índice único
    /// <c>(processo_seletivo_id, cpf_indice)</c>. Assíncrono: a implementação computa o índice
    /// cego via <c>IUniPlusBlindIndexService</c>, que não tem versão síncrona.
    /// </summary>
    Task<Inscricao?> ObterPorProcessoECpfAsync(Guid processoSeletivoId, Cpf cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas as inscrições do dono (CPF), em qualquer processo seletivo (issue #1860, "Minhas
    /// inscrições") — mesmo índice cego de <see cref="ObterPorProcessoECpfAsync"/>, sem o filtro
    /// de processo.
    /// </summary>
    Task<IReadOnlyList<Inscricao>> ObterTodasPorCpfAsync(Cpf cpf, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Inscricao entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assíncrono porque o CPF e os documentos são recifrados a cada atualização (ADR-0121) — a
    /// implementação chama <c>IUniPlusEncryptionService</c>, que não tem versão síncrona.
    /// </summary>
    Task AtualizarAsync(Inscricao entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os comprovantes gravados das inscrições, em ordem de emissão — o agregado reidratado não os
    /// carrega.
    /// </summary>
    Task<IReadOnlyList<ComprovanteInscricao>> ObterComprovantesAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default);

    /// <summary>Grava o comprovante emitido por <see cref="Inscricao.Submeter"/> — nunca pela navegação do agregado.</summary>
    Task AdicionarComprovanteAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken = default);
}
