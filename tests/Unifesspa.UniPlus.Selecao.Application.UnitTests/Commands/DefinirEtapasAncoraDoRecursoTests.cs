namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasAncoraDoRecursoTests
{
    [Theory(DisplayName = "Com o tipo fora do catálogo hoje, a âncora nova da etapa é recusada e a que o agregado já tinha segue aceita")]
    [InlineData(false, "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo")]
    [InlineData(true, null)]
    public async Task Handle_AncoraEmTipoSemVersaoVigenteNoCatalogo_RecusaSoANovaEmRelacaoAoAgregado(bool ancoraJaNoAgregado, string? codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        RegraCatalogo regra = RegraCatalogo.Criar(
            "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999, art. 59");
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(regra);

        EtapaProcessoInput Etapa(Guid? id, bool comAncora) => new(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, id,
            Produtos:
            [
                new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
            ],
            Bancas: [],
            Recursos: comAncora
                ? [new RecursoDaEtapaInput(
                    AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                    2m, UnidadePrazo.DiasUteis, "RESULTADO_CLASSIFICACAO", null, null, false, null, null)]
                : []);

        ITipoAtoPublicadoReader catalogoComOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoComOAto.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));
        Result declaracao = await Executar(
            new DefinirEtapasCommand(processo.Id, [Etapa(null, ancoraJaNoAgregado)]),
            repository, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoComOAto, registroCodecs: codecs);
        declaracao.IsSuccess.Should().BeTrue(declaracao.Error?.Message);
        Guid etapaId = processo.Etapas.Single().Id;
        VersaoPublicadaDeTeste.Publicar(
            processo, repository, codecs,
            new TipoDeAtoCongelado("RESULTADO_CLASSIFICACAO", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));

        // O catálogo deixa de ter versão vigente do tipo: a irreversibilidade dele passa a ser desconhecida.
        ITipoAtoPublicadoReader catalogoSemOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoSemOAto.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [Etapa(etapaId, true)]),
            repository, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoSemOAto, registroCodecs: codecs);

        if (codigoEsperado is null)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Error!.Code.Should().Be(codigoEsperado);
        }
    }

    [Fact(DisplayName = "A âncora que só a versão publicada tem segue aceita quando o catálogo perde o tipo e o agregado não a tem mais")]
    public async Task Handle_AncoraSoNaVersaoPublicada_CatalogoSemOTipo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        RegraCatalogo regra = RegraCatalogo.Criar(
            "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999, art. 59");
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(regra);

        EtapaProcessoInput Etapa(Guid? id, bool comAncora) => new(
            "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, id,
            Produtos:
            [
                new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
            ],
            Bancas: [],
            Recursos: comAncora
                ? [new RecursoDaEtapaInput(
                    AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                    2m, UnidadePrazo.DiasUteis, "RESULTADO_CLASSIFICACAO", null, null, false, null, null)]
                : []);

        ITipoAtoPublicadoReader catalogoComOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoComOAto.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));

        // A versão publicada tem a âncora. O grafo vem de uma instância à parte, com a identidade da etapa
        // viva, porque a gravação seguinte edita a etapa no lugar.
        (await Executar(
                new DefinirEtapasCommand(processo.Id, [Etapa(null, true)]),
                repository, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoComOAto, registroCodecs: codecs))
            .IsSuccess.Should().BeTrue();
        Guid etapaId = processo.Etapas.Single().Id;

        ProcessoSeletivo publicado = ProcessoSemEtapas();
        IProcessoSeletivoRepository repositorioDoPublicado = Substitute.For<IProcessoSeletivoRepository>();
        repositorioDoPublicado.ObterParaMutacaoAsync(publicado.Id, Arg.Any<CancellationToken>()).Returns(publicado);
        (await Executar(
                new DefinirEtapasCommand(publicado.Id, [Etapa(null, true)]),
                repositorioDoPublicado, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoComOAto))
            .IsSuccess.Should().BeTrue();
        typeof(Unifesspa.UniPlus.Kernel.Domain.Entities.EntityBase).GetProperty("Id")!.SetValue(publicado.Etapas.Single(), etapaId);
        publicado.Etapas.Single().Id.Should().Be(etapaId);
        publicado.Etapas.Single().Recursos.Should().ContainSingle();
        VersaoPublicadaDeTeste.Publicar(
            processo, publicado, repository, codecs,
            new TipoDeAtoCongelado("RESULTADO_CLASSIFICACAO", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));

        // A sessão grava o agregado sem a âncora, o catálogo perde o tipo e o operador a reenvia.
        (await Executar(
                new DefinirEtapasCommand(processo.Id, [Etapa(etapaId, false)]),
                repository, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoComOAto, registroCodecs: codecs))
            .IsSuccess.Should().BeTrue();
        processo.Etapas.Single().Recursos.Should().BeEmpty();
        publicado.Etapas.Single().Recursos.Should().ContainSingle();
        ITipoAtoPublicadoReader catalogoSemOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoSemOAto.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [Etapa(etapaId, true)]),
            repository, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoSemOAto, registroCodecs: codecs);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Em rascunho, a âncora que a etapa já tinha segue aceita quando o catálogo perde o tipo")]
    public async Task Handle_RascunhoComAncoraJaDeclarada_CatalogoSemOTipo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        RegraCatalogo regra = RegraCatalogo.Criar(
            "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999, art. 59");
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(regra);

        EtapaProcessoInput Etapa(Guid? id, string nome) => new(
            nome, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, id,
            Produtos:
            [
                new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
            ],
            Bancas: [],
            Recursos:
            [
                new RecursoDaEtapaInput(
                    AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                    2m, UnidadePrazo.DiasUteis, "RESULTADO_CLASSIFICACAO", null, null, false, null, null),
            ]);

        ITipoAtoPublicadoReader catalogoComOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoComOAto.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));
        Result declaracao = await Executar(
            new DefinirEtapasCommand(processo.Id, [Etapa(null, "Prova Objetiva")]),
            repository, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoComOAto);
        declaracao.IsSuccess.Should().BeTrue(declaracao.Error?.Message);
        Guid etapaId = processo.Etapas.Single().Id;

        ITipoAtoPublicadoReader catalogoSemOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoSemOAto.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [Etapa(etapaId, "Prova Objetiva — 1ª fase")]),
            repository, unitOfWork, regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: catalogoSemOAto);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Âncora do recurso da etapa em ato congelante ou irreversível é recusada, como na fase")]
    [InlineData(true, false, "ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado")]
    [InlineData(false, true, "RecursoDaEtapa.AncoraEmAtoIrreversivel")]
    public async Task Handle_AncoraEmAtoQueNaoCabeRecurso_Recusa(
        bool congelaConfiguracao, bool efeitoIrreversivel, string codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        RegraCatalogo regra = RegraCatalogo.Criar(
            "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
            "Lei 9.784/1999, art. 59");
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(regra);

        // O ato é resultado — a conferência do produto passa —, mas carrega a flag que torna
        // a âncora indevida. As três flags são independentes no catálogo: garantir que o ato
        // é resultado não garante que ele não congela a configuração nem que é irreversível.
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                "RESULTADO_CLASSIFICACAO", "Resultado da classificação",
                CongelaConfiguracao: congelaConfiguracao, UnicoPorObjeto: false,
                EfeitoIrreversivel: efeitoIrreversivel, EhResultado: true));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR")],
                    Recursos:
                    [
                        new RecursoDaEtapaInput(
                            AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                            2m, UnidadePrazo.DiasUteis, "RESULTADO_CLASSIFICACAO", null, null, false, null, null),
                    ], Bancas: []),
            ]);

        Result result = await Executar(
            command, repository, unitOfWork,
            regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: tipoAtoPublicadoReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A declaração de instância superior da janela chega à janela gravada, e não um valor fixo do handler")]
    public async Task Handle_JanelaComInstanciaSuperior_GravaADeclaracao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        RegraCatalogo regra = RegraCatalogo.Criar(
            "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
            "Lei 9.784/1999, art. 59");
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(regra);

        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                "RESULTADO_CLASSIFICACAO", "Resultado da classificação",
                CongelaConfiguracao: false, UnicoPorObjeto: false,
                EfeitoIrreversivel: false, EhResultado: true));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"), new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO")],
                    Recursos:
                    [
                        new RecursoDaEtapaInput(
                            AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                            2m, UnidadePrazo.DiasUteis, "RESULTADO_CLASSIFICACAO", null, null, true, null, null),
                    ], Bancas: []),
            ]);

        Result result = await Executar(
            command, repository, unitOfWork,
            regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: tipoAtoPublicadoReader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Recursos.Single().Parametros.AdmiteInstanciaSuperior.Should().BeTrue();
    }

    /// <summary>
    /// A etapa publica o mesmo ato duas vezes — preliminar e definitivo —, e é da publicação
    /// preliminar que a janela recursal corre. Resolver a âncora só pelo código do ato devolvia
    /// o definitivo sempre que ele viesse primeiro na coleção, e a etapa era recusada por
    /// ancorar onde não se pode — sem que nada no payload estivesse errado.
    /// </summary>
    [Fact(DisplayName = "A âncora do recurso resolve no produto PRELIMINAR, mesmo com o definitivo antes na coleção")]
    public async Task Handle_AncoraComDefinitivoAntesDoPreliminar_ResolveNoPreliminar()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        RegraCatalogo regra = RegraCatalogo.Criar(
            "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
            "Lei 9.784/1999, art. 59");
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));
        regraCatalogoReader
            .ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(regra);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos:
                    [
                        // O definitivo vem primeiro de propósito: é a ordem que o servidor
                        // devolve depois de reler a etapa, e era ela que derrubava a gravação.
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                    ],
                    Recursos:
                    [
                        new RecursoDaEtapaInput(
                            AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                            2m, UnidadePrazo.DiasUteis, "RESULTADO_CLASSIFICACAO", null, null, false, null, null),
                    ], Bancas: []),
            ]);

        Result result = await Executar(
            command, repository, unitOfWork,
            regraCatalogoReader: regraCatalogoReader, tipoAtoPublicadoReader: tipoAtoPublicadoReader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }
}
