namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class PapelDaBancaCodigosTests
{
    [Fact(DisplayName = "Cada papel de banca tem o código e o rótulo que o contrato publica")]
    public void Declarado_DeCadaPapel_EOQueOContratoPublica() =>
        CatalogoDe<PapelDaBanca>.Valores().ToDictionary(static v => v, static v => (v.Codigo(), v.Rotulo()))
            .Should().Equal(new Dictionary<PapelDaBanca, (string, string)>
            {
                [PapelDaBanca.AvaliaOMerito] = ("AVALIA_O_MERITO", "Avalia o mérito"),
                [PapelDaBanca.JulgaAPrimeiraInstancia] = ("JULGA_A_PRIMEIRA_INSTANCIA", "Julga o recurso em 1ª instância"),
                [PapelDaBanca.ImportaAsNotasDoInep] = ("IMPORTA_AS_NOTAS_DO_INEP", "Importa as notas do INEP"),
                [PapelDaBanca.ImportaCandidatos] = ("IMPORTA_CANDIDATOS", "Importa candidatos"),
                [PapelDaBanca.JulgaASegundaInstancia] = ("JULGA_A_SEGUNDA_INSTANCIA", "Julga o recurso em 2ª instância"),
            });

    [Fact(DisplayName = "O vocabulário de papéis de banca é publicado na ordem de exibição")]
    public void Valores_SaemNaOrdemDeDeclaracao() =>
        CatalogoDe<PapelDaBanca>.Valores().Should().Equal(
            PapelDaBanca.AvaliaOMerito,
            PapelDaBanca.JulgaAPrimeiraInstancia,
            PapelDaBanca.ImportaAsNotasDoInep,
            PapelDaBanca.ImportaCandidatos,
            PapelDaBanca.JulgaASegundaInstancia);

    [Fact(DisplayName = "O catálogo lê o papel pelo código exato, e não reconhece caixa diferente, espaço nem texto ausente")]
    public void TentarObter_SoReconheceOCodigoExato()
    {
        CatalogoDe<PapelDaBanca>.TentarObter("IMPORTA_CANDIDATOS", out PapelDaBanca papel).Should().BeTrue();
        papel.Should().Be(PapelDaBanca.ImportaCandidatos);
        CatalogoDe<PapelDaBanca>.TentarObter("importa_candidatos", out _).Should().BeFalse();
        CatalogoDe<PapelDaBanca>.TentarObter("IMPORTA_CANDIDATOS ", out _).Should().BeFalse();
        CatalogoDe<PapelDaBanca>.TentarObter(null, out _).Should().BeFalse();
        CatalogoDe<PapelDaBanca>.TentarObter(string.Empty, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Os códigos dos papéis de banca passam na conferência da subida")]
    public void ConferirEnumsDe_AssemblyDasRegras_Passa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([typeof(PapelDaBanca).Assembly])).Should().NotThrow();
}
