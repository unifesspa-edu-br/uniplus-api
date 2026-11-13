namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ManifestacoesDeTeste;

/// <summary>O evento de manifestação técnica: guarda o item, o ciclo, o conteúdo e quem se manifestou (ADR-0163).</summary>
public sealed class ManifestacaoDaProtecaoDoCatalogoTests
{
    private static readonly ItemDaProtecao Item = new(CadastroDoCatalogo.Fato, Guid.CreateVersion7());
    private static readonly DateTimeOffset Instante = TipoDocumentoNaAprovacaoBuilder.Segunda;

    [Fact(DisplayName = "O evento guarda o item, o ciclo, o parecer, o texto, quem se manifestou e quando")]
    public void Registrar_GuardaOEvento()
    {
        ManifestacaoDaProtecaoDoCatalogo evento = ManifestacaoDaProtecaoDoCatalogo.Registrar(
            Item, new CicloDaAprovacao(2), RecomendaAjuste("Recomendo sensível."), Kevin, null, Instante);

        evento.Should().BeEquivalentTo(new
        {
            Item,
            Ciclo = new CicloDaAprovacao(2),
            Parecer = ParecerDaManifestacao.RecomendaAjuste,
            SubstituiId = (Guid?)null,
            Instante,
            Desfavoravel = true,
        });
        evento.Texto!.Valor.Should().Be("Recomendo sensível.");
        evento.Autor.Should().Be(Kevin);
    }

    [Theory(DisplayName = "O nome de quem se manifestou é aparado; vazio ou com caractere nulo não é guardado")]
    [InlineData("  Kevin Anderson Carvalho  ", "Kevin Anderson Carvalho")]
    [InlineData("   ", null)]
    [InlineData("Kevin\0Carvalho", null)]
    public void Registrar_NormalizaONome(string? nome, string? esperado) =>
        ManifestacaoDaProtecaoDoCatalogo.Registrar(Item, CicloDaAprovacao.Primeiro, Adequada(), Kevin with { Nome = nome }, null, Instante)
            .Autor.Nome.Should().Be(esperado);

    [Fact(DisplayName = "A mesma pessoa é reconhecida pelo emissor e pelo sub, e não pelo nome")]
    public void DaMesmaPessoa_PeloEmissorEPeloSub()
    {
        ManifestacaoDaProtecaoDoCatalogo evento = ManifestacaoDaProtecaoDoCatalogo.Registrar(Item, CicloDaAprovacao.Primeiro, Adequada(), Kevin, null, Instante);

        evento.DaMesmaPessoa(Kevin with { Nome = "Outro nome" }).Should().BeTrue();
        evento.DaMesmaPessoa(Lucia).Should().BeFalse();
        evento.DaMesmaPessoa(Kevin with { Emissor = "https://outro.emissor" }).Should().BeFalse();
    }

    [Fact(DisplayName = "Substituir a manifestação de outra pessoa, de outro ciclo ou de outro item é erro de programação")]
    public void Registrar_SubstituidaDeOutro_Falha()
    {
        ManifestacaoDaProtecaoDoCatalogo doKevin = ManifestacaoDaProtecaoDoCatalogo.Registrar(Item, CicloDaAprovacao.Primeiro, Adequada(), Kevin, null, Instante);

        Action outraPessoa = () => ManifestacaoDaProtecaoDoCatalogo.Registrar(Item, CicloDaAprovacao.Primeiro, Adequada(), Lucia, doKevin, Instante);
        Action outroCiclo = () => ManifestacaoDaProtecaoDoCatalogo.Registrar(Item, new CicloDaAprovacao(2), Adequada(), Kevin, doKevin, Instante);
        Action outroItem = () => ManifestacaoDaProtecaoDoCatalogo.Registrar(Item with { ItemId = Guid.CreateVersion7() }, CicloDaAprovacao.Primeiro, Adequada(), Kevin, doKevin, Instante);

        outraPessoa.Should().Throw<ArgumentException>();
        outroCiclo.Should().Throw<ArgumentException>();
        outroItem.Should().Throw<ArgumentException>();
    }
}
