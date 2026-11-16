namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// As regras de consolidação do rol com o que o seed declara: um corretor para a avaliação única e três para a
/// correção independente, e o catálogo que as devolve pela identidade, como o reader real.
/// </summary>
internal static class RegrasDeConsolidacaoDeTeste
{
    public static RegraCatalogo AvaliacaoUnica { get; } = Regra(
        RegraConsolidacaoCodigo.AvaliacaoUnica, """{"corretores_distintos":1,"naturezas_do_conceito":["categorico","numerico"]}""");

    public static RegraCatalogo CorrecaoIndependente { get; } = Regra(
        RegraConsolidacaoCodigo.CorrecaoIndependente, """{"corretores_distintos":3,"naturezas_do_conceito":["numerico"]}""");

    public static RegraCatalogo Regra(string codigo, string esquemaArgs, string versao = "v1")
    {
        using JsonDocument esquema = JsonDocument.Parse(esquemaArgs);
        using JsonDocument invariantes = JsonDocument.Parse("[]");

        return RegraCatalogo.Criar(codigo, versao, TipoRegra.RegraConsolidacao, esquema.RootElement, invariantes.RootElement, "Regra de teste");
    }

    /// <summary>O catálogo com as duas regras do seed; sem argumento, o que não está nele devolve <see langword="null"/>.</summary>
    public static IRegraCatalogoReader Catalogo() => Catalogo(AvaliacaoUnica, CorrecaoIndependente);

    public static IRegraCatalogoReader Catalogo(params RegraCatalogo[] regras)
    {
        IRegraCatalogoReader reader = Substitute.For<IRegraCatalogoReader>();
        reader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chamada => regras.FirstOrDefault(regra =>
                regra.Codigo == chamada.ArgAt<string>(0) && regra.Versao == chamada.ArgAt<string>(1)));

        return reader;
    }
}
