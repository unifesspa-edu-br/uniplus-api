namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// A sequência nativa do banco (issue #1858) que numera a inscrição só na primeira submissão —
/// contra Postgres real, porque <c>nextval</c> não tem substituto em memória que prove a mesma
/// garantia de unicidade sob concorrência.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GeradorDeNumeroInscricaoTests : IClassFixture<InscricaoDbFixture>
{
    private readonly InscricaoDbFixture _fixture;

    public GeradorDeNumeroInscricaoTests(InscricaoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Cada chamada devolve um número diferente, formatado em 8 dígitos")]
    public async Task ProximoAsync_ChamadasSucessivas_DevolveNumerosDiferentesEFormatados()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        GeradorDeNumeroInscricao gerador = new(context);

        string primeiro = await gerador.ProximoAsync();
        string segundo = await gerador.ProximoAsync();

        primeiro.Should().MatchRegex("^[0-9]{8}$");
        segundo.Should().MatchRegex("^[0-9]{8}$");
        segundo.Should().NotBe(primeiro);
        long.Parse(segundo, System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeGreaterThan(long.Parse(primeiro, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "Chamadas concorrentes nunca repetem o mesmo número")]
    public async Task ProximoAsync_ChamadasConcorrentes_NuncaRepeteNumero()
    {
        async Task<string> ProximoEmNovoContextoAsync()
        {
            await using SelecaoDbContext context = _fixture.CreateDbContext();
            GeradorDeNumeroInscricao gerador = new(context);
            return await gerador.ProximoAsync();
        }

        string[] numeros = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => ProximoEmNovoContextoAsync()));

        numeros.Should().OnlyHaveUniqueItems("a sequência do banco garante unicidade mesmo sob concorrência real, sem lock nenhum do lado do código");
    }
}
