namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>A leitura do estado da governança que as métricas publicam, contra o banco real.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class LeituraDoMonitoramentoTests : PerfilEndpointTestBase
{
    public LeituraDoMonitoramentoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A versão pendente que vence em até 48 horas entra na contagem e a que vence depois não")]
    public async Task VersoesVencendo_SoDentroDe48Horas()
    {
        await using CenarioDeVencimento cenario = new(this);
        await cenario.ExecutarAsync();
        cenario.Depois.Should().Be(cenario.Antes + 1);
    }

    private sealed class CenarioDeVencimento(LeituraDoMonitoramentoTests testes) : IAsyncDisposable
    {
        private Guid _vencendo;
        private Guid _distante;

        public long Antes { get; private set; }
        public long Depois { get; private set; }

        public async Task ExecutarAsync()
        {
            _vencendo = await testes.SemearPerfilVigenteAsync(OperarInscricoes);
            _distante = await testes.SemearPerfilVigenteAsync(AnalisarAtendimento);
            Antes = (await testes.LerAsync()).VersoesVencendo;
            await testes.InserirVersaoAsync(_vencendo, 2, "PENDENTE");
            await testes.InserirVersaoAsync(_distante, 2, "PENDENTE");
            await testes.ExecutarAsync("UPDATE autorizacao.versoes_do_perfil SET expira_em = now() + interval '24 hours' WHERE perfil_id = {0} AND numero = 2", _vencendo);
            Depois = (await testes.LerAsync()).VersoesVencendo;
        }

        public async ValueTask DisposeAsync()
        {
            await testes.LimparPendentesInseridasAsync(_vencendo);
            await testes.LimparPendentesInseridasAsync(_distante);
        }
    }

    [Fact(DisplayName = "A divergência da conferência da implantação atual das permissões entra na contagem")]
    public async Task Divergencias_SoDaImplantacaoAtual()
    {
        Guid perfil = await SemearPerfilVigenteAsync(OperarInscricoes);
        long antes = (await LerAsync()).Divergencias;

        await ExecutarAsync(
            """
            INSERT INTO autorizacao.divergencias_da_governanca (id, implantacao_id, perfil_id, codigo, tipo, instante, ordem_na_trilha)
            SELECT {0}, c.id, {1}, 'teste:inscricoes:operar', 'COMPOSICAO_FALTANTE', now(), nextval('autorizacao.ordem_da_trilha_de_contencao')
            FROM autorizacao.implantacoes_das_permissoes c ORDER BY c.sequencia DESC LIMIT 1
            """,
            Guid.CreateVersion7(), perfil);

        (await LerAsync()).Divergencias.Should().Be(antes + 1);
    }

    private async Task<AmostraDaGovernanca> LerAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ILeituraDoMonitoramento>().LerAsync(DateTimeOffset.UtcNow);
    }
}
