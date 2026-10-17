namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ResolucaoDosTiposDeAtoUnicosTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 17);

    [Fact(DisplayName = "Resolve a unicidade pelo catálogo vigente, sem lista de códigos: único é o que o catálogo declara único")]
    public async Task Resolver_LeAUnicidadeDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null,
                produtos:
                [
                    ProdutoDaFase.Criar("ATO_QUE_O_CATALOGO_DECLARA_UNICO", PapelProdutoFase.Definitivo),
                    ProdutoDaFase.Criar("ATO_QUE_O_CATALOGO_NAO_DECLARA_UNICO", PapelProdutoFase.Definitivo),
                ],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        reader.ObterVigenteAsync("ATO_QUE_O_CATALOGO_DECLARA_UNICO", Hoje, Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("ATO_QUE_O_CATALOGO_DECLARA_UNICO", "Ato único", true, true, false, true));
        reader.ObterVigenteAsync("ATO_QUE_O_CATALOGO_NAO_DECLARA_UNICO", Hoje, Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("ATO_QUE_O_CATALOGO_NAO_DECLARA_UNICO", "Ato comum", true, false, false, true));

        TiposDeAtoUnicosPorObjeto unicos = await ResolucaoDosTiposDeAtoUnicos
            .ResolverAsync(reader, processo, Hoje, CancellationToken.None);

        unicos.EhUnico("ATO_QUE_O_CATALOGO_DECLARA_UNICO").Should().BeTrue();
        unicos.NomeDe("ATO_QUE_O_CATALOGO_DECLARA_UNICO").Should().Be("Ato único");
        unicos.EhUnico("ATO_QUE_O_CATALOGO_NAO_DECLARA_UNICO").Should().BeFalse();
    }
}
