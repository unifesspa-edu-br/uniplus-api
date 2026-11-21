namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Xunit;

/// <summary>Os papéis de banca que julgam recurso: só o de 1ª e o de 2ª instância.</summary>
public sealed class PapeisQueJulgamRecursoTests
{
    [Theory(DisplayName = "Julgam recurso o papel de 1ª instância e o de 2ª instância, e nenhum outro")]
    [InlineData(PapelDaBanca.AvaliaOMerito, false)]
    [InlineData(PapelDaBanca.JulgaAPrimeiraInstancia, true)]
    [InlineData(PapelDaBanca.ImportaAsNotasDoInep, false)]
    [InlineData(PapelDaBanca.ImportaCandidatos, false)]
    [InlineData(PapelDaBanca.JulgaASegundaInstancia, true)]
    public void Contem_SoOsPapeisQueJulgamRecurso(PapelDaBanca papel, bool julgaRecurso) =>
        PapeisQueJulgamRecurso.Contem(papel).Should().Be(julgaRecurso);
}
