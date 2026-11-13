namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// O handler das etapas montado como o host o monta: os serviços de aplicação vêm do registro do módulo, e o
/// repositório, a leitura das fases iniciadas e a unidade de trabalho são os de produção sobre um
/// <see cref="SelecaoDbContext"/> próprio no Postgres do fixture. Só os cadastros de outros módulos (tipos de
/// etapa, de banca, de ato e catálogo de regras) são dublês, porque moram em bancos que o fixture não tem.
/// </summary>
/// <remarks>
/// A transação ambiente do Wolverine confirma o contexto ao fim do handler, tenha ele gravado ou não: por isso
/// <see cref="DefinirAsync"/> salva uma vez mais depois dele, e o que o handler deixou rastreado numa recusa
/// chegaria ao banco se ele não o descartasse.
/// </remarks>
internal sealed class DefinicaoDasEtapasNoBanco : IAsyncDisposable
{
    private readonly ServiceProvider _servicos;
    private readonly AsyncServiceScope _escopo;
    private readonly TimeProvider _relogio;

    public DefinicaoDasEtapasNoBanco(ProcessoSeletivoDbFixture fixture, TimeProvider relogio, params TipoEtapaView[] tiposDeEtapa)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _relogio = relogio;
        ServiceCollection servicos = new();
        RegistrarOModuloSobOBanco(servicos, fixture, relogio);
        RegistrarOsCadastrosDeOutrosModulos(servicos, tiposDeEtapa);
        _servicos = servicos.BuildServiceProvider(validateScopes: true);
        _escopo = _servicos.CreateAsyncScope();
    }

    public async Task<Result<MutacaoAceita>> DefinirAsync(DefinirEtapasCommand command)
    {
        IServiceProvider escopo = _escopo.ServiceProvider;
        Result<MutacaoAceita> resultado = await DefinirEtapasCommandHandler.Handle(
            command,
            escopo.GetRequiredService<IProcessoSeletivoRepository>(),
            escopo.GetRequiredService<DefinicaoDasEtapas>(),
            escopo.GetRequiredService<ISelecaoUnitOfWork>(),
            _relogio,
            CancellationToken.None);
        await escopo.GetRequiredService<SelecaoDbContext>().SaveChangesAsync();
        return resultado;
    }

    public async ValueTask DisposeAsync()
    {
        await _escopo.DisposeAsync();
        await _servicos.DisposeAsync();
    }

    private static void RegistrarOModuloSobOBanco(IServiceCollection servicos, ProcessoSeletivoDbFixture fixture, TimeProvider relogio)
    {
        servicos.AddSelecaoApplication();
        servicos.AddScoped(_ => fixture.CreateDbContext());
        servicos.AddScoped<ISelecaoUnitOfWork>(static sp => sp.GetRequiredService<SelecaoDbContext>());
        servicos.AddScoped<IProcessoSeletivoRepository>(sp => new ProcessoSeletivoRepository(sp.GetRequiredService<SelecaoDbContext>(), relogio));
        servicos.AddScoped<IFasesIniciadasDoProcesso, FasesIniciadasDoProcesso>();
        servicos.AddScoped<IRegistroCodecsEnvelope, RegistroCodecsEnvelope>();
    }

    private static void RegistrarOsCadastrosDeOutrosModulos(IServiceCollection servicos, TipoEtapaView[] tiposDeEtapa)
    {
        ITipoEtapaReader tipos = Substitute.For<ITipoEtapaReader>();
        foreach (TipoEtapaView tipo in tiposDeEtapa)
        {
            tipos.ObterAtivoPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        }

        servicos.AddSingleton(tipos);
        servicos.AddSingleton(Substitute.For<ITipoBancaReader>());
        servicos.AddSingleton(Substitute.For<IRegraCatalogoReader>());
        servicos.AddSingleton(Substitute.For<ITipoAtoPublicadoReader>());
    }
}
