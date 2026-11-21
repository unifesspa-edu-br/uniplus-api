namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

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

        banca.Select(static p => p.Key).Should().BeEquivalentTo(TipoDeBancaNoEnvelope.Chaves);
    }
}
