namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A resposta motivada da providência e a ordem das recusas (UNI-REQ-0376).</summary>
public sealed class ProvidenciaRespostaTests
{
    private static readonly DateTimeOffset Agora = ProvidenciaBuilder.Agora;

    [Fact(DisplayName = "A resposta motivada leva a providência a respondida, com quem respondeu, quando e o motivo aparado")]
    public void Responder_GravaODesfecho()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();

        p.Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(Agora.AddHours(1)), $" {ProvidenciaBuilder.Motivo} ").IsSuccess.Should().BeTrue();

        (p.Estado, p.DesfechoEm, p.DesfechoPor, p.DesfechoPorNome, p.MotivoDoDesfecho).Should().Be(
            (EstadoDaProvidencia.Respondida, Agora.AddHours(1), ProvidenciaBuilder.Diogo, ProvidenciaBuilder.Diogo.Nome, ProvidenciaBuilder.Motivo));
        cenario.Concessao.EstaAtiva(Agora.AddHours(1)).Should().BeTrue("a resposta não revoga");
    }

    [Fact(DisplayName = "A segunda resposta é recusada por providência já respondida")]
    public void Responder_JaRespondida_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Respondida();

        CodigosDe(p.Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), "Outro motivo.")).Should().Equal(ProvidenciaErrorCodes.JaRespondida);
        p.MotivoDoDesfecho.Should().Be(ProvidenciaBuilder.Motivo);
    }

    [Fact(DisplayName = "A resposta da providência atendida é recusada por providência já atendida")]
    public void Responder_JaAtendida_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Atendida();

        CodigosDe(p.Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), ProvidenciaBuilder.Motivo)).Should().Equal(ProvidenciaErrorCodes.JaAtendida);
    }

    [Fact(DisplayName = "A revogação por outro caminho antes da resposta faz a resposta ser recusada e nada é gravado")]
    public void Responder_ConcessaoRevogadaPorOutroCaminho_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();
        cenario.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(5)), "Revogada pela revogação comum.");

        CodigosDe(p.Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(10)), ProvidenciaBuilder.Motivo))
            .Should().Equal(ProvidenciaErrorCodes.ConcessaoNaoAtiva);
        (p.Estado, p.DesfechoEm).Should().Be((EstadoDaProvidencia.Aberta, (DateTimeOffset?)null));
    }

    [Fact(DisplayName = "O vencimento da concessão antes do instante do ato faz a resposta ser recusada")]
    public void Responder_ConcessaoVencidaNoInstanteDoAto_Recusa()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1)));
        Providencia p = cenario.Aberta();

        CodigosDe(p.Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(Agora.AddHours(1)), ProvidenciaBuilder.Motivo))
            .Should().Equal(ProvidenciaErrorCodes.ConcessaoNaoAtiva);
    }

    [Fact(DisplayName = "O estado é conferido antes da concessão: a respondida cuja concessão venceu continua recusada por já respondida")]
    public void Responder_EstadoAntesDaConcessao()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1)));
        Providencia p = cenario.Respondida();

        CodigosDe(p.Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(Agora.AddHours(2)), ProvidenciaBuilder.Motivo))
            .Should().Equal(ProvidenciaErrorCodes.JaRespondida);
    }

    [Fact(DisplayName = "Quem solicitou não responde, e a recusa sai antes da do motivo")]
    public void Responder_PorQuemSolicitou_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();

        CodigosDe(p.Responder(cenario.Concessao, new AtoDoAgente(ProvidenciaBuilder.Cecilia.Identidade, Agora), null))
            .Should().Equal(ProvidenciaErrorCodes.SolicitanteNaoResponde);
    }

    [Fact(DisplayName = "O beneficiário da concessão não responde a providência sobre ela")]
    public void Responder_PeloBeneficiario_Recusa()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();

        CodigosDe(p.Responder(cenario.Concessao, new AtoDoAgente(ProvidenciaBuilder.Leandro.Identidade, Agora), ProvidenciaBuilder.Motivo))
            .Should().Equal(ProvidenciaErrorCodes.BeneficiarioNaoResponde);
    }

    [Theory(DisplayName = "O motivo é obrigatório, tem até 500 caracteres e não aceita o caractere nulo")]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("motivo\0", false)]
    [InlineData("x", true)]
    public void Responder_Motivo(string? motivo, bool aceita)
    {
        ProvidenciaBuilder cenario = new();

        Result resultado = cenario.Aberta().Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), motivo);

        (resultado.IsSuccess, resultado.Errors.Select(e => e.Field).FirstOrDefault()).Should().Be((aceita, aceita ? null : "motivo"));
    }

    [Theory(DisplayName = "O limite do motivo conta caracteres, não unidades UTF-16")]
    [InlineData(500, "a", true)]
    [InlineData(501, "a", false)]
    [InlineData(500, "😀", true)]
    public void Responder_LimiteDoMotivo(int repeticoes, string caractere, bool aceita)
    {
        ProvidenciaBuilder cenario = new();
        string motivo = string.Concat(Enumerable.Repeat(caractere, repeticoes));

        cenario.Aberta().Responder(cenario.Concessao, ProvidenciaBuilder.AtoDeDiogo(), motivo).IsSuccess.Should().Be(aceita);
    }

    [Fact(DisplayName = "A concessão que não é a da providência é defeito de quem chama")]
    public void Responder_OutraConcessao_Lanca()
    {
        Providencia p = new ProvidenciaBuilder().Aberta();

        Action responder = () => p.Responder(ProvidenciaBuilder.ConcessaoDeLeandro(), ProvidenciaBuilder.AtoDeDiogo(), ProvidenciaBuilder.Motivo);

        responder.Should().Throw<InvalidOperationException>();
    }

    private static string[] CodigosDe(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];
}
