namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Quantas ocorrências o grupo repetível admite (UNI-REQ-0146).</summary>
public sealed class OcorrenciasDoGrupoTests
{
    [Theory(DisplayName = "O mínimo não é negativo, e o máximo, quando declarado, é ao menos um e não fica abaixo do mínimo")]
    [InlineData(0, 1, true)]
    [InlineData(0, null, true)]
    [InlineData(1, null, true)]
    [InlineData(2, 2, true)]
    [InlineData(-1, 3, false)]
    [InlineData(-1, null, false)]
    [InlineData(0, 0, false)]
    [InlineData(3, 2, false)]
    public void Valida_MinimoEMaximo_ConformeARegra(int minimo, int? maximo, bool esperada) =>
        new OcorrenciasDoGrupo(minimo, maximo).Valida.Should().Be(esperada);

    [Theory(DisplayName = "A recusa aponta o mínimo quando ele é negativo e o máximo quando só o máximo destoa")]
    [InlineData(-1, 3, "minimo")]
    [InlineData(-1, null, "minimo")]
    [InlineData(0, 0, "maximo")]
    [InlineData(3, 2, "maximo")]
    public void Conferir_Incoerente_RecusaNoCampoQueDestoa(int minimo, int? maximo, string campo)
    {
        List<FieldError> erros = [.. new OcorrenciasDoGrupo(minimo, maximo).Conferir()];

        FieldError erro = erros.Should().ContainSingle().Subject;
        erro.Field.Should().Be(campo);
        erro.Error.Code.Should().Be(GrupoFormularioErrorCodes.ContagemIncoerente);
    }

    [Theory(DisplayName = "Mínimo e máximo coerentes não são recusados")]
    [InlineData(0, null)]
    [InlineData(1, 1)]
    [InlineData(0, 5)]
    public void Conferir_Coerente_NaoRecusa(int minimo, int? maximo) =>
        new OcorrenciasDoGrupo(minimo, maximo).Conferir().Should().BeEmpty();

    [Fact(DisplayName = "O grupo que não inclui o candidato não tem a identificação dele conferida, mesmo sem parentesco")]
    public void ConferirCandidatoComoMembro_SemCandidato_NaoRecusa() =>
        new OcorrenciasDoGrupo(0, 5).ConferirCandidatoComoMembro([("RENDA", false, Obrigatoriedade.Nunca, [])]).Should().BeEmpty();

    [Fact(DisplayName = "O grupo que inclui o candidato recusa a falta do parentesco que o identifica")]
    public void ConferirCandidatoComoMembro_ComCandidatoSemParentesco_Recusa()
    {
        List<FieldError> erros = [.. new OcorrenciasDoGrupo(1, 5, IncluiCandidato: true).ConferirCandidatoComoMembro([("RENDA", false, Obrigatoriedade.Sempre, [])])];

        FieldError erro = erros.Should().ContainSingle().Subject;
        erro.Field.Should().Be("subitens");
        erro.Error.Code.Should().Be(GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto);
    }
}
