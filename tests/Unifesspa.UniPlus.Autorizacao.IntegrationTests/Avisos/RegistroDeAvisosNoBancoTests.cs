namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Avisos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>O registro dos avisos contra o PostgreSQL real: grava só na transação da operação e devolve apenas os identificadores novos.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class RegistroDeAvisosNoBancoTests
{
    private const string Tipo = "PERMISSOES_REMOVIDAS";

    private readonly AutorizacaoEndpointFixture _fixture;

    public RegistroDeAvisosNoBancoTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O registro sem transação da operação é recusado e não grava")]
    public async Task RegistrarAsync_SemTransacao_Recusa()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        IRegistroDeAvisos registro = escopo.ServiceProvider.GetRequiredService<IRegistroDeAvisos>();
        Guid mensagem = Guid.CreateVersion7();

        Func<Task> registrar = () => registro.RegistrarAsync(Tipo, Guid.CreateVersion7(), [mensagem]);

        await registrar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*transação*");
        (await ContarAsync(escopo, mensagem)).Should().Be(0);
    }

    [Fact(DisplayName = "O registro na transação devolve os identificadores novos na ordem pedida e omite os já registrados")]
    public async Task RegistrarAsync_ComTransacao_DevolveSoOsNovos()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        IRegistroDeAvisos registro = escopo.ServiceProvider.GetRequiredService<IRegistroDeAvisos>();
        await using IDbContextTransaction transacao = await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.BeginTransactionAsync();
        (Guid primeiro, Guid segundo, Guid terceiro, Guid fato) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        IReadOnlyCollection<Guid> novos = await registro.RegistrarAsync(Tipo, fato, [primeiro, segundo]);
        IReadOnlyCollection<Guid> sobrepostos = await registro.RegistrarAsync(Tipo, fato, [segundo, terceiro]);

        novos.Should().Equal(primeiro, segundo);
        sobrepostos.Should().Equal(terceiro);
    }

    private static Task<int> ContarAsync(AsyncServiceScope escopo, Guid mensagem) =>
        escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.avisos_emitidos WHERE mensagem_id = {mensagem}")
            .SingleAsync();
}
