namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ConcessaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Email = "leandro.viana@uniplus-test.local";

    private static readonly DateTimeOffset Agora = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Leandro = new(Emissor, "leandro.viana");
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly Identidade Isabella = new(Emissor, "isabella.figueiredo");
    private static readonly OrigemDaConcessao Origem = new(TipoDeOrigem.Pedido, Guid.NewGuid());

    private static readonly FakeCatalogo Catalogo = new();

    [Fact(DisplayName = "Perfil comum concedido numa unidade nasce ativo e grava quem concedeu")]
    public void Conceder_PerfilComumNaUnidade_NasceAtiva()
    {
        Guid unidade = Guid.NewGuid();

        Result<Concessao> resultado = Concessao.Conceder(
            Leandro, PerfilComum(), EscopoDaConcessao.DaUnidade(unidade), Origem, Diogo, Agora);

        resultado.IsSuccess.Should().BeTrue();
        Concessao concessao = resultado.Value!;
        concessao.EstaAtiva(Agora).Should().BeTrue();
        concessao.UnidadeId.Should().Be(unidade);
        concessao.ConcedidaPorSub.Should().Be(Diogo.Sub);
        concessao.ConcedidaEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "Perfil inativo não aceita concessão nova")]
    public void Conceder_PerfilInativo_Recusa()
    {
        Perfil perfil = PerfilComum();
        perfil.Desativar();

        CodigosDaRecusa(Concessao.Conceder(
            Leandro, perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Origem, Diogo, Agora))
            .Should().Equal(ConcessaoErrorCodes.PerfilInativo);
    }

    [Fact(DisplayName = "Perfil de governança com escopo de unidade é recusado")]
    public void Conceder_GovernancaNaUnidade_Recusa() =>
        CodigosDaRecusa(Concessao.Conceder(
            Isabella, PerfilDeGovernanca(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Origem, Diogo, Agora))
            .Should().Equal(ConcessaoErrorCodes.EscopoInvalido);

    [Fact(DisplayName = "Perfil de governança aceita o escopo da instituição")]
    public void Conceder_GovernancaNaInstituicao_Aceita() =>
        Concessao.Conceder(
            Isabella, PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), Origem, Diogo, Agora)
            .IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Perfil comum com escopo da instituição é recusado")]
    public void Conceder_PerfilComumNaInstituicao_Recusa() =>
        CodigosDaRecusa(Concessao.Conceder(
            Leandro, PerfilComum(), EscopoDaConcessao.DaInstituicao(), Origem, Diogo, Agora))
            .Should().Equal(ConcessaoErrorCodes.EscopoInvalido);

    [Fact(DisplayName = "Escopo de unidade sem unidade e escopo da instituição com unidade são incoerentes")]
    public void Conceder_EscopoIncoerente_Recusa()
    {
        CodigosDaRecusa(Concessao.Conceder(
            Leandro, PerfilComum(), new EscopoDaConcessao(TipoDeEscopo.Unidade, null), Origem, Diogo, Agora))
            .Should().Equal(ConcessaoErrorCodes.EscopoInvalido);
        CodigosDaRecusa(Concessao.Conceder(
            Isabella, PerfilDeGovernanca(), new EscopoDaConcessao(TipoDeEscopo.Instituicao, Guid.NewGuid()), Origem, Diogo, Agora))
            .Should().Equal(ConcessaoErrorCodes.EscopoInvalido);
    }

    [Theory(DisplayName = "Validade que não é posterior ao instante da concessão é recusada")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Conceder_ValidadeNaoPosterior_Recusa(int minutos) =>
        CodigosDaRecusa(Concessao.Conceder(
            Leandro, PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Origem, Diogo, Agora, Agora.AddMinutes(minutos)))
            .Should().Equal(ConcessaoErrorCodes.ValidadeInvalida);

    [Fact(DisplayName = "Perfil de governança não aceita concessão com prazo")]
    public void Conceder_GovernancaComValidade_Recusa()
    {
        Result<Concessao> resultado = Concessao.Conceder(
            Isabella, PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), Origem, Diogo, Agora, Agora.AddDays(30));

        CodigosDaRecusa(resultado).Should().Equal(ConcessaoErrorCodes.ValidadeInvalida);
        resultado.Errors.Single().Error.Message.Should().Contain("governança");
    }

    [Fact(DisplayName = "Violações independentes da concessão se acumulam")]
    public void Conceder_VariasViolacoes_AcumulaTodas()
    {
        Perfil perfil = PerfilComum();
        perfil.Desativar();

        CodigosDaRecusa(Concessao.Conceder(
            Leandro, perfil, EscopoDaConcessao.DaInstituicao(), Origem, Diogo, Agora, Agora))
            .Should().BeEquivalentTo(
                ConcessaoErrorCodes.PerfilInativo, ConcessaoErrorCodes.EscopoInvalido, ConcessaoErrorCodes.ValidadeInvalida);
    }

    [Fact(DisplayName = "Concessão com vigência vencida deixa de estar ativa sem ser revogada")]
    public void EstaAtiva_VigenciaVencida_DeixaDeEstar()
    {
        Concessao concessao = Concedida(validaAte: Agora.AddDays(1));

        concessao.EstaAtiva(Agora).Should().BeTrue();
        concessao.EstaAtiva(Agora.AddDays(1)).Should().BeFalse();
        concessao.RevogadaEm.Should().BeNull();
    }

    [Fact(DisplayName = "Revogar grava autor, instante e motivo e encerra a concessão")]
    public void Revogar_Valido_GravaAutorInstanteEMotivo()
    {
        Concessao concessao = Concedida();
        DateTimeOffset depois = Agora.AddHours(2);

        Result resultado = concessao.Revogar(Diogo, "  Mudou de função.  ", depois);

        resultado.IsSuccess.Should().BeTrue();
        concessao.EstaAtiva(depois).Should().BeFalse();
        concessao.RevogadaEm.Should().Be(depois);
        concessao.RevogadaPorSub.Should().Be(Diogo.Sub);
        concessao.RevogadaPorEmissor.Should().Be(Emissor);
        concessao.MotivoRevogacao.Should().Be("Mudou de função.");
    }

    [Fact(DisplayName = "Revogar concessão já revogada é recusado e preserva a primeira revogação")]
    public void Revogar_JaRevogada_Recusa()
    {
        Concessao concessao = Concedida();
        concessao.Revogar(Diogo, "Primeiro motivo.", Agora.AddHours(1));

        Result resultado = concessao.Revogar(Isabella, "Segundo motivo.", Agora.AddHours(2));

        resultado.Errors.Single().Error.Code.Should().Be(ConcessaoErrorCodes.JaRevogada);
        concessao.RevogadaPorSub.Should().Be(Diogo.Sub);
        concessao.MotivoRevogacao.Should().Be("Primeiro motivo.");
    }

    [Theory(DisplayName = "Motivo ausente, em branco, longo ou com caractere nulo é recusado e não revoga")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Mot\0ivo")]
    public void Revogar_MotivoInvalido_Recusa(string? motivo)
    {
        Concessao concessao = Concedida();

        CodigosDaRecusa(concessao.Revogar(Diogo, motivo, Agora)).Should().Equal(ConcessaoErrorCodes.MotivoInvalido);
        concessao.RevogadaEm.Should().BeNull();
    }

    [Fact(DisplayName = "Motivo com mais de 500 caracteres é recusado")]
    public void Revogar_MotivoLongo_Recusa() =>
        CodigosDaRecusa(Concedida().Revogar(Diogo, new string('a', 501), Agora))
            .Should().Equal(ConcessaoErrorCodes.MotivoInvalido);

    [Theory(DisplayName = "O e-mail do beneficiário confirma sem diferenciar maiúsculas nem espaços nas pontas")]
    [InlineData("leandro.viana@uniplus-test.local", true)]
    [InlineData("  Leandro.Viana@UNIPLUS-test.local ", true)]
    [InlineData("isabella.figueiredo@uniplus-test.local", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ConfirmaEmailDoBeneficiario_ComparaSemCaixaNemEspacos(string? informado, bool esperado) =>
        Concedida().ConfirmaEmailDoBeneficiario(informado).Should().Be(esperado);

    [Fact(DisplayName = "Concessão sem e-mail gravado nunca confirma: não há com o que conferir")]
    public void ConfirmaEmailDoBeneficiario_SemEmailGravado_NaoConfirma()
    {
        Concessao antiga = Concessao.Conceder(
            Leandro, PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Origem, Diogo, Agora).Value!;

        antiga.ConfirmaEmailDoBeneficiario(Email).Should().BeFalse();
    }

    private static Concessao Concedida(DateTimeOffset? validaAte = null) =>
        Concessao.Conceder(
            Leandro, PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Origem, Diogo, Agora, validaAte, Email).Value!;

    private static Perfil PerfilComum() =>
        Perfil.Criar("Analista", "Descrição válida.", ["selecao:processos:consultar"], Catalogo).Value!;

    private static Perfil PerfilDeGovernanca()
    {
        Perfil perfil = PerfilComum();
        typeof(Perfil).GetProperty(nameof(Perfil.Governanca))!.SetValue(perfil, TipoDeGovernanca.GestorDeAcessos);
        return perfil;
    }

    private static string[] CodigosDaRecusa(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private static string[] CodigosDaRecusa<T>(Result<T> resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;
    }
}
