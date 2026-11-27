namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CadastrosDasVagasDoRascunhoDeTeste;

/// <summary>Cada cadastro das vagas é lido uma vez, pelos códigos distintos que as distribuições citam.</summary>
public sealed class LeituraDasVagasDoRascunhoTests
{
    private readonly CenarioDoRascunho _cenario = new();

    public LeituraDasVagasDoRascunhoTests() => Cadastrar(_cenario);

    private static JsonObject Documento(params string[] distribuicoes) => JsonNode.Parse(CadastrosDasVagasDoRascunhoDeTeste.Documento(distribuicoes))!.AsObject();

    [Fact(DisplayName = "A mesma oferta e as mesmas modalidades em duas distribuições vão uma vez a cada cadastro, com os códigos distintos")]
    public async Task Ler_DuasDistribuicoesComOsMesmosCodigos_LeCadaCadastroUmaVez()
    {
        await _cenario.LeituraDasVagas.LerAsync(Documento(Distribuicao(OfertaDePedagogia), Distribuicao(OfertaDePedagogia)), CancellationToken.None);

        await _cenario.Ofertas.Received(1).ListarVivasPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.SequenceEqual(new[] { OfertaDePedagogia })), Arg.Any<CancellationToken>());
        await _cenario.Modalidades.Received(1).ListarVivosPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Order(StringComparer.Ordinal).SequenceEqual(new[] { Ampla, AmplaComDeficiencia })), Arg.Any<CancellationToken>());
        await _cenario.Regras.Received(1).ListarPorReferenciasAsync(
            Arg.Is<IReadOnlyCollection<(string Codigo, string Versao)>>(regras => regras.Select(static regra => regra.Codigo).SequenceEqual(new[] { RegraInstitucional }) && regras.All(static regra => regra.Versao == "v1")), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Duas ofertas distintas vão juntas ao cadastro de ofertas, numa só leitura")]
    public async Task Ler_DuasOfertasDistintas_LeOCadastroDeOfertasUmaVez()
    {
        await _cenario.LeituraDasVagas.LerAsync(Documento(Distribuicao(OfertaDePedagogia), Distribuicao(OfertaDeMedicina)), CancellationToken.None);

        await _cenario.Ofertas.Received(1).ListarVivasPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Order(StringComparer.Ordinal).SequenceEqual(new[] { OfertaDeMedicina, OfertaDePedagogia })), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O documento sem distribuição não consulta nenhum cadastro")]
    public async Task Ler_SemDistribuicao_NaoConsultaNenhumCadastro()
    {
        await _cenario.LeituraDasVagas.LerAsync(new JsonObject(), CancellationToken.None);

        await _cenario.Ofertas.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
        await _cenario.Modalidades.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
        await _cenario.ReferenciasDemograficas.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
        await _cenario.Regras.DidNotReceiveWithAnyArgs().ListarPorReferenciasAsync(default!, default);
    }

    [Fact(DisplayName = "As distribuições lidas guardam a posição que tinham no documento, ainda que a anterior não possa ser calculada")]
    public async Task Ler_DistribuicaoAnteriorNaoCalculavel_MantemAPosicaoDoDocumento()
    {
        VagasDoRascunhoComCadastrosVivos vagas = await _cenario.LeituraDasVagas.LerAsync(
            Documento(Distribuicao("OFERTA_REMOVIDA"), Distribuicao(OfertaDeMedicina)), CancellationToken.None);

        vagas.DistribuicoesCalculadas().Select(static calculada => calculada.PosicaoNoDocumento).Should().Equal(1);
    }
}
