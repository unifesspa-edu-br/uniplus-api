namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CadastrosDasVagasDoRascunhoDeTeste;

/// <summary>A exportação traz os nomes e o quadro das vagas do mesmo resultado, lido uma vez por cadastro.</summary>
public sealed class ExportacaoDoRascunhoComVagasTests
{
    private readonly CenarioDoRascunho _cenario = new();

    public ExportacaoDoRascunhoComVagasTests()
    {
        Cadastrar(_cenario);
        _cenario.Esquema.Forma.Returns(FormaDosDescritivosDeTeste.Documento());
    }

    private async Task<JsonObject> ExportarAsync()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(
            CenarioDoRascunho.Ceps, CadastrosDasVagasDoRascunhoDeTeste.Documento(Distribuicao(OfertaDePedagogia), Distribuicao(OfertaDeMedicina)));
        return await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None);
    }

    [Fact(DisplayName = "A exportação traz o nome do curso, o nome e a descrição das modalidades, o quadro e o total de cada distribuição")]
    public async Task Exportar_DistribuicoesComCadastrosVivos_TrazNomesEQuadro()
    {
        JsonObject documento = await ExportarAsync();

        JsonObject medicina = documento["distribuicaoVagas"]![1]!.AsObject();
        medicina["cursoNome"]!.GetValue<string>().Should().Be("Medicina");
        medicina["modalidades"]![0]!["nome"]!.GetValue<string>().Should().Be("Ampla concorrência, ação afirmativa da Unifesspa para pessoas com deficiência");
        medicina["modalidades"]![0]!["descricao"]!.GetValue<string>().Should().Be("Ampla concorrência para pessoas com deficiência");
        medicina["quadro"]!.AsArray().Should().HaveCount(2);
        medicina["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(10);
    }

    [Fact(DisplayName = "Os nomes e o quadro usam a mesma leitura: o cadastro de ofertas vai uma vez, por códigos, e a leitura de todos os vivos nunca")]
    public async Task Exportar_LeCadaCadastroUmaVezPorCodigos()
    {
        await ExportarAsync();

        await _cenario.Ofertas.Received(1).ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _cenario.Modalidades.Received(1).ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _cenario.Ofertas.DidNotReceiveWithAnyArgs().ListarVivasAsync(default);
        await _cenario.Modalidades.DidNotReceiveWithAnyArgs().ListarVivosAsync(default);
    }

    [Fact(DisplayName = "O cadastro é lido vivo: com o mesmo rascunho, a nova referência demográfica muda o quadro e o total da exportação seguinte, sem mudar a revisão")]
    public async Task Exportar_ReferenciaDemograficaMudou_QuadroETotalSeguemOCadastroVivoSemMudarARevisao()
    {
        CadastrosDaLeiDeCotasDoRascunhoDeTeste.Cadastrar(_cenario, percentualDePessoasPretasPardasEIndigenas: 50m);
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(
            CenarioDoRascunho.Ceps, CadastrosDasVagasDoRascunhoDeTeste.Documento(CadastrosDaLeiDeCotasDoRascunhoDeTeste.Distribuicao(OfertaDeMedicina, 40)));
        int revisaoInicial = rascunho.Revisao;
        JsonNode antes = (await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None))["distribuicaoVagas"]![0]!;

        CadastrosDaLeiDeCotasDoRascunhoDeTeste.TrocarOPercentual(_cenario, percentualDePessoasPretasPardasEIndigenas: 30m);
        JsonNode depois = (await _cenario.Exportacao.ExportarAsync(rascunho, CancellationToken.None))["distribuicaoVagas"]![0]!;

        antes["quadro"]!.AsArray().Should().HaveCount(9);
        depois["quadro"]!.ToJsonString().Should().NotBe(antes["quadro"]!.ToJsonString());
        antes["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(40);
        depois["totalDeVagasDoEdital"]!.GetValue<int>().Should().Be(40);
        rascunho.Revisao.Should().Be(revisaoInicial);
    }
}
