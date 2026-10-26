namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Enums;

/// <summary>
/// A nota de uma área vigente para uma inscrição, como a avaliação a lê. Não expõe leiaute, hash,
/// número de inscrição no ENEM nem dado cadastral: quem avalia não conhece a origem do arquivo.
/// </summary>
public sealed record LancamentoVigente(
    Guid InscricaoId,
    Guid EtapaProcessoId,
    string ItemCodigo,
    decimal Nota,
    SituacaoDaProvaNoEnem Situacao,
    Guid LoteId);

/// <summary>
/// O contrato de leitura das notas lançadas. Só leitura: nenhum caminho altera ou apaga um
/// lançamento.
/// </summary>
public interface ILancamentosDeNotaReader
{
    /// <summary>
    /// Os lançamentos do lote vigente do processo, da etapa e da edição que a inscrição escolhe hoje.
    /// Inscrição sem linha, em conflito ou que mudou de edição desde a importação não aparece: a
    /// ausência de lançamento nunca é lida como nota zero. Quem faltou à prova aparece com a
    /// situação ausente.
    /// </summary>
    Task<IReadOnlyList<LancamentoVigente>> ListarVigentesAsync(
        Guid etapaProcessoId, IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default);
}
