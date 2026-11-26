namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ChavesRecusadasNoEnvelopeTests
{
    [Theory]
    [InlineData(false, "ExemploChaveInvalida", "Envelope malformado em 'parte.chave': a chave deve começar por letra minúscula e conter apenas letras minúsculas, números e sublinhados entre palavras, com até 64 caracteres.")]
    [InlineData(true, "chave_exemplo_repetida", "Envelope malformado em 'parte': a chave aparece mais de uma vez.")]
    public void LerOuExigirUnicas_ChaveRecusada_NaoRepeteValor(bool repetida, string valor, string mensagem)
    {
        LeitorEnvelope leitor = new();
        Recusar(leitor, repetida, valor);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be(mensagem).And.NotContain(valor);
        causa.Field.Should().BeNull();
    }
    private static void Recusar(LeitorEnvelope leitor, bool repetida, string valor)
    {
        if (repetida)
        {
            ChaveLocal chave = ChaveLocal.Criar(valor).Value;
            ChavesNoEnvelope.ExigirUnicas(leitor, [chave, chave], "parte");
        }
        else
        {
            ChavesNoEnvelope.Ler(leitor, new JsonObject { ["chave"] = valor }, "parte");
        }
    }
}
