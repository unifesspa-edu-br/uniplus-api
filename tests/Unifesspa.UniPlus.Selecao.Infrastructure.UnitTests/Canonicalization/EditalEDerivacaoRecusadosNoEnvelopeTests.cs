namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class EditalEDerivacaoRecusadosNoEnvelopeTests
{
    [Fact]
    public void LerApresentacao_FatoRepetido_NaoRepeteValor()
    {
        const string valor = "ExemploFatoRepetido";
        JsonObject documento = new() { ["dadosCitadosSemPergunta"] = new JsonArray(Fato(valor), Fato(valor)) };
        LeitorEnvelope leitor = new();
        LeituraDoEdital.LerApresentacaoDosPressupostos(leitor, documento);
        Conferir(leitor.Falha<object>().Errors.Should().ContainSingle().Subject, valor, "dadosCitadosSemPergunta");
    }

    [Fact]
    public void ValidarBloco_VersaoDesconhecida_NaoRepeteValor()
    {
        const string valor = "ExemploVersaoDesconhecida";
        JsonObject documento = new() { ["versaoDasRegrasDeCalculo"] = valor };
        BlocoDeFatosEDerivacao bloco = new([], [], [], documento["versaoDasRegrasDeCalculo"]!.GetValue<string>());
        DomainError erro = LeituraDaDerivacao.ValidarBlocoDeFatosEDerivacao(bloco, [], [], [], [], documento)!;
        LeitorEnvelope leitor = new();
        leitor.Propagar<object>(erro);
        Conferir(leitor.Falha<object>().Errors.Should().ContainSingle().Subject, valor, "versaoDasRegrasDeCalculo");
    }

    private static JsonObject Fato(string valor) => new() { ["codigoDoDado"] = valor, ["rotulo"] = "Dado", ["tipoDeCampo"] = null };

    private static void Conferir(FieldError causa, string valor, string caminho)
    {
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain(caminho).And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
