namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O atendimento pela revogação vinculada, que segue a revogação comum (UNI-REQ-0376 com UNI-REQ-0186).</summary>
public sealed class ProvidenciaAtendimentoTests
{
    private static readonly DateTimeOffset Agora = ProvidenciaBuilder.Agora;
    private static readonly string EmailDeLeandro = ProvidenciaBuilder.Leandro.Email!;

    [Fact(DisplayName = "O atendimento revoga a concessão com o motivo e leva a providência a atendida, no mesmo ato")]
    public void Atender_RevogaEAtende()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();

        p.Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), Declaracao()).IsSuccess.Should().BeTrue();

        (p.Estado, p.DesfechoPor, p.MotivoDoDesfecho).Should().Be((EstadoDaProvidencia.Atendida, ProvidenciaBuilder.Diogo, ProvidenciaBuilder.Motivo));
        (cenario.Concessao.RevogadaEm, cenario.Concessao.MotivoRevogacao, cenario.Concessao.RevogadaPorSub)
            .Should().Be((Agora, ProvidenciaBuilder.Motivo, ProvidenciaBuilder.Diogo.Sub));
    }

    [Fact(DisplayName = "O e-mail do beneficiário confere sem diferenciar maiúsculas nem espaços nas pontas")]
    public void Atender_EmailComCaixaEEspacos_Confere()
    {
        ProvidenciaBuilder cenario = new();

        cenario.Aberta().Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), Declaracao($"  {EmailDeLeandro.ToUpperInvariant()} "))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O e-mail que não é o do beneficiário é recusado com a recusa da revogação comum, e nada muda")]
    public void Atender_EmailDivergente_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();

        Result resultado = p.Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), Declaracao("outra.pessoa@uniplus-test.local"));

        resultado.Errors.Should().ContainSingle().Which.Should().Be(cenario.Concessao.RecusaDaConfirmacao("outra.pessoa@uniplus-test.local"));
        (p.Estado, cenario.Concessao.RevogadaEm).Should().Be((EstadoDaProvidencia.Aberta, (DateTimeOffset?)null));
    }

    [Fact(DisplayName = "A concessão sem e-mail gravado nunca confirma, e o atendimento é recusado")]
    public void Atender_ConcessaoSemEmail_Recusa()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(email: string.Empty));
        Providencia p = cenario.Aberta();

        CodigosDe(p.Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), Declaracao(null))).Should().Equal(ConcessaoErrorCodes.ConfirmacaoDivergente);
    }

    [Fact(DisplayName = "O e-mail divergente e o motivo ausente são recusados juntos, cada um no seu campo")]
    public void Atender_EmailEMotivoInvalidos_Acumulam()
    {
        ProvidenciaBuilder cenario = new();

        Result resultado = cenario.Aberta().Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), new DeclaracaoDoAtendimento(" ", null));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("confirmacaoEmail", ConcessaoErrorCodes.ConfirmacaoDivergente), ("motivo", ProvidenciaErrorCodes.MotivoInvalido));
    }

    [Fact(DisplayName = "A providência já atendida não se atende de novo")]
    public void Atender_JaAtendida_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Atendida();

        CodigosDe(p.Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), Declaracao())).Should().Equal(ProvidenciaErrorCodes.JaAtendida);
    }

    [Fact(DisplayName = "A providência já respondida não se atende")]
    public void Atender_JaRespondida_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Respondida();

        CodigosDe(p.Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), Declaracao())).Should().Equal(ProvidenciaErrorCodes.JaRespondida);
        cenario.Concessao.RevogadaEm.Should().BeNull();
    }

    [Fact(DisplayName = "A concessão revogada por outro caminho recusa o atendimento antes de conferir o e-mail")]
    public void Atender_ConcessaoRevogada_RecusaAntesDoEmail()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();
        cenario.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(-1)), "Revogada pela revogação comum.");

        CodigosDe(p.Atender(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), Declaracao("outra.pessoa@uniplus-test.local")))
            .Should().Equal(ProvidenciaErrorCodes.ConcessaoNaoAtiva);
    }

    [Fact(DisplayName = "Quem solicitou e o beneficiário não atendem, e a concessão segue ativa")]
    public void Atender_Impedidos_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();

        CodigosDe(p.Atender(cenario.Concessao, new(ProvidenciaBuilder.Cecilia.Identidade, Agora), Declaracao())).Should().Equal(ProvidenciaErrorCodes.SolicitanteNaoResponde);
        CodigosDe(p.Atender(cenario.Concessao, new(ProvidenciaBuilder.Leandro.Identidade, Agora), Declaracao())).Should().Equal(ProvidenciaErrorCodes.BeneficiarioNaoResponde);
        cenario.Concessao.RevogadaEm.Should().BeNull();
    }

    private static DeclaracaoDoAtendimento Declaracao(string? confirmacaoEmail = "") =>
        new(ProvidenciaBuilder.Motivo, confirmacaoEmail == string.Empty ? EmailDeLeandro : confirmacaoEmail);

    private static string[] CodigosDe(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];
}
