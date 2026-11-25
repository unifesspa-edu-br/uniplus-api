namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// A avaliação sem cadastro das regras de um formulário apresentado: a recusa da ocorrência sem
/// identidade própria no grupo e a do envio simulado numa zona que o ambiente não reconhece ou no primeiro
/// instante do calendário. O resultado da avaliação é conferido pelo corpus de casos.
/// </summary>
public sealed class AvaliarFormularioSemCadastroQueryHandlerTests
{
    private static readonly FormularioPortavel Regras = new(
        [new PaginaPortavel("DADOS", null, [
            new ItemPortavel("TIPO_ENDERECO", Sempre(), ["URBANO", "ALDEIA"], null),
            new ItemPortavel("NOME_ALDEIA", Sempre([[new CondicaoPrecondicaoDto("TIPO_ENDERECO", "IGUAL", JsonSerializer.SerializeToElement("ALDEIA"))]]), null, null),
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

    [Fact]
    public void Handle_GrupoSemOcorrencias_PreservaOCaminho()
    {
        FormularioPortavel regras = new([new PaginaPortavel("DADOS", null, [],
            [new GrupoPortavel("FAMILIA", null!, new ExibicaoEObrigatoriedadeDto(null, new ObrigatoriedadeDto("NUNCA", null)), [])])], [], [], []);

        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(
            new AvaliarFormularioSemCadastroQuery(new(regras, null, null, null, null)));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("paginas[0].grupos[0].ocorrencias");
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

    [Fact(DisplayName = "Envio simulado no primeiro instante do calendário é recusado no instante, sem estourar a conta dos limites")]
    public void Handle_EnvioNoPrimeiroInstanteDoCalendario_Recusa()
    {
        string corpo = "{\"regras\": " + JsonSerializer.Serialize(RegrasComFaixaDeData, JsonSerializerOptions.Web)
            + ", \"envio\": {\"instante\": \"0001-01-01T00:00:00Z\", \"fuso\": \"America/Belem\"}}";
        AvaliacaoSemCadastroInput simulacao = JsonSerializer.Deserialize<AvaliacaoSemCadastroInput>(corpo, JsonSerializerOptions.Web)!;

        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(new AvaliarFormularioSemCadastroQuery(simulacao));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "envio.instante",
            Error = new { Code = FormularioPortavelErrorCodes.InstanteDoEnvioForaDoCalendario },
        });
    }

    private static readonly FormularioPortavel RegrasComFaixaDeData = new(
        [new PaginaPortavel("DADOS", null, [
            new ItemPortavel(
                "DATA_NASCIMENTO",
                new RegrasDoCampoDto(
                    null,
                    new ObrigatoriedadeDto("SEMPRE", null),
                    [new RestricaoValorDto("FAIXA_DE_DATA", null, null, null, null, new LimiteDeDataDto("ENVIO", null, -120, 0), null)],
                    null),
                null,
                null),
        ], [])],
        [],
        [],
        []);

    private static RegrasDoCampoDto Sempre(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? exibicao = null) =>
        new(exibicao, new ObrigatoriedadeDto("SEMPRE", null), [], null);
}
