namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>A leitura do estado da governança que as métricas publicam, contra o banco real.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class LeituraDoMonitoramentoTests : PerfilEndpointTestBase
{
    public LeituraDoMonitoramentoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A versão pendente que vence em até 48 horas entra na contagem e a que vence depois não")]
    public async Task VersoesVencendo_SoDentroDe48Horas()
    {
        Guid vencendo = await SemearPerfilVigenteAsync(OperarInscricoes);
        Guid distante = await SemearPerfilVigenteAsync(AnalisarAtendimento);
        long antes = (await LerAsync()).VersoesVencendo;
        try
        {
            await InserirVersaoAsync(vencendo, 2, "pendente");
            await InserirVersaoAsync(distante, 2, "pendente");
            await ExecutarAsync("UPDATE autorizacao.versoes_do_perfil SET expira_em = now() + interval '24 hours' WHERE perfil_id = {0} AND numero = 2", vencendo);

            (await LerAsync()).VersoesVencendo.Should().Be(antes + 1);
        }
        finally
        {
            await LimparPendentesInseridasAsync(vencendo);
            await LimparPendentesInseridasAsync(distante);
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
            SELECT {0}, c.id, {1}, 'teste:inscricoes:operar', 'composicao_faltante', now(), nextval('autorizacao.ordem_da_trilha_de_contencao')
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
