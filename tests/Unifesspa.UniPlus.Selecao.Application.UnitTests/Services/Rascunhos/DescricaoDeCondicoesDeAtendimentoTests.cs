namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeCondicoesDeAtendimentoTests
{
    private const string DocumentoComCondicoes = """
        { "atendimento": { "condicoes": [ { "condicaoCodigo": "LACTANTE" }, { "condicaoCodigo": "CONDICAO_REMOVIDA" } ] } }
        """;

    private readonly ICondicaoAtendimentoReader _condicoes = Substitute.For<ICondicaoAtendimentoReader>();

    public DescricaoDeCondicoesDeAtendimentoTests() =>
        _condicoes.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns([new CondicaoAtendimentoView(Guid.CreateVersion7(), "LACTANTE", "Lactante"), new CondicaoAtendimentoView(Guid.CreateVersion7(), "GESTANTE", "Gestante")]);

    [Fact(DisplayName = "O nome da condição entra logo depois do código, com uma leitura do cadastro")]
    public async Task Descrever_CondicaoViva_TrazONomeAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComCondicoes, new DescricaoDeCondicoesDeAtendimento(_condicoes));

        JsonNode? condicao = documento["atendimento"]!["condicoes"]![0];
        DocumentoDescritoDeTeste.Chaves(condicao).Should().Equal("condicaoCodigo", "condicaoNome");
        condicao!["condicaoNome"]!.GetValue<string>().Should().Be("Lactante");
        await _condicoes.Received(1).ListarVivosAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A condição que o cadastro não devolve viva fica sem nome")]
    public async Task Descrever_CondicaoSemCadastroVivo_OmiteONome()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComCondicoes, new DescricaoDeCondicoesDeAtendimento(_condicoes));

        documento["atendimento"]!["condicoes"]![1]!["condicaoNome"].Should().BeNull();
    }
}
