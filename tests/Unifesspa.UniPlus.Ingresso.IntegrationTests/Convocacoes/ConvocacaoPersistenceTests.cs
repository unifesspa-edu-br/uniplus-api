namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Convocacoes;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Enums;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(IngressoComPostgres.Name)]
public sealed class ConvocacaoPersistenceTests(MonolitoPostgresFixture fixture)
{
    [Fact(DisplayName = "Convocação gravada é lida com protocolo e status, sem eventos de domínio pendentes")]
    public async Task Convocacao_GravadaELida_MantemProtocoloEStatusESemEventos()
    {
        Chamada chamada = NovaChamada();
        Convocacao gravada = NovaConvocacao(chamada.Id);
        await GravarAsync(chamada, gravada);

        Convocacao lida = await LerAsync(gravada.Id);

        lida.Protocolo.Valor.Should().Be(gravada.Protocolo.Valor);
        lida.Status.Should().Be(StatusConvocacao.Pendente);
        lida.CodigoCurso.Should().Be("CURSO01");
        lida.DomainEvents.Should().BeEmpty("a materialização não registra o evento de candidato convocado de novo");
    }

    private static Chamada NovaChamada()
    {
        DateTimeOffset publicacao = DateTimeOffset.UtcNow;

        return Chamada.Criar(Guid.CreateVersion7(), numero: 1, publicacao, publicacao.AddDays(5));
    }

    private static Convocacao NovaConvocacao(Guid chamadaId) =>
        Convocacao.Criar(
            chamadaId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ProtocoloConvocacao.Gerar(1, Random.Shared.Next(1, 99999), TimeProvider.System),
            posicao: 1,
            codigoCurso: "CURSO01",
            clock: TimeProvider.System);

    private async Task GravarAsync(Chamada chamada, Convocacao convocacao)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        contexto.Set<Chamada>().Add(chamada);
        contexto.Set<Convocacao>().Add(convocacao);
        await contexto.SaveChangesAsync();
    }

    private async Task<Convocacao> LerAsync(Guid id)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await contexto.Set<Convocacao>().AsNoTracking().SingleAsync(c => c.Id == id);
    }
}
