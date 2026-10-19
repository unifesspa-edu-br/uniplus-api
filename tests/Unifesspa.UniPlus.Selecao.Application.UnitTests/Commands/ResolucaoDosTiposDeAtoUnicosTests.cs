namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ResolucaoDosTiposDeAtoUnicosTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 17);

    [Fact(DisplayName = "Resolve a unicidade numa só leitura do catálogo vigente, com as vagas lidas do registro local dos atos e o tipo do ato da operação")]
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
                new TipoAtoPublicadoView("EDITAL_JA_REGISTRADO", "Edital já registrado", true, true, false, false),
            ]);
        Guid atoDaLinhagem = Guid.CreateVersion7();
        IAtoDoProcessoRepository atos = Substitute.For<IAtoDoProcessoRepository>();
        atos.ListarTiposDosAtosAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == atoDaLinhagem), Arg.Any<CancellationToken>())
            .Returns(["EDITAL_JA_REGISTRADO"]);
        var tipoDaOperacao = new TipoAtoPublicadoView("EDITAL_DA_OPERACAO", "Edital", true, true, false, false);
        TimeProvider relogio = new RelogioFixo(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));

        Result<TiposDeAtoUnicosPorObjeto> resultado = await ResolucaoDosTiposDeAtoUnicos.ConferirAsync(
            reader, atos, processo, tipoDaOperacao, [atoDaLinhagem], relogio, TimeZoneInfo.Utc, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        TiposDeAtoUnicosPorObjeto unicos = resultado.Value!;
        unicos.EhUnico("ATO_QUE_O_CATALOGO_DECLARA_UNICO").Should().BeTrue();
        unicos.NomeDe("ATO_QUE_O_CATALOGO_DECLARA_UNICO").Should().Be("Ato único");
        unicos.EhUnico("ATO_QUE_O_CATALOGO_NAO_DECLARA_UNICO").Should().BeFalse();
        unicos.EhUnico("EDITAL_DA_OPERACAO").Should().BeTrue("a unicidade do ato da operação vem do tipo já conferido, sem reler o catálogo");
        unicos.VagaOcupada("EDITAL_JA_REGISTRADO").Should().BeTrue();
        unicos.EhUnico("EDITAL_JA_REGISTRADO").Should().BeTrue("o código ocupado entra na leitura em lote do catálogo, que decide a unicidade");
        await reader.Received(1).ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Hoje, Arg.Any<CancellationToken>());
        await reader.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }
}
