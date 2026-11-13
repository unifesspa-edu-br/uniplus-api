namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O texto que fundamenta um ato da proteção do catálogo de tipos de documento e de fatos do candidato: a justificativa
/// da decisão e o texto da manifestação seguem a mesma regra, cada um com as próprias recusas.
/// </summary>
public sealed class FundamentacaoTests
{
    [Fact(DisplayName = "O texto é guardado sem os espaços das pontas")]
    public void Ler_AparaOTexto()
    {
        List<FieldError> erros = [];

        Fundamentacao? lida = Fundamentacao.Ler("  Dado desnecessário.  ", obrigatoria: true, RecusasDaFundamentacao.DaJustificativa, erros);

        lida!.Valor.Should().Be("Dado desnecessário.");
        erros.Should().BeEmpty();
    }

    [Theory(DisplayName = "O texto opcional ausente ou em branco conta como ausente, sem recusa")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ler_OpcionalAusente_SemRecusa(string? texto)
    {
        List<FieldError> erros = [];

        Fundamentacao.Ler(texto, obrigatoria: false, RecusasDaFundamentacao.DaJustificativa, erros).Should().BeNull();
        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "O texto obrigatório ausente é recusado no campo e com o código de quem o usa")]
    public void Ler_ObrigatorioAusente_RecusaComOCodigoDoUso()
    {
        List<FieldError> erros = [];

        Fundamentacao.Ler("  ", obrigatoria: true, RecusasDaFundamentacao.DaJustificativa, erros);
        Fundamentacao.Ler(null, obrigatoria: true, RecusasDaFundamentacao.DoTextoDaManifestacao, erros);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("justificativa", ProtecaoDoCatalogoErrorCodes.JustificativaObrigatoria),
            ("texto", ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoObrigatorio));
    }

    [Theory(DisplayName = "O texto de até 2.000 caracteres é aceito; com mais, recusado")]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Ler_LimiteDeTamanho(int tamanho, bool aceito)
    {
        List<FieldError> erros = [];

        Fundamentacao? lida = Fundamentacao.Ler(new string('a', tamanho), obrigatoria: false, RecusasDaFundamentacao.DoTextoDaManifestacao, erros);

        (lida is not null).Should().Be(aceito);
        string[] esperados = aceito ? [] : [ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoMuitoLongo];
        erros.Select(static e => e.Error.Code).Should().Equal(esperados);
    }

    [Fact(DisplayName = "A reidratação aceita o texto gravado e falha alto no que não poderia ter sido gravado")]
    public void Analisar_TextoForaDaForma_Falha()
    {
        Fundamentacao.Analisar("Recomendo a classificação sensível.").Valor.Should().Be("Recomendo a classificação sensível.");

        new Action(() => Fundamentacao.Analisar(" com espaço ")).Should().Throw<ArgumentException>();
        new Action(() => Fundamentacao.Analisar(new string('a', 2001))).Should().Throw<ArgumentException>();
        new Action(() => Fundamentacao.Analisar("  ")).Should().Throw<ArgumentException>();
    }
}
