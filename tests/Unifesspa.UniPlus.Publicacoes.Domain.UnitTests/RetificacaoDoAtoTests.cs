namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>A relação de retificação: o par (ato retificado, motivo) completo ou ausente.</summary>
public sealed class RetificacaoDoAtoTests
{
    [Fact(DisplayName = "Retificação ausente é aceita")]
    public void Conferir_Ausente_Aceita()
    {
        ParteConferida<RelacaoDeRetificacao> parte = default(RelacaoDeRetificacao).Conferir();

        parte.EstaAceita.Should().BeTrue();
        parte.Valor.Should().Be(default(RelacaoDeRetificacao));
    }

    [Fact(DisplayName = "Par completo é aceito com o motivo aparado")]
    public void Conferir_ParCompleto_AceitoComMotivoAparado()
    {
        Guid retificado = Guid.CreateVersion7();

        ParteConferida<RelacaoDeRetificacao> parte = new RelacaoDeRetificacao(retificado, "  corrige o anexo II ").Conferir();

        parte.Valor.Should().Be(new RelacaoDeRetificacao(retificado, "corrige o anexo II"));
    }

    [Fact(DisplayName = "Só o ato retificado aponta o motivo que falta")]
    public void Conferir_SoOAtoRetificado_ApontaOMotivo()
    {
        ParteConferida<RelacaoDeRetificacao> parte = new RelacaoDeRetificacao(Guid.CreateVersion7(), null).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("motivoRetificacao", AtoNormativoErrorCodes.RetificacaoIncompleta));
    }

    [Fact(DisplayName = "Motivo só de espaços conta como ausente e aponta o motivo que falta")]
    public void Conferir_MotivoEmBranco_ApontaOMotivo()
    {
        ParteConferida<RelacaoDeRetificacao> parte = new RelacaoDeRetificacao(Guid.CreateVersion7(), "   ").Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("motivoRetificacao", AtoNormativoErrorCodes.RetificacaoIncompleta));
    }

    [Fact(DisplayName = "Só o motivo aponta o ato retificado que falta")]
    public void Conferir_SoOMotivo_ApontaOAtoRetificado()
    {
        ParteConferida<RelacaoDeRetificacao> parte = new RelacaoDeRetificacao(null, "corrige o anexo II").Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("atoRetificadoId", AtoNormativoErrorCodes.RetificacaoIncompleta));
    }

    [Theory(DisplayName = "Ato retificado vazio é recusado com uma recusa só, com ou sem motivo")]
    [InlineData(null)]
    [InlineData("corrige o anexo II")]
    public void Conferir_AtoRetificadoVazio_UmaRecusaSo(string? motivo)
    {
        ParteConferida<RelacaoDeRetificacao> parte = new RelacaoDeRetificacao(Guid.Empty, motivo).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("atoRetificadoId", AtoNormativoErrorCodes.RetificacaoIncompleta));
        parte.Recusas[0].Error.Message.Should().Be("O identificador do ato retificado não pode ser vazio.");
    }

    [Fact(DisplayName = "Motivo acima de 1000 caracteres é recusado como texto inválido")]
    public void Conferir_MotivoLongo_RecusaComoTextoInvalido()
    {
        ParteConferida<RelacaoDeRetificacao> parte = new RelacaoDeRetificacao(Guid.CreateVersion7(), new string('m', 1001)).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("motivoRetificacao", AtoNormativoErrorCodes.TextoInvalido));
    }

    [Fact(DisplayName = "Ato retificado vazio e motivo longo são recusados juntos, o ato retificado primeiro")]
    public void Conferir_AtoRetificadoVazioEMotivoLongo_AcumulaNaOrdem()
    {
        ParteConferida<RelacaoDeRetificacao> parte = new RelacaoDeRetificacao(Guid.Empty, new string('m', 1001)).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(
            ("atoRetificadoId", AtoNormativoErrorCodes.RetificacaoIncompleta),
            ("motivoRetificacao", AtoNormativoErrorCodes.TextoInvalido));
    }

    [Fact(DisplayName = "Conferida lança ArgumentException com o campo da primeira recusa")]
    public void Conferida_Invalida_LancaComOCampo()
    {
        Action acao = () => new RelacaoDeRetificacao(Guid.Empty, "corrige").Conferida();

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("atoRetificadoId");
    }
}
