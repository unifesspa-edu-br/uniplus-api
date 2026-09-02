namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Guarda o vocabulário de tipos de banca (<see cref="TipoBancaCatalogo"/>) contra edições
/// que passariam despercebidas: um código acrescentado ao catálogo sem rótulo, ou uma
/// segunda lista de códigos que diverge da primeira.
/// </summary>
public sealed class CatalogoDeTiposBancaTests
{
    [Fact(DisplayName = "Codigos é exatamente o conjunto de Descritos, na mesma ordem")]
    public void Codigos_DerivaDeDescritos()
    {
        // Codigos é uma projeção de Descritos, não uma segunda lista escrita à mão — este
        // teste prova a derivação, não apenas o conjunto (BeEquivalentTo aceitaria ordem
        // trocada, o que aqui seria sintoma de alguém ter voltado a duas listas irmãs).
        TipoBancaCatalogo.Codigos.Should().Equal(
            [.. TipoBancaCatalogo.Descritos.Select(static d => d.Codigo)]);
    }

    [Fact(DisplayName = "Todo tipo de banca do vocabulário tem rótulo não vazio")]
    public void Descritos_TemRotuloNaoVazio()
    {
        TipoBancaCatalogo.Descritos.Should().AllSatisfy(
            static d => d.Nome.Should().NotBeNullOrWhiteSpace());
    }

    [Fact(DisplayName = "O vocabulário tem exatamente os seis códigos canônicos, na ordem publicada")]
    public void Descritos_TemOsSeisCodigosNaOrdemPublicada()
    {
        // Afirmado por extenso, e não derivado do catálogo: é o vocabulário publicado que
        // está sob teste — derivá-lo da mesma fonte que o produz faria o teste concordar
        // com qualquer mudança, inclusive uma remoção acidental de código.
        TipoBancaCatalogo.Descritos.Select(static d => d.Codigo).Should().Equal(
            "BANCA_ANALISE_DOCUMENTAL",
            "BANCA_ENTREVISTA",
            "BANCA_CORRECAO_REDACOES",
            "BANCA_ANALISE_RECURSOS",
            "BANCA_HETEROIDENTIFICACAO",
            "BANCA_BIOPSICOSSOCIAL");
    }
}
