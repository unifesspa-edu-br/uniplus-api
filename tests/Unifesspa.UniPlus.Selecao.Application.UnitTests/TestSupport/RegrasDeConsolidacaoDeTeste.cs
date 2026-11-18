namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using NSubstitute;

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
        RegraConsolidacaoCodigo.AvaliacaoUnica, """{"corretores_distintos":1,"naturezas_do_conceito":["categorico","numerico"]}""");

    public static RegraCatalogo CorrecaoIndependente { get; } = Regra(
        RegraConsolidacaoCodigo.CorrecaoIndependente, """{"corretores_distintos":3,"naturezas_do_conceito":["numerico"]}""");

    public static RegraCatalogo ConceitoMaioriaSimples { get; } = Regra(
        RegraConsolidacaoCodigo.ConceitoMaioriaSimples,
        """{"corretores_distintos":3,"criterio":"maioria_simples","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"novo_corretor"}""");

    public static RegraCatalogo ConceitoUnanimidade { get; } = Regra(
        RegraConsolidacaoCodigo.ConceitoUnanimidade,
        """{"corretores_distintos":2,"criterio":"unanimidade","naturezas_do_conceito":["categorico"],"quando_nao_consolida":"decisao_da_banca"}""");

    public static RegraCatalogo Regra(string codigo, string declaracaoDaRegra, string versao = "v1")
    {
        using JsonDocument esquema = JsonDocument.Parse(declaracaoDaRegra);
        using JsonDocument invariantes = JsonDocument.Parse("[]");

        return RegraCatalogo.Criar(codigo, versao, TipoRegra.RegraConsolidacao, esquema.RootElement, invariantes.RootElement, "Regra de teste");
    }

    /// <summary>O catálogo com as regras de teste; sem argumento, o que não está nele devolve <see langword="null"/>.</summary>
    public static IRegraCatalogoReader Catalogo() =>
        Catalogo(AvaliacaoUnica, CorrecaoIndependente, ConceitoMaioriaSimples, ConceitoUnanimidade,
            RegraComHash(RegraCalculoCodigo.ClassificacaoImportada, ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraCalculoCodigo.FormulaMediaPonderada, new string('b', 64)),
            RegraComHash(RegraArredondamentoCodigo.PrecisaoTruncar, new string('c', 64)),
            RegraComHash(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraDistribuicaoVagasCodigo.Institucional, ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(CriterioDesempateCodigo.MaiorIdade, new string('f', 64)),
            RegraComHash(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraPrazoRecursoCodigo.AncoradoEmAto, ProcessoSeletivoConformeBuilder.HashFixo),
            RegraComHash(RegraBonusCodigo.Multiplicativo, ProcessoSeletivoConformeBuilder.HashFixo));

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
