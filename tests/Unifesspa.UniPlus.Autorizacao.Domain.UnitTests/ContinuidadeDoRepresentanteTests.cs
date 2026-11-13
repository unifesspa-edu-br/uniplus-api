namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Depois da primeira designação, a revogação não deixa a instituição sem Representante do controlador vigente
/// (UNI-REQ-0394). Só a designação vigente da instituição reduz o papel, e a concessão anterior à regra, com prazo ainda
/// não vencido, conta como vigente. Isabella é a Representante; Leandro, o outro beneficiário.
/// </summary>
public sealed class ContinuidadeDoRepresentanteTests : PedidoDeConcessaoTestBase
{
    [Fact(DisplayName = "A designação vigente de Representante na instituição reduz o papel quando revogada")]
    public void EDesignacaoVigente_RepresentanteAtivo() =>
        ContinuidadeDoRepresentante.EDesignacaoVigente(Designacao(), Agora).Should().BeTrue();

    [Fact(DisplayName = "A designação já revogada não reduz o papel")]
    public void EDesignacaoVigente_Revogada_NaoConta()
    {
        Concessao revogada = Designacao();
        revogada.Revogar(new AtoDoAgente(Diogo.Identidade, Agora), "Deixou a função.").IsSuccess.Should().BeTrue();

        ContinuidadeDoRepresentante.EDesignacaoVigente(revogada, Agora.AddMinutes(1)).Should().BeFalse();
    }

    [Fact(DisplayName = "A designação anterior à regra conta enquanto o prazo não vence, e a vencida não conta")]
    public void EDesignacaoVigente_ComPrazoAnteriorARegra_ContaAteVencer()
    {
        Concessao comPrazo = DesignacaoAnteriorARegra(validaAte: Agora.AddDays(1));

        ContinuidadeDoRepresentante.EDesignacaoVigente(comPrazo, Agora).Should().BeTrue();
        ContinuidadeDoRepresentante.EDesignacaoVigente(comPrazo, Agora.AddDays(1)).Should().BeFalse();
    }

    [Theory(DisplayName = "A concessão de outro perfil não reduz o papel de Representante")]
    [InlineData(TipoDeGovernanca.GestorDeAcessos)]
    [InlineData(TipoDeGovernanca.EncarregadoDeDados)]
    public void EDesignacaoVigente_OutroPerfil_NaoConta(TipoDeGovernanca governanca)
    {
        Concessao outra = ConcessaoBuilder.DoPerfil(PerfilDeGovernanca(governanca), Catalogo).Para(Isabella.Identidade).Em(Agora).Construir();

        ContinuidadeDoRepresentante.EDesignacaoVigente(outra, Agora).Should().BeFalse();
    }

    [Theory(DisplayName = "A revogação da designação vigente só deixa sem Representante quando ninguém mais é vigente")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RevogacaoDeixariaSemRepresentante_PelaOutraPessoaVigente(bool haOutraPessoaVigente, bool deixaria) =>
        ContinuidadeDoRepresentante.RevogacaoDeixariaSemRepresentante(Designacao(), Agora, haOutraPessoaVigente).Should().Be(deixaria);

    [Fact(DisplayName = "Revogar uma designação que já não é vigente nunca deixa sem Representante")]
    public void RevogacaoDeixariaSemRepresentante_DesignacaoVencida_NaoDeixa() =>
        ContinuidadeDoRepresentante
            .RevogacaoDeixariaSemRepresentante(DesignacaoAnteriorARegra(Agora.AddHours(1)), Agora.AddHours(2), haOutraPessoaVigente: false)
            .Should().BeFalse();

    [Fact(DisplayName = "A conferência recusa a concessão nula")]
    public void EDesignacaoVigente_ConcessaoNula_Lanca() =>
        FluentActions.Invoking(() => ContinuidadeDoRepresentante.EDesignacaoVigente(null!, Agora)).Should().Throw<ArgumentNullException>();

    private static Concessao Designacao() =>
        ConcessaoBuilder.DoPerfil(Representante(), Catalogo).Para(Isabella.Identidade).Em(Agora).Construir();

    // O domínio já não concede Representante com prazo. A designação anterior à regra nasce de um perfil que admite
    // prazo e passa ao perfil de Representante, como ficou gravada na base de antes da mudança.
    private static Concessao DesignacaoAnteriorARegra(DateTimeOffset validaAte)
    {
        Concessao concessao = ConcessaoBuilder.DoPerfil(PerfilDeGovernanca(TipoDeGovernanca.EncarregadoDeDados), Catalogo)
            .Para(Isabella.Identidade).Em(Agora.AddDays(-1)).ValidaAte(validaAte).Construir();
        typeof(Concessao).GetProperty(nameof(Concessao.PerfilId))!.SetValue(concessao, PerfisDeSistema.RepresentanteDoControladorId);
        return concessao;
    }

    private static Perfil Representante()
    {
        Perfil perfil = PerfilDeGovernanca(TipoDeGovernanca.RepresentanteDoControlador);
        typeof(Perfil).GetProperty(nameof(Perfil.Id))!.SetValue(perfil, PerfisDeSistema.RepresentanteDoControladorId);
        return perfil;
    }
}
