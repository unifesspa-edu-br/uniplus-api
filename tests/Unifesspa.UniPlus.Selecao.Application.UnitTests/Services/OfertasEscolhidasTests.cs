namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;

using Xunit;

/// <summary>As ofertas do inscrito, nomeadas pela versão dele e na ordem em que ele as escolheu.</summary>
public sealed class OfertasEscolhidasTests
{
    private static readonly Guid Primeira = Guid.CreateVersion7();
    private static readonly Guid Segunda = Guid.CreateVersion7();

    [Fact(DisplayName = "Com um turno, o nome sai com curso, grau, unidade e o turno por extenso")]
    public void Nome_UmTurno()
    {
        Dictionary<Guid, string> nomes = OfertasEscolhidas.NomesDe(
            [new OfertaDeCursoCertameDto(Primeira, "Medicina", "Bacharelado", "Instituto de Estudos em Saúde e Biológicas", ["MATUTINO"])]);

        nomes[Primeira].Should().Be("Medicina (Bacharelado) — Instituto de Estudos em Saúde e Biológicas — Matutino");
    }

    [Fact(DisplayName = "Com dois turnos, o nome junta os turnos por 'e'")]
    public void Nome_DoisTurnos()
    {
        Dictionary<Guid, string> nomes = OfertasEscolhidas.NomesDe(
            [new OfertaDeCursoCertameDto(Primeira, "Direito", "Bacharelado", "Faculdade de Direito", ["MATUTINO", "NOTURNO"])]);

        nomes[Primeira].Should().Be("Direito (Bacharelado) — Faculdade de Direito — Matutino e Noturno");
    }

    [Fact(DisplayName = "As duas opções saem na ordem da escolha, com os nomes da versão")]
    public void Montar_DuasOpcoes()
    {
        IReadOnlyList<OfertaEscolhidaDto> ofertas = OfertasEscolhidas.Montar(
            Primeira, Segunda, new Dictionary<Guid, string> { [Primeira] = "Curso A", [Segunda] = "Curso B" });

        ofertas.Should().Equal(new OfertaEscolhidaDto(Primeira, "Curso A", 1), new OfertaEscolhidaDto(Segunda, "Curso B", 2));
    }

    [Fact(DisplayName = "Oferta fora da versão sai com nome nulo e a ordem segue sem lacuna")]
    public void Montar_OfertaForaDaVersao_NomeNuloSemLacuna()
    {
        IReadOnlyList<OfertaEscolhidaDto> ofertas = OfertasEscolhidas.Montar(
            Primeira, Segunda, new Dictionary<Guid, string> { [Segunda] = "Curso B" });

        ofertas.Should().Equal(new OfertaEscolhidaDto(Primeira, null, 1), new OfertaEscolhidaDto(Segunda, "Curso B", 2));
    }

    [Fact(DisplayName = "Sem a primeira opção, a segunda assume a ordem 1")]
    public void Montar_SoASegunda_OrdemUm() =>
        OfertasEscolhidas.Montar(null, Segunda, new Dictionary<Guid, string>())
            .Should().Equal(new OfertaEscolhidaDto(Segunda, null, 1));
}
