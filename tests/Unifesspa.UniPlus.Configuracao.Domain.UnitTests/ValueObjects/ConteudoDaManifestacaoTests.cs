namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O parecer e o texto da manifestação técnica (UNI-REQ-0373).</summary>
public sealed class ConteudoDaManifestacaoTests
{
    [Fact(DisplayName = "O parecer recomenda aprovação dispensa o texto e não é desfavorável")]
    public void Ler_RecomendaAprovacaoSemTexto_Aceita()
    {
        List<FieldError> erros = [];

        ConteudoDaManifestacao? conteudo = ConteudoDaManifestacao.Ler(" RECOMENDA_APROVACAO ", null, erros);

        conteudo!.Parecer.Should().Be(ParecerDaManifestacao.RecomendaAprovacao);
        conteudo.Texto.Should().BeNull();
        conteudo.Desfavoravel.Should().BeFalse();
        erros.Should().BeEmpty();
    }

    [Theory(DisplayName = "Recomenda ajuste e recomenda não uso com texto são aceitos e desfavoráveis")]
    [InlineData("RECOMENDA_AJUSTE", ParecerDaManifestacao.RecomendaAjuste)]
    [InlineData("RECOMENDA_NAO_USO", ParecerDaManifestacao.RecomendaNaoUso)]
    public void Ler_DesfavoravelComTexto_Aceita(string parecer, ParecerDaManifestacao esperado)
    {
        List<FieldError> erros = [];

        ConteudoDaManifestacao? conteudo = ConteudoDaManifestacao.Ler(parecer, "  A classificação deve ser sensível.  ", erros);

        conteudo!.Parecer.Should().Be(esperado);
        conteudo.Texto!.Valor.Should().Be("A classificação deve ser sensível.");
        conteudo.Desfavoravel.Should().BeTrue();
    }

    [Theory(DisplayName = "Recomenda ajuste e recomenda não uso sem texto são recusados no campo texto")]
    [InlineData("RECOMENDA_AJUSTE")]
    [InlineData("RECOMENDA_NAO_USO")]
    public void Ler_DesfavoravelSemTexto_Recusa(string parecer)
    {
        List<FieldError> erros = [];

        ConteudoDaManifestacao.Ler(parecer, "   ", erros).Should().BeNull();
        erros.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "texto" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoObrigatorio);
    }

    [Theory]
    [InlineData("ADEQUADA")]
    [InlineData("INADEQUADA")]
    public void Ler_ParecerAntigo_Recusa(string parecer)
    {
        List<FieldError> erros = [];
        ConteudoDaManifestacao.Ler(parecer, "Texto.", erros).Should().BeNull();
        erros.Should().ContainSingle(e => e.Field == "parecer" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.ParecerInvalido);
        new Action(() => PareceresDaManifestacao.Analisar(parecer)).Should().Throw<ArgumentOutOfRangeException>();
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
        PareceresDaManifestacao.TokensCanonicos.Should().Equal("RECOMENDA_APROVACAO", "RECOMENDA_AJUSTE", "RECOMENDA_NAO_USO");
        PareceresDaManifestacao.ParaTokenCanonico(ParecerDaManifestacao.RecomendaAjuste).Should().Be("RECOMENDA_AJUSTE");
        PareceresDaManifestacao.Analisar("RECOMENDA_NAO_USO").Should().Be(ParecerDaManifestacao.RecomendaNaoUso);
        PareceresDaManifestacao.TryAnalisar("recomenda não uso", out _).Should().BeFalse();
        new Action(() => PareceresDaManifestacao.Analisar("TALVEZ")).Should().Throw<ArgumentOutOfRangeException>();
        new Action(() => PareceresDaManifestacao.ParaTokenCanonico(ParecerDaManifestacao.Nenhum)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
