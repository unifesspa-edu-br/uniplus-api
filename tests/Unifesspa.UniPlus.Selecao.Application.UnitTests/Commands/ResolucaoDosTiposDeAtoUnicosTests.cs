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
            reader, atos, processo, tipoDaOperacao, [atoDaLinhagem], relogio, TimeZoneInfo.Utc, new Dictionary<string, Application.Abstractions.TipoDeAtoCongelado>(StringComparer.Ordinal), CancellationToken.None);

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

    [Fact(DisplayName = "Entrega ao checklist a classificação de cada tipo citado: se é resultado e se congela a configuração")]
    public async Task ParaChecklist_LeAClassificacaoDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null,
                produtos:
                [
                    ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Definitivo),
                    ProdutoDaFase.Criar("AVISO_X", papel: null),
                ],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        reader.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Hoje, Arg.Any<CancellationToken>())
            .Returns(
            [
                new TipoAtoPublicadoView("RESULTADO_X", "Resultado", CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true),
                new TipoAtoPublicadoView("AVISO_X", "Aviso", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false),
            ]);

        TiposDeAtoUnicosPorObjeto tipos = await ResolucaoDosTiposDeAtoUnicos.ParaChecklistAsync(
            reader, Substitute.For<IAtoDoProcessoRepository>(), processo, [], Hoje, new Dictionary<string, Application.Abstractions.TipoDeAtoCongelado>(StringComparer.Ordinal), CancellationToken.None);

        tipos.ClassificacaoDe("RESULTADO_X").Should().Be(new ClassificacaoDoTipoDeAto(EhResultado: true, CongelaConfiguracao: true));
        tipos.ClassificacaoDe("AVISO_X").Should().Be(new ClassificacaoDoTipoDeAto(EhResultado: false, CongelaConfiguracao: false));
        tipos.ClassificacaoDe("SEM_VERSAO_VIGENTE").Should().BeNull();
    }

    [Fact(DisplayName = "Congela a classificação de cada tipo declarado, e recusa o tipo declarado que o catálogo não tem")]
    public void Congelamento_UmaEntradaPorTipoDeclarado_ERecusaOSemVersaoVigente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null,
                produtos:
                [
                    ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Definitivo),
                    ProdutoDaFase.Criar("AVISO_UNICO", null),
                ],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        var classificacoes = new Dictionary<string, ClassificacaoDoTipoDeAto>(StringComparer.Ordinal)
        {
            ["RESULTADO_X"] = new(EhResultado: true, CongelaConfiguracao: false),
            ["AVISO_UNICO"] = new(EhResultado: false, CongelaConfiguracao: false),
        };
        TiposDeAtoUnicosPorObjeto tipos = TiposDeAtoUnicosPorObjeto.ParaChecklist(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["AVISO_UNICO"] = "Aviso único" }, [], classificacoes);

        Result<IReadOnlyDictionary<string, Application.Abstractions.TipoDeAtoCongelado>> congelados = CongelamentoDosTiposDeAto.Resolver(processo, tipos);

        congelados.IsSuccess.Should().BeTrue();
        congelados.Value!.Should().HaveCount(2, "o tipo declarado duas vezes congela uma só entrada");
        congelados.Value["RESULTADO_X"].Should().Be(new Application.Abstractions.TipoDeAtoCongelado("RESULTADO_X", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
        congelados.Value["AVISO_UNICO"].UnicoPorObjeto.Should().BeTrue();

        Result<IReadOnlyDictionary<string, Application.Abstractions.TipoDeAtoCongelado>> semVersao = CongelamentoDosTiposDeAto.Resolver(
            processo, TiposDeAtoUnicosPorObjeto.ParaChecklist(new Dictionary<string, string>(StringComparer.Ordinal), [], new Dictionary<string, ClassificacaoDoTipoDeAto>(StringComparer.Ordinal)));
        semVersao.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
    }

    [Fact(DisplayName = "Na retificação, o tipo já congelado na versão vigente mantém a classificação congelada, ainda que o catálogo tenha mudado, e a versão nova a congela de novo")]
    public async Task Retificacao_TipoJaCongelado_IgnoraOCatalogoVivo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null,
                produtos:
                [
                    ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Definitivo),
                    ProdutoDaFase.Criar("TIPO_NOVO", null),
                ],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // No catálogo de hoje o resultado passou a ser único e a congelar; o tipo novo é um aviso comum.
        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        TipoAtoPublicadoView[] catalogo =
        [
            new TipoAtoPublicadoView("RESULTADO_X", "Resultado X", CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, EhResultado: true),
            new TipoAtoPublicadoView("TIPO_NOVO", "Tipo novo", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false),
        ];
        reader.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Hoje, Arg.Any<CancellationToken>())
            .Returns(chamada => catalogo.Where(t => chamada.Arg<IReadOnlyCollection<string>>().Contains(t.Codigo)).ToList());
        var congelados = new Dictionary<string, Application.Abstractions.TipoDeAtoCongelado>(StringComparer.Ordinal)
        {
            ["RESULTADO_X"] = new("RESULTADO_X", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false),
        };

        Result<TiposDeAtoUnicosPorObjeto> resultado = await ResolucaoDosTiposDeAtoUnicos.ConferirAsync(
            reader, Substitute.For<IAtoDoProcessoRepository>(), processo,
            new TipoAtoPublicadoView("EDITAL_RETIFICACAO", "Retificação", true, false, false, false), [],
            new RelogioFixo(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc, congelados, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EhUnico("RESULTADO_X").Should().BeFalse("a marcação de único congelada vale, e não a do catálogo de hoje");
        resultado.Value.ClassificacaoDe("RESULTADO_X")!.CongelaConfiguracao.Should().BeFalse();
        processo.PendenciaDoTipoQueCongelaDeclaradoComoResultado(resultado.Value).Should().BeNull();
        await reader.Received(1).ListarVigentesAsync(
            Arg.Is<IReadOnlyCollection<string>>(c => c.Single() == "TIPO_NOVO"), Hoje, Arg.Any<CancellationToken>());

        Result<IReadOnlyDictionary<string, Application.Abstractions.TipoDeAtoCongelado>> novos = CongelamentoDosTiposDeAto.Resolver(processo, resultado.Value);
        novos.Value!["RESULTADO_X"].Should().Be(congelados["RESULTADO_X"], "a versão nova mantém a classificação da versão anterior");
        novos.Value["TIPO_NOVO"].CongelaConfiguracao.Should().BeFalse("o tipo declarado pela primeira vez lê o catálogo");
    }

    [Fact(DisplayName = "O ato da operação de mesmo código que um item do cronograma não sobrescreve a classificação congelada do item: os gates não recusam e a versão nova a mantém")]
    public async Task Retificacao_AtoDaOperacaoComOCodigoDeItemCongelado_NaoSobrescreveOItem()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "CLASSIFICACAO", "CEPS", OrigemDataFase.Delegada,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: null, fim: null,
                produtos:
                [
                    ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Definitivo),
                ],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!],
            [],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        var congelados = new Dictionary<string, Application.Abstractions.TipoDeAtoCongelado>(StringComparer.Ordinal)
        {
            ["RESULTADO_X"] = new("RESULTADO_X", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false),
        };
        // O catálogo de hoje diz único e congelante, e a retificação usa esse mesmo código como tipo do ato.
        var tipoDaOperacao = new TipoAtoPublicadoView("RESULTADO_X", "Resultado X", CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, EhResultado: true);

        Result<TiposDeAtoUnicosPorObjeto> resultado = await ResolucaoDosTiposDeAtoUnicos.ConferirAsync(
            Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<IAtoDoProcessoRepository>(), processo, tipoDaOperacao, [],
            new RelogioFixo(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc, congelados, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EhUnico("RESULTADO_X").Should().BeFalse("a unicidade do item é a congelada");
        resultado.Value.AtoDaOperacaoEhUnico.Should().BeTrue("o ato da operação tem a vaga dele");
        processo.PendenciaDoTipoQueCongelaDeclaradoComoResultado(resultado.Value).Should().BeNull();
        CongelamentoDosTiposDeAto.Resolver(processo, resultado.Value).Value!["RESULTADO_X"].Should().Be(congelados["RESULTADO_X"]);
    }
}
