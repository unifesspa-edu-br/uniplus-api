namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>As portas de leitura dos cadastros que a exportação do rascunho e a leitura das vagas do rascunho pedem à injeção.</summary>
internal static class PortasDoRascunhoDeTeste
{
    internal static void AdicionarA(IServiceCollection servicos)
    {
        servicos.AddSingleton(Substitute.For<IEsquemaDoDocumentoDaConfiguracao>());
        servicos.AddSingleton(Substitute.For<IUnidadeReader>());
        servicos.AddSingleton(Substitute.For<ITipoProcessoReader>());
        servicos.AddSingleton(Substitute.For<ITipoDocumentoReader>());
        servicos.AddSingleton(Substitute.For<IMotivoDeDecisaoRepository>());
        servicos.AddSingleton(Substitute.For<IPesoAreaEnemReader>());
        servicos.AddSingleton(Substitute.For<IModeloDeDocumentoRepository>());
        servicos.AddSingleton(Substitute.For<ICategoriaDocumentoReader>());
        servicos.AddSingleton(Substitute.For<IFaseCanonicaReader>());
        servicos.AddSingleton(Substitute.For<ITipoBancaReader>());
        servicos.AddSingleton(Substitute.For<ITipoEtapaReader>());
        servicos.AddSingleton(Substitute.For<ITipoAtoPublicadoReader>());
        AdicionarCadastrosDasVagas(servicos);
    }

    private static void AdicionarCadastrosDasVagas(IServiceCollection servicos)
    {
        servicos.AddSingleton(Substitute.For<IOfertaCursoReader>());
        servicos.AddSingleton(Substitute.For<IModalidadeReader>());
        servicos.AddSingleton(Substitute.For<IReferenciaReservaDemograficaReader>());
        servicos.AddSingleton(Substitute.For<ICondicaoAtendimentoReader>());
        servicos.AddSingleton(Substitute.For<IRecursoAcessibilidadeReader>());
        servicos.AddSingleton(Substitute.For<ITipoDeficienciaReader>());
        servicos.AddSingleton(Substitute.For<IBaseLegalDoFadaReader>());
        servicos.AddSingleton(Substitute.For<IRegraCatalogoReader>());
        servicos.AddSingleton(Substitute.For<IFatoCandidatoReader>());
    }
}
