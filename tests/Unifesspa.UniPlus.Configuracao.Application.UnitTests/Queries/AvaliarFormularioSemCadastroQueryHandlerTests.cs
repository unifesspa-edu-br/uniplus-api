namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A avaliação sem cadastro das regras de um formulário renderizável: o resultado na forma portátil, com
/// o estado de cada fato, e a recusa da ocorrência sem identidade própria no grupo.
/// </summary>
public sealed class AvaliarFormularioSemCadastroQueryHandlerTests
{
    private static readonly FormularioPortavel Regras = new(
        [new EtapaPortavel("DADOS", null, [
            new ItemPortavel("TIPO_ENDERECO", null, "SEMPRE", null, [], null, ["URBANO", "ALDEIA"]),
            new ItemPortavel("NOME_ALDEIA", [[new CondicaoPrecondicaoInput("TIPO_ENDERECO", "IGUAL", JsonSerializer.SerializeToElement("ALDEIA"))]], "SEMPRE", null, [], null, null),
        ], [])],
        [],
        [],
        []);

    [Fact(DisplayName = "O endereço urbano esconde a aldeia, e o resultado sai na forma portátil com o estado de cada fato")]
    public void Handle_EnderecoUrbano_EscondeAAldeia()
    {
        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(new AvaliarFormularioSemCadastroQuery(new(
            Regras, new Dictionary<string, JsonElement> { ["TIPO_ENDERECO"] = JsonSerializer.SerializeToElement("URBANO") }, null, null, null)));

        resultado.Value!.Campos.Select(static c => (c.FatoCodigo, c.Visivel, c.Estado)).Should().Equal(
            ("TIPO_ENDERECO", "VERDADEIRO", "RESOLVIDO"), ("NOME_ALDEIA", "FALSO", "NAO_APLICAVEL"));
    }

    [Fact(DisplayName = "Ocorrência sem identidade própria no grupo é recusada com o caminho dela")]
    public void Handle_OcorrenciaSemIdentidade_Recusa()
    {
        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(new AvaliarFormularioSemCadastroQuery(new(
            Regras, null, new Dictionary<string, IReadOnlyList<OcorrenciaRecebida>?> { ["FAMILIA"] = [new OcorrenciaRecebida(" ", null)] }, null, null)));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("grupos.FAMILIA[0].id");
    }
}
