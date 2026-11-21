namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.BancoDaSementeParaTeste;

/// <summary>
/// O motivo que a semente registra quando não grava a versão do termo, contra Postgres real: o termo da semente não foi
/// gravado, ou o termo já tem uma versão com o mesmo número. O registro traz o código do termo e nunca o texto dele. Cada
/// teste desfaz o que gravou, porque o banco da coleção é compartilhado.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class SementeDeVersaoPuladaTests
{
    private const string CodigoDoTermo = "TERMO_FICTICIO_DA_SEMENTE_DE_TESTE";

    private static readonly TermoDaSemente Semente = new(
        Guid.Parse("7e570001-0000-7000-8000-0000000000e1"), Guid.Parse("7e570002-0000-7000-8000-0000000000e1"),
        "Termo fictício da semente de teste", "Texto fictício do termo de teste.", "Base legal fictícia.");

    private static readonly Guid OutroTermoId = Guid.Parse("7e570001-0000-7000-8000-0000000000e2");

    private static readonly Guid OutraVersaoId = Guid.Parse("7e570002-0000-7000-8000-0000000000e2");

    private static readonly DeclaracaoDaSemente Declaracao = new(
        "de teste", "semente-de-teste", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    private readonly ConfiguracaoDbFixture _fixture;

    public SementeDeVersaoPuladaTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O termo não gravado porque o código está ocupado deixa a versão pulada com o motivo de o termo não ter sido gravado")]
    public async Task TermoNaoGravado_VersaoPuladaDizQueOTermoNaoFoiGravado()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await CadastrarTermoAsync(ctx, OutroTermoId, CodigoDoTermo, "Outro termo fictício de teste", removido: false);
        RegistroDeLogDeTeste log = new();

        await AplicarOTermoAsync(ctx, log);

        ConferirMensagens(log,
            $"A semente não gravou o item termo {CodigoDoTermo}: o código já está ocupado.",
            $"A semente não gravou o item versão do termo {CodigoDoTermo}: o termo não foi gravado.");
        await transacao.RollbackAsync();
    }

    [Fact(DisplayName = "O termo que já tem uma versão com o mesmo número deixa a versão pulada com o motivo de o número estar ocupado")]
    public async Task NumeroDaVersaoOcupado_VersaoPuladaDizQueONumeroEstaOcupado()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();
        await CadastrarTermoAsync(ctx, Semente.TermoId, CodigoDoTermo, Semente.Nome, removido: false);
        await CadastrarVersaoAsync(ctx, OutraVersaoId, Semente.TermoId, numero: 1);
        RegistroDeLogDeTeste log = new();

        await AplicarOTermoAsync(ctx, log);

        ConferirMensagens(log,
            $"A semente não gravou o item versão do termo {CodigoDoTermo}: o termo já tem uma versão com esse número.");
        await transacao.RollbackAsync();
    }

    private static async Task AplicarOTermoAsync(UniPlusDbContext ctx, RegistroDeLogDeTeste log) =>
        await AplicacaoDosComandosDaSemente.ExecutarAsync(
            ctx, [.. SqlDaSemente.ComandosDoTermo(Semente, Declaracao)], log, CancellationToken.None);

    private static void ConferirMensagens(RegistroDeLogDeTeste log, params string[] esperadas)
    {
        log.Mensagens.Should().BeEquivalentTo(esperadas);
        log.Mensagens.Should().NotContain(static m => m.Contains(Semente.Texto, StringComparison.Ordinal), "o registro não traz o texto do termo");
    }
}
