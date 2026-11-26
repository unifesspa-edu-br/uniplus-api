namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class DescricaoDeRecursosDeAcessibilidadeTests
{
    private const string DocumentoComRecursos = """
        { "atendimento": { "recursos": [ { "recursoCodigo": "LEDOR" }, { "recursoCodigo": "RECURSO_REMOVIDO" } ] } }
        """;

    private readonly IRecursoAcessibilidadeReader _recursos = Substitute.For<IRecursoAcessibilidadeReader>();

    public DescricaoDeRecursosDeAcessibilidadeTests() =>
        _recursos.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns([new RecursoAcessibilidadeView(Guid.CreateVersion7(), "LEDOR", "Ledor"), new RecursoAcessibilidadeView(Guid.CreateVersion7(), "INTERPRETE_DE_LIBRAS", "Intérprete de Libras")]);

    [Fact(DisplayName = "O nome do recurso entra logo depois do código, com uma leitura do cadastro")]
    public async Task Descrever_RecursoVivo_TrazONomeAoLadoDoCodigo()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComRecursos, new DescricaoDeRecursosDeAcessibilidade(_recursos));

        JsonNode? recurso = documento["atendimento"]!["recursos"]![0];
        DocumentoDescritoDeTeste.Chaves(recurso).Should().Equal("recursoCodigo", "recursoNome");
        recurso!["recursoNome"]!.GetValue<string>().Should().Be("Ledor");
        await _recursos.Received(1).ListarVivosAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O recurso que o cadastro não devolve vivo fica sem nome")]
    public async Task Descrever_RecursoSemCadastroVivo_OmiteONome()
    {
        JsonObject documento = await DocumentoDescritoDeTeste.DescreverAsync(DocumentoComRecursos, new DescricaoDeRecursosDeAcessibilidade(_recursos));

        documento["atendimento"]!["recursos"]![1]!["recursoNome"].Should().BeNull();
    }
}
