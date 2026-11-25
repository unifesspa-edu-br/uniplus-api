namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

internal static class PortasDaInscricaoDeTeste
{
    internal static void AdicionarA(IServiceCollection servicos)
    {
        AdicionarRepositorios(servicos);
        AdicionarPortas(servicos);
        AdicionarAutorizacao(servicos);
        servicos.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    private static void AdicionarRepositorios(IServiceCollection servicos)
    {
        servicos.AddSingleton(Substitute.For<IInscricaoRepository>());
        servicos.AddSingleton(Substitute.For<IDocumentoDaInscricaoRepository>());
        servicos.AddSingleton(Substitute.For<IProcessoSeletivoRepository>());
        servicos.AddSingleton(Substitute.For<ICertameDivulgadoRepository>());
        servicos.AddSingleton(Substitute.For<IComprovanteDaInscricaoRepository>());
        servicos.AddSingleton(Substitute.For<ILoteDeNotasDoEnemRepository>());
        servicos.AddSingleton(Substitute.For<IGeracaoDeEnvioAoInepRepository>());
    }

    private static void AdicionarPortas(IServiceCollection servicos)
    {
        servicos.AddSingleton(Substitute.For<IUnitOfWork>());
        servicos.AddSingleton(Substitute.For<IUserContext>());
        servicos.AddSingleton(Substitute.For<IResolvedorFusoInstitucional>());
        servicos.AddSingleton(Substitute.For<IRegistroCodecsEnvelope>());
        servicos.AddSingleton(Substitute.For<IConferenciaEnderecoGeo>());
        servicos.AddSingleton(Substitute.For<IArquivoDoCandidatoStorage>());
        servicos.AddSingleton(Substitute.For<IGeradorDeNumeroInscricao>());
        servicos.AddSingleton(Substitute.For<IResolvedorDaUnidadeResponsavel>());
        servicos.AddSingleton(Substitute.For<IInscritosDaEdicaoDoEnem>());
        servicos.AddSingleton(Substitute.For<IIndiceCegoDoCpf>());
        servicos.AddSingleton(Substitute.For<IEnderecoNoAcervoPublico>());
    }

    private static void AdicionarAutorizacao(IServiceCollection servicos)
    {
        servicos.AddSingleton(Substitute.For<IVerificadorDeAcesso>());
        servicos.AddSingleton(Substitute.For<IDecisoesDaRequisicao>());
        servicos.AddScoped<ProjetorDeItens>();
    }
}
