namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Esqueleto do teste da ordem de travas da ADR-0170: a transação que grava em mais de um módulo trava o recurso
/// compartilhado, depois o processo, depois o ato.
/// </summary>
/// <remarks>
/// <para><b>O que garante.</b> Um handler de teste, que segue a ordem, trava uma linha do catálogo da Configuração, o
/// processo (<c>IProcessoSeletivoRepository.TravarAsync</c>) e só então registra o ato pela porta de escrita de Publicações.
/// Uma segunda transação, que o teste mantém aberta, faz o handler esperar num ponto conhecido, e a conferência vê, por
/// <c>FOR UPDATE NOWAIT</c> noutra sessão, o que ele já tinha travado: esperando pelo ato, ele mantém o recurso e o
/// processo; esperando pelo processo, mantém o recurso e ainda não tocou nenhuma tabela de Publicações. A ordem inversa
/// apareceria como trava ainda livre, ou como escrita em Publicações antes da trava do processo.</para>
/// <para><b>O limite.</b> O handler é do teste: ele prova que a porta cabe na ordem, e a ordem de cada operação do produto é
/// conferida nos testes de cada uma.</para>
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
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
        string tipo = await SemearTipoAsync();

        Result resultado = await ExecutarEsperandoAsync(
            new GravarNaOrdemDasTravas(processoId, NovoCodigo(), tipo),
            dona => RegistrarSemConfirmarAsync(dona, processoId, tipo),
            async () => await ConferirTravadasAsync(TravaDoRecursoSemEspera, TravaDoProcessoSemEspera(processoId)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (await ConsultasDaTransacaoNoBanco.VagasDoObjetoAsync(_servicos, processoId)).Should().Be(1, "o ato do comando reservou a vaga depois que a transação retida soltou");
    }

    [Fact(DisplayName = "Quem espera pelo processo já travou o recurso compartilhado e ainda não escreveu em Publicações")]
    public async Task EsperandoPeloProcesso_JaTravouORecurso_ENaoEscreveuEmPublicacoes()
    {
        Guid processoId = await SemearProcessoAsync();
        string tipo = await SemearTipoAsync();

        Result resultado = await ExecutarEsperandoAsync(
            new GravarNaOrdemDasTravas(processoId, NovoCodigo(), tipo),
            dona => TravarProcessoSemConfirmarAsync(dona, processoId),
            async () =>
            {
                await ConferirTravadasAsync(TravaDoRecursoSemEspera);
                (await ConsultasDaTransacaoNoBanco.EscritasEmPublicacoesAsync(_servicos)).Should().Be(0, "nenhuma escrita em Publicações antes da trava do processo");
            });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (await ConsultasDaTransacaoNoBanco.VagasDoObjetoAsync(_servicos, processoId)).Should().Be(1);
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

    private async Task<string> SemearTipoAsync()
    {
        string tipo = CodigoDeTesteDaTransacao.Novo("TIPO_TRAVAS");
        await CatalogoDeAtosDeTeste.SemearTipoAsync(_servicos, tipo, unicoPorObjeto: true);

        return tipo;
    }

    /// <summary>Registra, na transação retida, o ato que ocupa a vaga do processo: o comando que chegar depois espera por ela.</summary>
    private static async Task RegistrarSemConfirmarAsync(TransacaoRetidaDeTeste dona, Guid processoId, string tipo)
    {
        Result registrado = await dona.Servicos.GetRequiredService<IRegistroDeAtoNormativo>().RegistrarAsync(
            RegistrarAtoPelaPortaHandler.AtoDe(new RegistrarAtoPelaPorta(
                CodigoDeTesteDaTransacao.Novo("DONA"), Guid.CreateVersion7(), processoId, tipo, UnicoPorObjeto: true, DesfechoDaGravacao.Sucesso)),
            CancellationToken.None);
        registrado.IsSuccess.Should().BeTrue(registrado.Error?.Message);
        await dona.Servicos.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync(CancellationToken.None);
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
