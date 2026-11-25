namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ObrigatoriedadeLegalMappingTests
{
    [Fact]
    public void ToDto_TodosOsTipos_UsaRotuloReservado()
    {
        ObrigatoriedadeLegalDto resultado = ObrigatoriedadeLegalMapping.ToDto(Regra(TodosOsTiposDeProcesso.Codigo), new Dictionary<string, string>());

        resultado.TipoProcesso.Codigo.Should().Be(TodosOsTiposDeProcesso.Codigo);
        resultado.TipoProcesso.Rotulo.Should().Be(TodosOsTiposDeProcesso.Rotulo);
    }

    [Fact]
    public void ToDto_TipoCadastrado_UsaNomeDoDicionario()
    {
        ObrigatoriedadeLegalDto resultado = ObrigatoriedadeLegalMapping.ToDto(Regra("TIPO_A"), new Dictionary<string, string> { ["TIPO_A"] = "Tipo A" });

        resultado.TipoProcesso.Codigo.Should().Be("TIPO_A");
        resultado.TipoProcesso.Rotulo.Should().Be("Tipo A");
    }

    [Fact]
    public void ToDto_TipoAusente_LancaExcecao()
    {
        Action mapear = () => ObrigatoriedadeLegalMapping.ToDto(Regra("TIPO_A"), new Dictionary<string, string>());

        mapear.Should().Throw<KeyNotFoundException>();
    }

    private static ObrigatoriedadeLegal Regra(string codigo) => ObrigatoriedadeLegal.Criar(
        codigo, CategoriaObrigatoriedade.Outros, "REGRA", new ConcorrenciaDuplaObrigatoria(),
        new TextoNormativo("Descrição", "Lei", null, null), new DateOnly(2026, 1, 1), null).Value!;
}
