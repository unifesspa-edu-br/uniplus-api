namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeTiposDeDeficienciaTests
{
    private const string DocumentoComTipos = """
        { "atendimento": { "tiposDeficiencia": [ { "tipoDeficienciaCodigo": "DEFICIENCIA_VISUAL" }, { "tipoDeficienciaCodigo": "TIPO_REMOVIDO" } ] } }
        """;

    private readonly ITipoDeficienciaReader _tipos = Substitute.For<ITipoDeficienciaReader>();

    public DescricaoDeTiposDeDeficienciaTests() =>
        _tipos.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns([new TipoDeficienciaView(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual", "Baixa visão ou cegueira.", true)]);

    [Fact(DisplayName = "O nome do tipo de deficiência entra logo depois do código, com uma leitura do cadastro")]
    public async Task Descrever_TipoVivo_TrazONomeAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComTipos, new DescricaoDeTiposDeDeficiencia(_tipos));

        JsonNode? tipo = documento["atendimento"]!["tiposDeficiencia"]![0];
        DocumentoDescritoDeTeste.Chaves(tipo).Should().Equal("tipoDeficienciaCodigo", "tipoDeficienciaNome");
        tipo!["tipoDeficienciaNome"]!.GetValue<string>().Should().Be("Deficiência visual");
        await _tipos.Received(1).ListarVivosAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de deficiência que o cadastro não devolve vivo fica sem nome")]
    public async Task Descrever_TipoSemCadastroVivo_OmiteONome()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComTipos, new DescricaoDeTiposDeDeficiencia(_tipos));

        documento["atendimento"]!["tiposDeficiencia"]![1]!["tipoDeficienciaNome"].Should().BeNull();
    }
}
