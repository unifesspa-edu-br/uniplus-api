namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>A leitura das vagas do rascunho sobre leitores sem cadastro, para os documentos de teste que não têm distribuição de vagas.</summary>
internal static class LeituraDasVagasDeTeste
{
    internal static LeituraDasVagasDoRascunho SemCadastros() => new(
        new LeituraDosCadastrosDasVagasCitados(
            Substitute.For<IOfertaCursoReader>(),
            Substitute.For<IModalidadeReader>(),
            Substitute.For<IReferenciaReservaDemograficaReader>(),
            Substitute.For<ICondicaoAtendimentoReader>(),
            Substitute.For<IRecursoAcessibilidadeReader>(),
            Substitute.For<ITipoDeficienciaReader>(),
            Substitute.For<IBaseLegalDoFadaReader>()),
        new LeituraDosCadastrosDoCatalogoCitados(
            Substitute.For<IRegraCatalogoReader>(), Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<IFatoCandidatoReader>()),
        new FakeTimeProvider());
}
