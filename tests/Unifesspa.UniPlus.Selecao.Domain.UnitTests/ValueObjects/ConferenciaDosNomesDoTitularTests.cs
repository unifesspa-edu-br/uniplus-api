namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConferenciaDosNomesDoTitularTests
{
    private static RespostasAdmitidas Admitidas => new(
        new Dictionary<string, DefinicaoDaResposta> { [ConjuntoBasicoDaInscricao.NomeSocial] = new(TipoDoValorDeResposta.Texto, false, false) },
        new HashSet<string>(), new Dictionary<string, IReadOnlySet<string>>(), new HashSet<Guid>(),
        new Dictionary<string, TermoAdmitido>());

    private static Dictionary<string, JsonElement> Respostas(string? nome) => new()
    {
        [ConjuntoBasicoDaInscricao.DesejaNomeSocial] = JsonSerializer.SerializeToElement(true),
        [ConjuntoBasicoDaInscricao.NomeSocial] = JsonSerializer.SerializeToElement(nome),
    };

    [Fact]
    public void RecusasDoNomeSocial_SemNomeNaContaEDigitado_RecusaSomenteAoEnviar()
    {
        Dictionary<string, JsonElement> respostas = Respostas(null);
        ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoSalvar(respostas, Admitidas).Should().BeEmpty();
        ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoEnviar(respostas, Admitidas).Should().ContainSingle()
            .Which.Error.Code.Should().Be(InscricaoErrorCodes.NomeSocialAusenteNaConta);
    }

    [Fact]
    public void RecusasDoNomeSocial_NomeDiferenteDaConta_RecusaAoSalvarEEnviar()
    {
        Dictionary<string, JsonElement> respostas = Respostas("Nome diferente");
        RespostasAdmitidas admitidas = Admitidas with { NomeSocialDoLogin = "Nome da conta" };
        ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoSalvar(respostas, admitidas).Should().ContainSingle()
            .Which.Error.Code.Should().Be(InscricaoErrorCodes.NomeSocialDiferenteDaConta);
        ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoEnviar(respostas, admitidas).Should().ContainSingle()
            .Which.Error.Code.Should().Be(InscricaoErrorCodes.NomeSocialDiferenteDaConta);
    }

    [Fact]
    public void RecusasDoNomeSocial_NomeComEspacosExtras_Aceita()
    {
        Dictionary<string, JsonElement> respostas = Respostas(" Nome   da conta ");
        RespostasAdmitidas admitidas = Admitidas with { NomeSocialDoLogin = "Nome da conta" };
        ConferenciaDosNomesDoTitular.RecusasDoNomeSocialAoEnviar(respostas, admitidas).Should().BeEmpty();
    }
}
