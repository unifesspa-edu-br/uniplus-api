namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A avaliação sem cadastro das regras de um formulário renderizável: a recusa da ocorrência sem
/// identidade própria no grupo e a do envio simulado numa zona que o ambiente não reconhece ou sem
/// instante. O resultado da avaliação é conferido pelo corpus de casos.
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

    [Fact(DisplayName = "Ocorrência sem identidade própria no grupo é recusada com o caminho dela")]
    public void Handle_OcorrenciaSemIdentidade_Recusa()
    {
        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(new AvaliarFormularioSemCadastroQuery(new(
            Regras, null, new Dictionary<string, IReadOnlyList<OcorrenciaRecebida>?> { ["FAMILIA"] = [new OcorrenciaRecebida(" ", null)] }, null, null)));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("grupos.FAMILIA[0].id");
    }

    [Fact(DisplayName = "Envio simulado numa zona que o ambiente não reconhece é recusado no fuso")]
    public void Handle_FusoDoEnvioDesconhecido_Recusa()
    {
        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(new AvaliarFormularioSemCadastroQuery(new(
            Regras, null, null, null, null, new EnvioSimuladoInput(DateTimeOffset.UnixEpoch, "America/Atlantida"))));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "envio.fuso",
            Error = new { Code = FormularioPortavelErrorCodes.FusoDoEnvioDesconhecido },
        });
    }

    [Fact(DisplayName = "Envio simulado sem instante é recusado no instante, sem estourar a conta dos limites")]
    public void Handle_EnvioSemInstante_Recusa()
    {
        // O corpo sem "instante" no envio: o desserializador deixa o instante no primeiro do calendário.
        string corpo = "{\"regras\": " + JsonSerializer.Serialize(RegrasComFaixaDeData, JsonSerializerOptions.Web)
            + ", \"envio\": {\"fuso\": \"America/Belem\"}}";
        AvaliacaoSemCadastroInput simulacao = JsonSerializer.Deserialize<AvaliacaoSemCadastroInput>(corpo, JsonSerializerOptions.Web)!;

        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(new AvaliarFormularioSemCadastroQuery(simulacao));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "envio.instante",
            Error = new { Code = FormularioPortavelErrorCodes.InstanteDoEnvioForaDoCalendario },
        });
    }

    private static readonly FormularioPortavel RegrasComFaixaDeData = new(
        [new EtapaPortavel("DADOS", null, [
            new ItemPortavel("DATA_NASCIMENTO", null, "SEMPRE", null,
                [new RestricaoValorInput("FAIXA_DE_DATA", DataMinima: new LimiteDeDataInput("ENVIO", Anos: -120, Dias: 0))], null, null),
        ], [])],
        [],
        [],
        []);
}
