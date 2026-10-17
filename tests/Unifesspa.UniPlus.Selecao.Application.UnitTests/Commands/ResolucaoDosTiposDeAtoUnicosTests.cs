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

    [Fact(DisplayName = "Resolve a unicidade numa só leitura do catálogo vigente, incluindo o tipo do ato da operação")]
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
        reader.ListarVigentesAsync(
                Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 3),
                Hoje,
                Arg.Any<CancellationToken>())
            .Returns(
            [
                new TipoAtoPublicadoView("ATO_QUE_O_CATALOGO_DECLARA_UNICO", "Ato único", true, true, false, true),
                new TipoAtoPublicadoView("ATO_QUE_O_CATALOGO_NAO_DECLARA_UNICO", "Ato comum", true, false, false, true),
                new TipoAtoPublicadoView("EDITAL_DA_OPERACAO", "Edital", true, true, false, false),
            ]);

        TiposDeAtoUnicosPorObjeto unicos = await ResolucaoDosTiposDeAtoUnicos
            .ParaOperacaoAsync(reader, processo, "EDITAL_DA_OPERACAO", Hoje, CancellationToken.None);

        unicos.EhUnico("ATO_QUE_O_CATALOGO_DECLARA_UNICO").Should().BeTrue();
        unicos.NomeDe("ATO_QUE_O_CATALOGO_DECLARA_UNICO").Should().Be("Ato único");
        unicos.EhUnico("ATO_QUE_O_CATALOGO_NAO_DECLARA_UNICO").Should().BeFalse();
        unicos.EhUnico("EDITAL_DA_OPERACAO").Should().BeTrue();
        unicos.AtoDaOperacaoCodigo.Should().Be("EDITAL_DA_OPERACAO");
        await reader.Received(1).ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Hoje, Arg.Any<CancellationToken>());
        await reader.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }
}
