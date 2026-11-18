namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class ConcessaoPendenteDeMigrarTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly Identidade Isabella = CenarioDeMigracao.Isabella;
    private static readonly DateTimeOffset Depois = CenarioDeMigracao.Abertura.AddHours(3);

    [Fact(DisplayName = "A concessão ativa fora da versão-alvo está pendente de migrar")]
    public void PendenteDeMigrarPara_AtivaForaDaVersao_EstaPendente()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao ativa = cenario.Conceder(Leandro);
        cenario.Abrir();

        ativa.PendenteDeMigrarPara(cenario.VersaoNova!.Id, Depois).Should().BeTrue();
    }

    [Fact(DisplayName = "A concessão que já está na versão-alvo não está pendente de migrar")]
    public void PendenteDeMigrarPara_JaNaVersao_NaoEstaPendente()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao migrada = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        migrada.MigrarParaVersao(cenario.VersaoAntiga, cenario.VersaoNova!);

        migrada.PendenteDeMigrarPara(revisao.VersaoAlvoId, Depois).Should().BeFalse();
    }

    [Fact(DisplayName = "A concessão revogada não está pendente de migrar")]
    public void PendenteDeMigrarPara_Revogada_NaoEstaPendente()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao revogada = cenario.Conceder(Leandro);
        revogada.Revogar(new AtoDoAgente(Diogo, CenarioDeMigracao.Abertura.AddMinutes(-1)), "Teste de revogação.").IsSuccess.Should().BeTrue();
        cenario.Abrir();

        revogada.PendenteDeMigrarPara(cenario.VersaoNova!.Id, Depois).Should().BeFalse();
    }

    [Fact(DisplayName = "A concessão vencida no instante não está pendente de migrar")]
    public void PendenteDeMigrarPara_Vencida_NaoEstaPendente()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao vencida = cenario.Conceder(Leandro, validaAte: CenarioDeMigracao.Abertura.AddHours(2));
        cenario.Abrir();

        vencida.PendenteDeMigrarPara(cenario.VersaoNova!.Id, CenarioDeMigracao.Abertura.AddHours(1)).Should().BeTrue();
        vencida.PendenteDeMigrarPara(cenario.VersaoNova.Id, Depois).Should().BeFalse();
    }

    [Fact(DisplayName = "A elegibilidade da revisão é a pendência da concessão mais o perfil da revisão")]
    public void Elegivel_ConcessaoDeOutroPerfil_PendenteMasNaoElegivel()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao doPerfil = cenario.Conceder(Leandro);
        var outro = new CenarioDeMigracao(protegido: false);
        Concessao deOutroPerfil = outro.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.Elegivel(doPerfil, Depois).Should().Be(doPerfil.PendenteDeMigrarPara(revisao.VersaoAlvoId, Depois)).And.BeTrue();
        deOutroPerfil.PendenteDeMigrarPara(revisao.VersaoAlvoId, Depois).Should().BeTrue();
        revisao.Elegivel(deOutroPerfil, Depois).Should().BeFalse();
    }
}
