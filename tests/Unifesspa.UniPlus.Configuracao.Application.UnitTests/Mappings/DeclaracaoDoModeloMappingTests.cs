namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.ConteudosDeModeloParaTeste;

/// <summary>A declaração do modelo nas duas leituras: a da manutenção e a do leitor entre módulos.</summary>
public sealed class DeclaracaoDoModeloMappingTests
{
    [Theory(DisplayName = "A leitura da manutenção traz a declaração do modelo, e o JSON a publica com os nomes do contrato")]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ToDto_TrazADeclaracao(bool serveAFaseDeInscricao, bool obrigatorio)
    {
        ModeloFormulario modelo = ModeloDeOutraFase(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio));

        ModeloFormularioDto dto = modelo.ToDto();

        dto.Declaracao.Should().Be(new DeclaracaoDoModeloDto(serveAFaseDeInscricao, obrigatorio));
        JsonSerializer.Serialize(dto.Declaracao, JsonSerializerOptions.Web).Should().Be(
            $"{{\"serveAFaseDeInscricao\":{(serveAFaseDeInscricao ? "true" : "false")},\"obrigatorio\":{(obrigatorio ? "true" : "false")}}}");
    }

    [Theory(DisplayName = "O leitor entre módulos entrega a declaração do modelo")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ToView_EntregaADeclaracao(bool serveAFaseDeInscricao, bool obrigatorio)
    {
        ModeloFormulario modelo = ModeloDeOutraFase(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio));

        ModeloFormularioView view = modelo.ToView();

        view.Declaracao.Should().Be(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio));
    }

    private static ModeloFormulario ModeloDeOutraFase(DeclaracaoDoFormulario declaracao)
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "MODELO_DE_TESTE", "Modelo de teste", null, declaracao, null,
            new ConteudoDoModelo("Modelo", [Secao, Revisao], [], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        return modelo.Value ?? throw new InvalidOperationException(string.Join("; ", modelo.Errors.Select(static e => e.Error.Code)));
    }
}
