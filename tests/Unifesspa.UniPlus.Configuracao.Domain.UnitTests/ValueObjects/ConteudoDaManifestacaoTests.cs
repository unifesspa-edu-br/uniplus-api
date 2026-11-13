namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O parecer e o texto da manifestação técnica (UNI-REQ-0373).</summary>
public sealed class ConteudoDaManifestacaoTests
{
    [Fact(DisplayName = "O parecer adequada dispensa o texto e não é desfavorável")]
    public void Ler_AdequadaSemTexto_Aceita()
    {
        List<FieldError> erros = [];

        ConteudoDaManifestacao? conteudo = ConteudoDaManifestacao.Ler(" ADEQUADA ", null, erros);

        conteudo!.Parecer.Should().Be(ParecerDaManifestacao.Adequada);
        conteudo.Texto.Should().BeNull();
        conteudo.Desfavoravel.Should().BeFalse();
        erros.Should().BeEmpty();
    }

    [Theory(DisplayName = "Recomenda ajuste e inadequada com texto são aceitos e desfavoráveis")]
    [InlineData("RECOMENDA_AJUSTE", ParecerDaManifestacao.RecomendaAjuste)]
    [InlineData("INADEQUADA", ParecerDaManifestacao.Inadequada)]
    public void Ler_DesfavoravelComTexto_Aceita(string parecer, ParecerDaManifestacao esperado)
    {
        List<FieldError> erros = [];

        ConteudoDaManifestacao? conteudo = ConteudoDaManifestacao.Ler(parecer, "  A classificação deve ser sensível.  ", erros);

        conteudo!.Parecer.Should().Be(esperado);
        conteudo.Texto!.Valor.Should().Be("A classificação deve ser sensível.");
        conteudo.Desfavoravel.Should().BeTrue();
    }

    [Theory(DisplayName = "Recomenda ajuste e inadequada sem texto são recusados no campo texto")]
    [InlineData("RECOMENDA_AJUSTE")]
    [InlineData("INADEQUADA")]
    public void Ler_DesfavoravelSemTexto_Recusa(string parecer)
    {
        List<FieldError> erros = [];

        ConteudoDaManifestacao.Ler(parecer, "   ", erros).Should().BeNull();
        erros.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "texto" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoObrigatorio);
    }

    [Fact(DisplayName = "O parecer fora do vocabulário e o texto longo demais saem juntos no lote")]
    public void Ler_AcumulaAsRecusas()
    {
        List<FieldError> erros = [];

        ConteudoDaManifestacao.Ler("TALVEZ", new string('a', 2001), erros).Should().BeNull();
        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("parecer", ProtecaoDoCatalogoErrorCodes.ParecerInvalido),
            ("texto", ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoMuitoLongo));
    }

    [Fact(DisplayName = "Os pareceres têm token canônico, e o token fora do vocabulário falha alto na reidratação")]
    public void Pareceres_TokensCanonicos()
    {
        PareceresDaManifestacao.TokensCanonicos.Should().Equal("ADEQUADA", "RECOMENDA_AJUSTE", "INADEQUADA");
        PareceresDaManifestacao.ParaTokenCanonico(ParecerDaManifestacao.RecomendaAjuste).Should().Be("RECOMENDA_AJUSTE");
        PareceresDaManifestacao.Analisar("INADEQUADA").Should().Be(ParecerDaManifestacao.Inadequada);
        PareceresDaManifestacao.TryAnalisar("inadequada", out _).Should().BeFalse();
        new Action(() => PareceresDaManifestacao.Analisar("TALVEZ")).Should().Throw<ArgumentOutOfRangeException>();
        new Action(() => PareceresDaManifestacao.ParaTokenCanonico(ParecerDaManifestacao.Nenhum)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
