namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using Domain.Services;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que a apuração da nota lê do edital divulgado: o grupo de área de cada oferta de curso e os
/// pesos das áreas de cada grupo, ambos congelados na versão divulgada.
/// </summary>
public sealed record EditalParaApuracaoDaNota(
    IReadOnlyDictionary<Guid, string> GrupoPorOferta,
    IReadOnlyDictionary<string, IReadOnlyList<PesoDaArea>> Quadro)
{
    /// <summary>
    /// A publicação exige grupo de área em toda oferta e o grupo no quadro de pesos quando a nota
    /// vem do ENEM com cálculo local; uma oferta sem grupo aqui é versão divulgada fora desse contrato.
    /// </summary>
    public static EditalParaApuracaoDaNota De(ConfiguracaoPublicada configuracaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);

        Dictionary<Guid, string> grupos = configuracaoPublicada.Vagas.DistribuicaoVagas
            .DistinctBy(static o => o.OfertaCursoOrigemId)
            .ToDictionary(
                static o => o.OfertaCursoOrigemId,
                static o => o.GrupoAreaEnem?.Codigo
                    ?? throw new InvalidOperationException("Há oferta sem grupo de área do ENEM no edital divulgado cuja nota vem do ENEM."));
        ConfiguracaoClassificacao classificacao = configuracaoPublicada.Avaliacao.Classificacao
            ?? throw new InvalidOperationException("A configuração publicada não declarou a classificação para apurar as notas do Enem.");
        return new EditalParaApuracaoDaNota(grupos, classificacao.QuadroPesoAreaEnem.ToDictionary(static g => g.GrupoAreaEnem.Codigo,
            static g => (IReadOnlyList<PesoDaArea>)[.. g.Areas.Select(static a => new PesoDaArea(a.Codigo, a.Peso))]));
    }
}
