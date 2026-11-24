namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// As regras de consolidação do rol com o que o seed declara: um corretor para a avaliação única, três para a
/// correção independente, três com novo corretor quando o critério não converge para a maioria simples, e dois com
/// decisão da banca para a unanimidade. O catálogo as devolve pela identidade, como o reader real.
/// </summary>
internal static class RegrasDeConsolidacaoDeTeste
{
    public static RegraCatalogo AvaliacaoUnica { get; } = Regra(
        RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), """{"corretores_distintos":1,"naturezas_do_conceito":["CATEGORICO","NUMERICO"]}""");

    public static RegraCatalogo CorrecaoIndependente { get; } = Regra(
        RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), """{"corretores_distintos":3,"naturezas_do_conceito":["NUMERICO"]}""");

    public static RegraCatalogo ConceitoMaioriaSimples { get; } = Regra(
        RegraDeApuracaoDaAvaliacao.ConceitoMaioriaSimples.Codigo(),
        """{"corretores_distintos":3,"criterio":"MAIORIA_SIMPLES","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"NOVO_CORRETOR"}""");

    public static RegraCatalogo ConceitoUnanimidade { get; } = Regra(
        RegraDeApuracaoDaAvaliacao.ConceitoUnanimidade.Codigo(),
        """{"corretores_distintos":2,"criterio":"UNANIMIDADE","naturezas_do_conceito":["CATEGORICO"],"quando_nao_consolida":"DECISAO_DA_BANCA"}""");

    public static RegraCatalogo Regra(string codigo, string declaracaoDaRegra, string versao = "v1")
    {
        using JsonDocument esquema = JsonDocument.Parse(declaracaoDaRegra);
        using JsonDocument invariantes = JsonDocument.Parse("[]");

        return RegraCatalogo.Criar(codigo, versao, TipoRegra.RegraApuracaoAvaliacao, esquema.RootElement, invariantes.RootElement, "Regra de teste");
    }

    /// <summary>O catálogo com as regras de teste; sem argumento, o que não está nele devolve <see langword="null"/>.</summary>
    public static IRegraCatalogoReader Catalogo() =>
        Catalogo(AvaliacaoUnica, CorrecaoIndependente, ConceitoMaioriaSimples, ConceitoUnanimidade,
            RegraComHash(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraDeCalculoDaNota.MediaPonderada.Codigo(), new string('b', 64)),
            RegraComHash(RegraDeArredondamentoDaNota.Truncar.Codigo(), new string('c', 64)),
            RegraComHash(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraDeDesempate.MaiorIdade.Codigo(), new string('f', 64)),
            RegraComHash(ConvencaoDeContagemDoPrazo.AvancaDataUtil.Codigo(), ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraDeBonus.Multiplicativo.Codigo(), ProcessoSeletivoConformeBuilder.HashFixo));

    private static RegraCatalogo RegraComHash(string codigo, string hash)
    {
        RegraCatalogo regra = Regra(codigo, "{}");
        typeof(RegraCatalogo).GetProperty(nameof(RegraCatalogo.Hash))!.SetValue(regra, hash);
        return regra;
    }

    public static IRegraCatalogoReader Catalogo(params RegraCatalogo[] regras)
    {
        IRegraCatalogoReader reader = Substitute.For<IRegraCatalogoReader>();
        reader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chamada => regras.FirstOrDefault(regra =>
                regra.Codigo == chamada.ArgAt<string>(0) && regra.Versao == chamada.ArgAt<string>(1)));

        return reader;
    }
}
