namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Enums;

/// <summary>
/// A nota da etapa de uma inscrição numa oferta de curso que ela concorre, como a classificação a lê.
/// Não expõe leiaute, hash nem número de inscrição no ENEM: quem classifica não conhece a origem do arquivo.
/// </summary>
public sealed record AvaliacaoVigente(
    Guid InscricaoId,
    Guid EtapaProcessoId,
    Guid OfertaCursoId,
    string GrupoAreaEnemCodigo,
    SituacaoDaAvaliacao Situacao,
    decimal? Nota,
    Guid LoteId);

/// <summary>
/// O contrato de leitura das avaliações da etapa. Só leitura: nenhum caminho altera ou apaga uma
/// avaliação.
/// </summary>
public interface IAvaliacoesDaEtapaReader
{
    /// <summary>
    /// As avaliações do lote vigente do processo, da etapa e da edição que a inscrição escolhe hoje,
    /// só das ofertas que a inscrição ainda concorre. Inscrição enviada depois do lote não aparece:
    /// a ausência significa "aguarda importação", nunca nota zero. A falta de nota aparece como situação.
    /// </summary>
    Task<IReadOnlyList<AvaliacaoVigente>> ListarVigentesAsync(
        Guid etapaProcessoId, IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default);
}
