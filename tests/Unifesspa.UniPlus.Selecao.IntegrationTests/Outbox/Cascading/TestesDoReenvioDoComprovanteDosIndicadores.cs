namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCenarioDeIndicadores;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class TestesDoReenvioDoComprovanteDosIndicadores : IAsyncLifetime
{
    private readonly ApoioDoCenarioDeIndicadores _apoio;
    private readonly CascadingFixture _ambiente;

    public TestesDoReenvioDoComprovanteDosIndicadores(CascadingFixture cenario)
    {
        _ambiente = cenario;
        _apoio = new ApoioDoCenarioDeIndicadores(cenario);
    }

    public Task InitializeAsync() => _apoio.LimparConcessoesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Reenvio da inscrição não altera os três indicadores")]
    public async Task Reenvio_DaInscricaoSubmetida_NaoAlteraOsIndicadores()
    {
        Guid processoId = await _apoio.SemearPublicadoAsync(nameof(Reenvio_DaInscricaoSubmetida_NaoAlteraOsIndicadores));
        Guid inscricaoId = await GravarPrimeiroEnvioAsync(processoId);
        await _apoio.ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        using HttpResponseMessage antes = await _apoio.EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);
        await ReenviarInscricaoAsync(inscricaoId);
        using HttpResponseMessage depois = await _apoio.EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);
        await ConferirContagensAsync(antes, 1L, 1L, 0L);
        await ConferirContagensAsync(depois, 1L, 1L, 0L);
    }

    private async Task<Guid> GravarPrimeiroEnvioAsync(Guid processoId)
    {
        Inscricao inscricao = CenarioDaInscricaoGravada.NovaInscricao(PersonasDeTeste.FernandaDrumond, processoId);
        string numero = Random.Shared.NextInt64(1000000000000000, 9999999999999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Result<ComprovanteInscricao> envio = CenarioDaInscricaoGravada.Enviar(inscricao, numero);
        envio.IsSuccess.Should().BeTrue();
        await using AsyncServiceScope escopo = _ambiente.Factory.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>().AdicionarAsync(inscricao);
        await escopo.ServiceProvider.GetRequiredService<IComprovanteDaInscricaoRepository>().AdicionarComprovanteAsync(envio.Value!);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync();
        return inscricao.Id;
    }

    private async Task ReenviarInscricaoAsync(Guid inscricaoId)
    {
        await using AsyncServiceScope escopo = _ambiente.Factory.Services.CreateAsyncScope();
        IInscricaoRepository inscricoes = escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>();
        IComprovanteDaInscricaoRepository comprovantes = escopo.ServiceProvider.GetRequiredService<IComprovanteDaInscricaoRepository>();
        Inscricao inscricao = (await inscricoes.ObterPorIdAsync(inscricaoId))!;
        Result<ComprovanteInscricao> reenvio = CenarioDaInscricaoGravada.Enviar(inscricao, "000901");
        reenvio.IsSuccess.Should().BeTrue();
        await inscricoes.AtualizarAsync(inscricao);
        await comprovantes.AdicionarComprovanteAsync(reenvio.Value!);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync();
        (await comprovantes.ObterComprovantesAsync([inscricaoId], CancellationToken.None)).Should().HaveCount(2);
    }

}
