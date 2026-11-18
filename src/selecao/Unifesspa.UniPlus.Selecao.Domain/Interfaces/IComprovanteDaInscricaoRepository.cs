namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Prepara a persistência e lê as evidências imutáveis de cada envio da inscrição, sem confirmar a transação.</summary>
public interface IComprovanteDaInscricaoRepository
{
    /// <summary>
    /// O comprovante que ocupa a posição <paramref name="numero"/> na ordem de emissão da inscrição (o instante, e o id para
    /// desempatar), com o total emitido. Só ele é lido e decifrado; <see langword="null"/> quando a posição não existe.
    /// </summary>
    Task<ComprovanteNaPosicao?> ObterComprovanteAsync(Guid inscricaoId, int numero, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quando cada inscrição emitiu cada comprovante, em ordem de emissão, lido só por projeção, sem carregar nem decifrar o
    /// conteúdo. A posição de cada emissão na sua inscrição é o número do comprovante.
    /// </summary>
    Task<IReadOnlyList<EmissaoDoComprovante>> ObterEmissoesAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default);

    /// <summary>
    /// O instante do primeiro envio de cada inscrição, o menor instante entre os comprovantes, lido só por projeção, sem
    /// carregar nem decifrar o conteúdo deles. A inscrição sem comprovante não consta.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ObterPrimeirosEnviosAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default);

    /// <summary>Grava o comprovante emitido por <see cref="Inscricao.Submeter"/> — nunca pela navegação do agregado.</summary>
    Task AdicionarComprovanteAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken = default);
}
