namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.BaseLegalBonusRegional;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O índice único parcial do código da base legal, contra Postgres real: vale entre as vivas, deixa reaproveitar o código de
/// uma base removida, e a violação chega com o nome que o handler traduz em recusa.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class CodigoDaBaseLegalPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public CodigoDaBaseLegalPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Duas bases legais vivas com o mesmo código violam o índice único nomeado")]
    public async Task DuasVivasComOMesmoCodigo_ViolamOIndice()
    {
        string identificacao = IdentificacaoUnica();
        await Gravar(identificacao);

        Func<Task> segunda = () => Gravar(identificacao);

        DbUpdateException violacao = (await segunda.Should().ThrowAsync<DbUpdateException>()).Which;
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(violacao).Should().Be(RestricoesDoBanco.BaseLegalBonusRegionalCodigoVivo);
    }

    [Fact(DisplayName = "O código de uma base legal removida pode ser reaproveitado por uma nova")]
    public async Task CodigoDeBaseRemovida_PodeSerReaproveitado()
    {
        string identificacao = IdentificacaoUnica();
        Guid primeira = await Gravar(identificacao);
        await Remover(primeira);

        Func<Task> reaproveitar = () => Gravar(identificacao);

        await reaproveitar.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Duas criações concorrentes do mesmo ato: uma grava e a outra é barrada pelo índice")]
    public async Task CriacoesConcorrentes_UmaGravaEAOutraEBarrada()
    {
        string identificacao = IdentificacaoUnica();

        Task<Exception?>[] tentativas = [TentarGravar(identificacao), TentarGravar(identificacao)];
        Exception?[] falhas = await Task.WhenAll(tentativas);

        falhas.Count(f => f is null).Should().Be(1);
        Exception barrada = falhas.Single(f => f is not null)!;
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(barrada).Should().Be(RestricoesDoBanco.BaseLegalBonusRegionalCodigoVivo);
    }

    private static string IdentificacaoUnica() => $"Portaria Teste {Guid.NewGuid():N}";

    private async Task<Exception?> TentarGravar(string identificacao)
    {
        try
        {
            await Gravar(identificacao);
            return null;
        }
        catch (DbUpdateException excecao)
        {
            return excecao;
        }
    }

    private async Task<Guid> Gravar(string identificacao)
    {
        Domain.Entities.BaseLegalBonusRegional baseLegal = Domain.Entities.BaseLegalBonusRegional.Criar(
            "PORTARIA", identificacao, "Institui inclusão regional", [("1504208", "Marabá", "PA")]).Value!;

        await using UniPlusDbContext contexto = _fixture.CreateDbContext("admin-a");
        contexto.BaseLegaisBonus.Add(baseLegal);
        await contexto.SaveChangesAsync();

        return baseLegal.Id;
    }

    private async Task Remover(Guid id)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext("admin-a");
        Domain.Entities.BaseLegalBonusRegional rastreada = await contexto.BaseLegaisBonus.Include(b => b.Municipios).SingleAsync(b => b.Id == id);
        contexto.BaseLegaisBonus.Remove(rastreada);
        await contexto.SaveChangesAsync();
    }
}
