namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// Com mais de um bloco ilegível, a projeção do certame recusa citando o primeiro na ordem em que a lê:
/// identidade, depois oferta, depois participação, e a retificação por último.
/// </summary>
public sealed class OrdemDasRecusasDaProjecaoDoCertameTests
{
    public static TheoryData<string, string, string, string> Pares()
    {
        IReadOnlyList<(string Bloco, string Parte)> ordem = PartesDoContratoDoCertame.NaOrdemDaLeitura;
        TheoryData<string, string, string, string> pares = [];
        for (int i = 0; i < ordem.Count - 1; i++)
        {
            pares.Add(ordem[i].Bloco, ordem[i].Parte, ordem[i + 1].Bloco, ordem[i + 1].Parte);
        }

        pares.Add(ordem[0].Bloco, ordem[0].Parte, ordem[^1].Bloco, ordem[^1].Parte);
        return pares;
    }

    [Theory(DisplayName = "Com dois blocos ilegíveis, a recusa cita o que a projeção lê primeiro")]
    [MemberData(nameof(Pares))]
    public void Projetar_DoisBlocosIlegiveis_CitaOPrimeiro(string primeiroBloco, string primeiraParte, string segundoBloco, string segundaParte)
    {
        Result<CertamePublicadoDto> resultado = ProjecaoDoCertameDeTeste.ProjetarComBlocosIlegiveis(segundoBloco, primeiroBloco);

        resultado.Error!.Message.Should().Contain($"'{primeiraParte}'").And.NotContain($"'{segundaParte}'");
    }

    [Fact(DisplayName = "O período e a taxa quebrados juntos citam o período de inscrição")]
    public void Projetar_PeriodoETaxaIlegiveis_CitaOPeriodo() =>
        ProjecaoDoCertameDeTeste.ProjetarComBlocosIlegiveis("taxaInscricao", "periodo")
            .Error!.Message.Should().Contain("'período de inscrição'");

    [Fact(DisplayName = "As ofertas e as vagas quebradas juntas citam as ofertas")]
    public void Projetar_OfertasEVagasIlegiveis_CitaAsOfertas() =>
        ProjecaoDoCertameDeTeste.ProjetarComBlocosIlegiveis("vagas", "ofertas")
            .Error!.Message.Should().Contain("'ofertas'");

    [Fact(DisplayName = "As unidades ilegíveis citam a unidade administradora, e não o documento do edital que também as lê")]
    public void Projetar_UnidadesIlegiveis_CitaAUnidadeAdministradora() =>
        ProjecaoDoCertameDeTeste.ProjetarComBlocosIlegiveis("identidadesUnidade")
            .Error!.Message.Should().Contain("'unidade administradora'").And.NotContain("'documento do edital'");
}
