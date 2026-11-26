namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DescricaoDeTiposDeAtoTests
{
    private const string DocumentoComPublicacoes = """
        { "cronograma": { "fases": [ { "chave": "a", "publicacoesPrevistas": [ { "atoCodigo": "RESULTADO_PRELIMINAR" } ] } ] },
          "etapas": [ { "chave": "e", "publicacoesPrevistas": [ { "atoCodigo": "RESULTADO_PRELIMINAR" }, { "atoCodigo": "ATO_DESCONHECIDO" } ] } ] }
        """;

    // 23h30 de 26/11/2026 em Belém, que já é 27/11 em UTC: o dia do tipo de ato é o do fuso institucional.
    private static readonly DateTimeOffset NoFimDoDiaEmBelem = new(2026, 11, 27, 2, 30, 0, TimeSpan.Zero);

    private readonly ITipoAtoPublicadoReader _tipos = Substitute.For<ITipoAtoPublicadoReader>();

    public DescricaoDeTiposDeAtoTests() =>
        _tipos.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new TipoAtoPublicadoView("RESULTADO_PRELIMINAR", "Resultado preliminar", false, false, false, true)]);

    private DescricaoDeTiposDeAto Descricao(IResolvedorFusoInstitucional? fuso = null) =>
        new(_tipos, new LeituraDoInstanteDoComando(new RelogioFixo(NoFimDoDiaEmBelem), fuso ?? new ResolvedorFusoDeTeste()));

    [Fact(DisplayName = "O nome do tipo de ato entra na publicação da fase e da etapa, com uma leitura no dia de hoje em Belém")]
    public async Task Descrever_PublicacoesDaFaseEDaEtapa_TrazONomeAoLadoDoCodigoLidoNoDiaDeBelem()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComPublicacoes, Descricao());

        JsonNode? daFase = documento["cronograma"]!["fases"]![0]!["publicacoesPrevistas"]![0];
        JsonNode? daEtapa = documento["etapas"]![0]!["publicacoesPrevistas"]![0];
        DocumentoDescritoDeTeste.Chaves(daFase).Should().Equal("atoCodigo", "atoNome");
        daFase!["atoNome"]!.GetValue<string>().Should().Be("Resultado preliminar");
        daEtapa!["atoNome"]!.GetValue<string>().Should().Be("Resultado preliminar");
        await _tipos.Received(1).ListarVigentesAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 2), new DateOnly(2026, 11, 26), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de ato sem versão vigente no dia fica sem nome")]
    public async Task Descrever_TipoDeAtoSemVersaoVigente_OmiteONome()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComPublicacoes, Descricao());

        documento["etapas"]![0]!["publicacoesPrevistas"]![1]!["atoNome"].Should().BeNull();
    }

    [Fact(DisplayName = "Sem o dia do fuso institucional, nenhum nome é descrito e o catálogo não é consultado")]
    public async Task Descrever_FusoInstitucionalNaoReconhecido_OmiteOsNomesSemConsultar()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(
            DocumentoComPublicacoes, Descricao(new ResolvedorFusoIndisponivelDeTeste()));

        documento["cronograma"]!["fases"]![0]!["publicacoesPrevistas"]![0]!["atoNome"].Should().BeNull();
        await _tipos.DidNotReceiveWithAnyArgs().ListarVigentesAsync(default!, default, default);
    }
}
