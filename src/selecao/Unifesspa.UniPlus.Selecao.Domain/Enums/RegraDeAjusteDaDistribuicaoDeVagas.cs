namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Regras reconhecidas pelo catálogo.</summary>
public enum RegraDeAjusteDaDistribuicaoDeVagas
{
    [CodigoDeContrato("AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "Ajusta a distribuição conforme o artigo 11, parágrafo único",
        Descricao = "Ajusta a distribuição de vagas quando o arredondamento ultrapassa as vagas ofertadas, conforme o artigo 11, parágrafo único.")]
    AjusteDoTotalPeloArtigo11 = 1,
}
