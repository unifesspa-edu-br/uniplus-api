namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Regras.Enums;

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
            new PoliticaDoGabaritoDaProva(),
            new PoliticaDaNotaDaEtapa(),
        }.ToDictionary(static p => p.Tipo);

    /// <summary>A política do tipo.</summary>
    public static IPoliticaDoTipoDeMotivo Do(TipoMotivoDeDecisao tipo) => PorTipo[tipo];
}
