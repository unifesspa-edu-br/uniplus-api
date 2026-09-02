using Unifesspa.UniPlus.Discentes.Domain.Entities;

namespace Unifesspa.UniPlus.Discentes.Domain.Interfaces;

public interface IVinculoDiscenteRepository
{
    Task<VinculoDiscente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VinculoDiscente?> ObterPorIdSigaaAsync(long idDiscenteSigaa, CancellationToken cancellationToken = default);
    Task AdicionarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assíncrono porque o CPF é recifrado a cada atualização (ADR-0121) — a
    /// implementação chama <c>IUniPlusEncryptionService</c>, que não tem versão
    /// síncrona.
    /// </summary>
    Task AtualizarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava um lote vindo da sincronização, inserindo o que ainda não existe, atualizando
    /// o que mudou e deixando intacto o que continua igual.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A comparação é pelo resumo do conteúdo, não campo a campo: a esmagadora maioria dos
    /// vínculos não muda de um dia para o outro, e reescrevê-los custaria uma recifragem
    /// de CPF por linha, todos os dias, sem nada mudar no banco.
    /// </para>
    /// <para>
    /// Vínculo que não aparece no lote <b>não é tocado</b>. Ausência não é remoção: uma
    /// execução que só alcançou parte das páginas não pode apagar o que não chegou a ver.
    /// </para>
    /// </remarks>
    Task<ResultadoDaGravacao> GravarLoteAsync(
        IReadOnlyList<VinculoSincronizavel> lote,
        CancellationToken cancellationToken = default);
}
