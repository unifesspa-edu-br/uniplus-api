namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

public sealed class TipoDeBancaNoEnvelopeTests
{
    private static TipoDeBancaCopiado Tipo(string nome) => new(
        Guid.CreateVersion7(), TipoDeBanca.Heteroidentificacao, nome, PapelDaBanca.AvaliaOMerito, true, false, [TipoMotivoDeDecisao.Heteroidentificacao]);

    [Fact(DisplayName = "O nome do tipo sai normalizado em NFC: a forma composta e a decomposta dão o mesmo texto no envelope")]
    public void Nome_SaiEmNfc()
    {
        string composto = "Comissão de heteroidentificação";
        string decomposto = composto.Normalize(NormalizationForm.FormD);
        decomposto.IsNormalized(NormalizationForm.FormC).Should().BeFalse("o teste só vale se as duas formas diferem");
        JsonObject deComposto = [];
        JsonObject deDecomposto = [];

        TipoDeBancaNoEnvelope.Serializar(deComposto, Tipo(composto));
        TipoDeBancaNoEnvelope.Serializar(deDecomposto, Tipo(decomposto));

        deDecomposto["nome"]!.GetValue<string>().Should().Be(composto).And.Be(deComposto["nome"]!.GetValue<string>());
    }

    [Fact(DisplayName = "A cópia do tipo escreve todas as chaves que a leitura exige")]
    public void Serializar_EscreveTodasAsChaves()
    {
        JsonObject banca = [];

        TipoDeBancaNoEnvelope.Serializar(banca, Tipo("Comissão"));

        banca.Select(static p => p.Key).Should().BeEquivalentTo(TipoDeBancaNoEnvelope.Chaves.Append("contexto"));
        banca["contexto"]!["codigoRotulo"]!.GetValue<string>().Should().Be(TipoDeBanca.Heteroidentificacao.Rotulo());
        banca["contexto"]!["papelRotulo"]!.GetValue<string>().Should().Be(PapelDaBanca.AvaliaOMerito.Rotulo());
    }

    [Fact(DisplayName = "A cópia do tipo de 2ª instância, escrita e lida de volta pelo envelope, mantém o tipo e o papel que julga o recurso em 2ª instância")]
    public void Ler_CopiaDeSegundaInstancia_MantemOPapel()
    {
        TipoDeBancaCopiado copia = new(
            Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia, "Banca de análise de recursos de 2ª instância",
            PapelDaBanca.JulgaASegundaInstancia, false, false, []);
        JsonObject escrita = [];
        LeitorEnvelope leitor = new();

        TipoDeBancaNoEnvelope.Serializar(escrita, copia);
        JsonObject banca = JsonNode.Parse(escrita.ToJsonString())!.AsObject();
        TipoDeBancaCopiado? lida = TipoDeBancaNoEnvelope.Ler(leitor, banca, "fases[0].bancasRequeridas[0]");

        banca["papel"]!.GetValue<string>().Should().Be("JULGA_A_SEGUNDA_INSTANCIA");
        leitor.Falhou.Should().BeFalse();
        lida.Should().Be(copia);
        lida!.Papel.Should().Be(PapelDaBanca.JulgaASegundaInstancia);
    }
}
