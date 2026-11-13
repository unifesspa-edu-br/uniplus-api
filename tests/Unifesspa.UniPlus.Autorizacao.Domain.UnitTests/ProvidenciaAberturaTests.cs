namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A abertura da providência pelo Encarregado de dados (UNI-REQ-0375).</summary>
public sealed class ProvidenciaAberturaTests
{
    private static readonly DateTimeOffset Agora = ProvidenciaBuilder.Agora;
    private static readonly PessoaNoToken Cecilia = ProvidenciaBuilder.Cecilia;

    [Fact(DisplayName = "A abertura grava o tipo, o fundamento aparado, quem solicitou e o e-mail verificado em minúsculas")]
    public void Abrir_GravaASolicitacao()
    {
        Concessao concessao = ProvidenciaBuilder.ConcessaoDeLeandro();
        PessoaNoToken comEmailMaiusculo = Cecilia with { Email = "Cecilia_Ester_Moura@Uniplus-Test.Local" };

        Providencia p = Abrir(concessao, new("Retirada ", $"  {ProvidenciaBuilder.Fundamento}  "), comEmailMaiusculo).Value!;

        (p.Estado, p.Tipo, p.Fundamento, p.ConcessaoId).Should().Be((EstadoDaProvidencia.Aberta, TipoDaProvidencia.Retirada, ProvidenciaBuilder.Fundamento, concessao.Id));
        (p.SolicitadaPor, p.SolicitadaPorNome, p.SolicitadaPorEmail).Should().Be((Cecilia.Identidade, Cecilia.Nome, "cecilia_ester_moura@uniplus-test.local"));
        (p.AbertaEm, p.DesfechoEm, p.DesfechoPor, p.MotivoDoDesfecho).Should().Be((Agora, (DateTimeOffset?)null, (Identidade?)null, (string?)null));
    }

    [Fact(DisplayName = "Sem e-mail verificado no token, a abertura é aceita e não grava e-mail de quem solicitou")]
    public void Abrir_SemEmailVerificado_NaoGravaEmail()
    {
        PessoaNoToken naoVerificado = Cecilia with { EmailVerificado = false };

        Abrir(ProvidenciaBuilder.ConcessaoDeLeandro(), Valida(), naoVerificado).Value!.SolicitadaPorEmail.Should().BeNull();
    }

    [Fact(DisplayName = "O tipo aceita retirada e revisão, sem diferenciar maiúsculas")]
    public void Abrir_TiposAceitos()
    {
        Abrir(ProvidenciaBuilder.ConcessaoDeLeandro(), new("REVISAO", ProvidenciaBuilder.Fundamento)).Value!.Tipo.Should().Be(TipoDaProvidencia.Revisao);
        Abrir(ProvidenciaBuilder.ConcessaoDeLeandro(), new("retirada", ProvidenciaBuilder.Fundamento)).Value!.Tipo.Should().Be(TipoDaProvidencia.Retirada);
    }

    [Fact(DisplayName = "Tipo fora do vocabulário e fundamento ausente são recusados juntos, cada um no seu campo")]
    public void Abrir_TipoEFundamentoInvalidos_Acumulam()
    {
        Result<Providencia> resultado = Abrir(ProvidenciaBuilder.ConcessaoDeLeandro(), new("suspensao", "  "));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("tipo", ProvidenciaErrorCodes.TipoInvalido), ("fundamento", ProvidenciaErrorCodes.FundamentoInvalido));
    }

    [Theory(DisplayName = "O fundamento tem de 10 a 2000 caracteres, contados por caractere, e não aceita o caractere nulo")]
    [InlineData(9, "a", false)]
    [InlineData(10, "a", true)]
    [InlineData(2000, "a", true)]
    [InlineData(2001, "a", false)]
    [InlineData(2000, "😀", true)]
    [InlineData(2001, "😀", false)]
    public void Abrir_LimitesDoFundamento(int repeticoes, string caractere, bool aceita)
    {
        string fundamento = string.Concat(Enumerable.Repeat(caractere, repeticoes));

        Abrir(ProvidenciaBuilder.ConcessaoDeLeandro(), new("retirada", fundamento)).IsSuccess.Should().Be(aceita);
    }

    [Fact(DisplayName = "O fundamento com o caractere nulo é recusado")]
    public void Abrir_FundamentoComCaractereNulo_Recusa()
    {
        Abrir(ProvidenciaBuilder.ConcessaoDeLeandro(), new("retirada", $"{ProvidenciaBuilder.Fundamento}\0")).Errors
            .Should().ContainSingle(e => e.Error.Code == ProvidenciaErrorCodes.FundamentoInvalido);
    }

    [Fact(DisplayName = "Concessão revogada recusa a abertura antes de conferir os dados")]
    public void Abrir_ConcessaoRevogada_RecusaAntesDosDados()
    {
        Concessao revogada = ProvidenciaBuilder.ConcessaoDeLeandro();
        revogada.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(-1)), "Mudou de função.");

        CodigosDe(Abrir(revogada, new(null, null))).Should().Equal(ProvidenciaErrorCodes.ConcessaoNaoAtiva);
    }

    [Fact(DisplayName = "Concessão vencida recusa a abertura")]
    public void Abrir_ConcessaoVencida_Recusa()
    {
        Concessao vencida = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddMinutes(-1));

        CodigosDe(Abrir(vencida, Valida())).Should().Equal(ProvidenciaErrorCodes.ConcessaoNaoAtiva);
    }

    [Fact(DisplayName = "Só uma providência fica aberta por concessão, e a segunda é recusada antes dos dados")]
    public void Abrir_JaHaAberta_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia primeira = cenario.Aberta();

        Result<Providencia> segunda = Providencia.Abrir(cenario.Concessao, primeira, new("revisao", null), Cecilia, Agora);

        CodigosDe(segunda).Should().Equal(ProvidenciaErrorCodes.JaHaAberta);
    }

    [Fact(DisplayName = "Depois do desfecho, outra providência pode ser aberta para a mesma concessão ativa")]
    public void Abrir_DepoisDoDesfecho_Aceita()
    {
        ProvidenciaBuilder cenario = new();
        Providencia respondida = cenario.Respondida();

        Providencia.Abrir(cenario.Concessao, respondida, Valida(), Cecilia, Agora).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O Encarregado pode solicitar providência sobre a própria concessão")]
    public void Abrir_SobreAPropriaConcessao_Aceita()
    {
        PessoaNoToken leandro = ProvidenciaBuilder.Leandro;

        Abrir(ProvidenciaBuilder.ConcessaoDeLeandro(), Valida(), leandro).Value!.SolicitadaPor.Should().Be(leandro.Identidade);
    }

    private static Result<Providencia> Abrir(Concessao concessao, SolicitacaoDaProvidencia solicitacao, PessoaNoToken? solicitante = null) =>
        Providencia.Abrir(concessao, null, solicitacao, solicitante ?? Cecilia, Agora);

    private static SolicitacaoDaProvidencia Valida() => new("retirada", ProvidenciaBuilder.Fundamento);

    private static string[] CodigosDe(Result<Providencia> resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];
}
