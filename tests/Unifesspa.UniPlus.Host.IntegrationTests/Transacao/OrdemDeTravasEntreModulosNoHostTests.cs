namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Esqueleto do teste da ordem de travas da ADR-0170: a transação que grava em mais de um módulo trava o recurso
/// compartilhado, depois o processo, depois o ato.
/// </summary>
/// <remarks>
/// <para><b>O que garante hoje.</b> Um handler de teste, que segue a ordem, trava uma linha do catálogo da Configuração, o
/// processo (<c>IProcessoSeletivoRepository.TravarAsync</c>) e só então grava em Publicações. Uma segunda transação, que o teste
/// mantém aberta, faz o handler esperar num ponto conhecido, e a conferência vê, por <c>FOR UPDATE NOWAIT</c> noutra sessão, o
/// que ele já tinha travado: esperando pelo ato, ele mantém o recurso e o processo; esperando pelo processo, mantém o
/// recurso. A ordem inversa apareceria como trava ainda livre.</para>
/// <para><b>O que depende da porta de escrita.</b> A porta de escrita no <c>Contracts</c> de Publicações ainda não existe, e
/// nenhuma operação do produto trava o recurso compartilhado. Por isso o handler é do teste, e o teste não prova que as
/// operações reais seguem a ordem: quando a porta existir, cada operação que grava em Publicações a atravessa depois de
/// travar o processo, e a mesma conferência passa a rodar sobre o handler real, no lugar deste.</para>
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class OrdemDeTravasEntreModulosNoHostTests : IClassFixture<HostComHandlersDeTransacao>
{
    private const string TravaDoRecursoSemEspera =
        "SELECT 1 FROM configuracao.rol_de_fatos_candidato ORDER BY id LIMIT 1 FOR UPDATE NOWAIT";

    private readonly IServiceProvider _servicos;

    public OrdemDeTravasEntreModulosNoHostTests(MonolitoPostgresFixture fixture, HostComHandlersDeTransacao host)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(host);
        _servicos = host.Servicos(fixture);
    }

    [Fact(DisplayName = "Quem espera pelo ato já travou o recurso compartilhado e o processo")]
    public async Task EsperandoPeloAto_JaTravouORecursoEOProcesso()
    {
        Guid processoId = await SemearProcessoAsync();
        string codigo = NovoCodigo();

        Result resultado = await ExecutarEsperandoAsync(
            new GravarNaOrdemDasTravas(processoId, codigo),
            dona => GravarTipoSemConfirmarAsync(dona, codigo),
            async () => await ConferirTravadasAsync(TravaDoRecursoSemEspera, TravaDoProcessoSemEspera(processoId)));

        resultado.IsSuccess.Should().BeTrue();
        (await ConsultasDaTransacaoNoBanco.TiposAsync(_servicos, codigo)).Should().Be(1);
    }

    [Fact(DisplayName = "Quem espera pelo processo já travou o recurso compartilhado")]
    public async Task EsperandoPeloProcesso_JaTravouORecurso()
    {
        Guid processoId = await SemearProcessoAsync();
        string codigo = NovoCodigo();

        Result resultado = await ExecutarEsperandoAsync(
            new GravarNaOrdemDasTravas(processoId, codigo),
            dona => TravarProcessoSemConfirmarAsync(dona, processoId),
            async () => await ConferirTravadasAsync(TravaDoRecursoSemEspera));

        resultado.IsSuccess.Should().BeTrue();
        (await ConsultasDaTransacaoNoBanco.TiposAsync(_servicos, codigo)).Should().Be(1);
    }

    private static string NovoCodigo() => CodigoDeTesteDaTransacao.Novo("TRAVAS");

    private static string TravaDoProcessoSemEspera(Guid processoId) =>
        $"SELECT 1 FROM selecao.processos_seletivos WHERE id = '{processoId}' FOR UPDATE NOWAIT";

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _servicos.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "Processo da ordem de travas", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadorLegivel.Criar($"certame-{Guid.NewGuid():N}"[..40]).Value!);
        contexto.Set<ProcessoSeletivo>().Add(processo);
        await contexto.SaveChangesAsync();

        return processo.Id;
    }

    private static async Task GravarTipoSemConfirmarAsync(TransacaoRetidaDeTeste dona, string codigo)
    {
        UniPlusDbContext contexto = dona.Servicos.GetRequiredService<UniPlusDbContext>();
        contexto.Set<Unifesspa.UniPlus.Publicacoes.Domain.Entities.TipoAtoPublicado>().Add(GravarEmDoisModulosHandler.NovoTipo(codigo));
        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// Prende o ponto de espera com uma transação do teste, executa o comando até ele esperar, confere o que ele já travou e
    /// solta a transação (rollback), para o comando seguir.
    /// </summary>
    private async Task<Result> ExecutarEsperandoAsync(
        GravarNaOrdemDasTravas comando, Func<TransacaoRetidaDeTeste, Task> prender, Func<Task> conferirEnquantoEspera)
    {
        Task<Result> execucao;
        await using (TransacaoRetidaDeTeste dona = await TransacaoRetidaDeTeste.AbrirAsync(_servicos))
        {
            await prender(dona);
            execucao = ExecutarAsync(comando);
            await dona.AguardarBloqueioAsync();
            await conferirEnquantoEspera();
        }

        return await execucao;
    }

    private async Task ConferirTravadasAsync(params string[] travasSemEspera)
    {
        foreach (string trava in travasSemEspera)
        {
            (await ConsultasDaTransacaoNoBanco.TravadaAsync(_servicos, trava)).Should().BeTrue($"já está travada pelo comando em espera: {trava}");
        }
    }

    private static async Task TravarProcessoSemConfirmarAsync(TransacaoRetidaDeTeste dona, Guid processoId) =>
        await dona.Servicos.GetRequiredService<UniPlusDbContext>().Database
            .ExecuteSqlRawAsync(TravaDoProcessoSemEspera(processoId).Replace(" NOWAIT", string.Empty, StringComparison.Ordinal));

    private Task<Result> ExecutarAsync(GravarNaOrdemDasTravas comando) =>
        Task.Run(async () =>
        {
            await using AsyncServiceScope escopo = _servicos.CreateAsyncScope();

            return await escopo.ServiceProvider.GetRequiredService<ICommandBus>().Send(comando, CancellationToken.None);
        });
}
