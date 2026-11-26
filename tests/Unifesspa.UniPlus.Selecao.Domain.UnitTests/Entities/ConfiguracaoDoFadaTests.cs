namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConfiguracaoDoFadaTests
{
    private static readonly Guid BaseLegalId = Guid.CreateVersion7();
    private static readonly (string CodigoIbge, string Nome, string Uf)[] MunicipioValido = [("1504208", "Marabá", "PA")];

    private static ReferenciaRegra RegraMultiplicativo() =>
        ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", new string('a', 64));

    private static Result<ConfiguracaoDoFada> Criar(
        decimal fator = 1.20m,
        decimal? teto = null,
        ReferenciaRegra? regra = null,
        IEnumerable<(string CodigoIbge, string Nome, string Uf)>? municipios = null,
        string? fatoElegibilidade = null) =>
        ConfiguracaoDoFada.Criar(regra ?? RegraMultiplicativo(), fator, teto, fatoElegibilidade, new BaseLegalCongelada(BaseLegalId, "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [.. municipios ?? MunicipioValido]));

    [Fact(DisplayName = "Criar com fator válido e sem teto tem sucesso (P.O.: ×1,20 sem teto)")]
    public void Criar_SemTeto_Sucesso()
    {
        Result<ConfiguracaoDoFada> resultado = Criar();

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Teto.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com teto informado tem sucesso")]
    public void Criar_ComTeto_Sucesso()
    {
        Result<ConfiguracaoDoFada> resultado = Criar(teto: 10m);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Teto.Should().Be(10m);
    }

    [Theory(DisplayName = "O fato de elegibilidade, quando citado, segue o formato do cadastro e tem até 50 caracteres")]
    [InlineData("   ")]
    [InlineData(" FATO ")]
    [InlineData("fato")]
    [InlineData("FATO__INVALIDO")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Criar_FatoDeElegibilidadeMalFormado_Falha(string fato)
    {
        Result<ConfiguracaoDoFada> resultado = Criar(fatoElegibilidade: fato);

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoDoFada.FatoElegibilidadeInvalido");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Criar_FatoDeElegibilidadeNoFormato_Aceita(string fato)
    {
        Result<ConfiguracaoDoFada> resultado = Criar(fatoElegibilidade: fato);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.FatoElegibilidadeCodigo.Should().Be(fato);
    }

    [Fact(DisplayName = "Criar congela o snapshot da Base Legal (Id, código, tipo, identificação, descrição e municípios)")]
    public void Criar_CongelaSnapshotDaBaseLegal()
    {
        Result<ConfiguracaoDoFada> resultado = Criar();

        resultado.IsSuccess.Should().BeTrue();
        ConfiguracaoDoFada fada = resultado.Value!;
        fada.BaseLegalId.Should().Be(BaseLegalId);
        fada.BaseLegalCodigo.Should().Be("PORTARIA_UNIFESSPA_NO_2514_2023");
        fada.TipoInstrumento.Should().Be("PORTARIA");
        fada.Identificacao.Should().Be("Portaria Unifesspa nº 2514/2023");
        fada.Descricao.Should().Be("Institui inclusão regional");
        fada.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1504208" && m.Nome == "Marabá" && m.Uf == "PA");
    }

    [Fact(DisplayName = "Criar com regra de código diferente de FADA_MULTIPLICATIVO falha")]
    public void Criar_RegraInvalida_Falha()
    {
        ReferenciaRegra regraErrada = ReferenciaRegra.De("FORMULA_MEDIA_PONDERADA", "v1", new string('b', 64));

        Result<ConfiguracaoDoFada> resultado = Criar(regra: regraErrada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDoFada.RegraInvalida");
    }

    [Theory(DisplayName = "Criar com fator não positivo falha")]
    [InlineData(0)]
    [InlineData(-1.2)]
    public void Criar_FatorInvalido_Falha(double fator)
    {
        Result<ConfiguracaoDoFada> resultado = Criar(fator: (decimal)fator);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDoFada.FatorInvalido");
    }

    [Fact(DisplayName = "Criar com teto não positivo falha")]
    public void Criar_TetoInvalido_Falha()
    {
        Result<ConfiguracaoDoFada> resultado = Criar(teto: 0m);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDoFada.TetoInvalido");
    }

    [Fact(DisplayName = "Criar sem nenhum município falha — defesa contra envelope adulterado com municipios:[]")]
    public void Criar_SemMunicipios_Falha()
    {
        Result<ConfiguracaoDoFada> resultado = Criar(municipios: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDoFada.SemMunicipios");
    }

    [Fact(DisplayName = "ADR-0125: violações independentes acumulam num único lote")]
    public void Criar_RegraEFatorETetoInvalidos_AcumulaAsTresViolacoes()
    {
        ReferenciaRegra regraErrada = ReferenciaRegra.De("FORMULA_MEDIA_PONDERADA", "v1", new string('b', 64));

        Result<ConfiguracaoDoFada> resultado = Criar(fator: 0m, teto: 0m, regra: regraErrada);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoDoFada.RegraInvalida",
            "ConfiguracaoDoFada.FatorInvalido",
            "ConfiguracaoDoFada.TetoInvalido",
        ]);
    }
}
