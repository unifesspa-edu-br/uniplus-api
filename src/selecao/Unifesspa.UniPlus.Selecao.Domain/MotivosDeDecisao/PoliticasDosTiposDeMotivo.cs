namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>A política de cada tipo de motivo, uma por tipo do vocabulário fechado.</summary>
public static class PoliticasDosTiposDeMotivo
{
    private static readonly Dictionary<TipoMotivoDeDecisao, IPoliticaDoTipoDeMotivo> PorTipo =
        new IPoliticaDoTipoDeMotivo[]
        {
            new PoliticaDaIsencaoDaTaxa(),
            new PoliticaDaHomologacaoDasInscricoes(),
            new PoliticaDaAnaliseDocumental(),
            new PoliticaDaHeteroidentificacao(),
            new PoliticaDaAvaliacaoBiopsicossocial(),
            new PoliticaDaClassificacao(),
        }.ToDictionary(static p => p.Tipo);

    /// <summary>A política do tipo, ou nula para a sentinela <see cref="TipoMotivoDeDecisao.Nenhum"/>.</summary>
    public static IPoliticaDoTipoDeMotivo? Do(TipoMotivoDeDecisao tipo) =>
        PorTipo.GetValueOrDefault(tipo);
}
