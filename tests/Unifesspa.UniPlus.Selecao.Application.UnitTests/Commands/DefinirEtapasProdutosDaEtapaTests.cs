namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasProdutosDaEtapaTests
{
    /// <summary>
    /// A fase confere o ato contra o catálogo de Publicações; a etapa não conferia. O código
    /// declarado aqui vira o nome do produto no cronograma público — um código com erro de
    /// digitação só apareceria depois de o certame estar no Diário Oficial.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com ato fora do catálogo é recusado, como na fase")]
    public async Task Handle_ProdutoComAtoForaDoCatalogo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        // Catálogo que não conhece código algum: é o erro de digitação do payload chegando
        // sem correspondência, não uma indisponibilidade do módulo de Publicações.
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput("RESULTADO_PRELMINAR", null)], Bancas: [], Recursos: []),
            ]);

        Result result = await Executar(
            command, repository, unitOfWork, tipoAtoPublicadoReader: tipoAtoPublicadoReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        // A etapa já foi construída quando a recusa acontece. Não basta o handler deixar de
        // chamar SalvarAlteracoesAsync: o SaveChanges automático do Wolverine roda DEPOIS do
        // retorno e persistiria a mutação, e só o descarte do rastreamento a impede.
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// Papel preliminar ou definitivo é o par que a janela recursal ancora, e só resultado o
    /// admite. A fase recusa o mesmo.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com papel sobre ato que não é resultado é recusado")]
    public async Task Handle_PapelEmAtoQueNaoEhResultado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        // O ato existe e está vigente — o que ele não é, é resultado. A recusa tem de vir da
        // segunda pergunta, não da primeira.
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                "EDITAL_ABERTURA", "Edital de abertura", CongelaConfiguracao: true, UnicoPorObjeto: true,
                EfeitoIrreversivel: false, EhResultado: false));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput("EDITAL_ABERTURA", "PRELIMINAR")], Bancas: [], Recursos: []),
            ]);

        Result result = await Executar(
            command, repository, unitOfWork, tipoAtoPublicadoReader: tipoAtoPublicadoReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        // Mesmo motivo do caso anterior: sem o descarte, o SaveChanges automático do Wolverine
        // persistiria a etapa que a recusa acabou de invalidar.
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// O mesmo tipo de ato aparece muitas vezes num cronograma real — cada etapa publica
    /// preliminar e definitivo, e etapas diferentes publicam o mesmo gabarito. Reler o catálogo
    /// por item multiplica idas ao banco enquanto o <c>SELECT ... FOR UPDATE</c> do certame está
    /// na mão, serializando quem mais estiver editando o mesmo certame por todo esse tempo.
    /// </summary>
    [Fact(DisplayName = "O catálogo é consultado uma vez por código, não uma vez por produto declarado")]
    public async Task Handle_MesmoAtoEmVariosProdutos_ConsultaOCatalogoUmaVezPorCodigo()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // Quatro produtos, dois por etapa, todos do MESMO código: uma consulta basta.
        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos:
                    [
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
                    ], Bancas: [], Recursos: []),
                new EtapaProcessoInput(
                    "Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 2,
                    Produtos:
                    [
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
                    ], Bancas: [], Recursos: []),
            ]);

        Result result = await Executar(
            command, repository, unitOfWork, tipoAtoPublicadoReader: tipoAtoPublicadoReader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await tipoAtoPublicadoReader.Received(1).ObterVigenteAsync(
            "RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo classificado como resultado declarado sem papel pela etapa é recusado com ProcessoSeletivo.ResultadoDeclaradoSemPapel")]
    public async Task Handle_ResultadoSemPapel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", null)], Bancas: [], Recursos: []),
            ]);

        Result result = await Executar(
            command, repository, unitOfWork, tipoAtoPublicadoReader: tipoAtoPublicadoReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.ResultadoDeclaradoSemPapel");
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O endpoint substitui a coleção inteira, e o cliente devolve o que leu. Se o catálogo
    /// aposentar um ato que alguma etapa já declarava, reconferir todo produto a cada gravação
    /// trancaria a edição de qualquer etapa do certame — inclusive de quem só quer corrigir um
    /// nome —, e a única saída seria apagar um produto que o operador não declarou hoje. É a
    /// mesma distinção que o tipo da etapa recebe: confere-se o vínculo novo, não o que já
    /// estava lá.
    /// </summary>
    [Fact(DisplayName = "Ato aposentado no catálogo não tranca a gravação da etapa que já o declarava")]
    public async Task Handle_AtoAposentadoJaDeclarado_NaoTrancaAColecao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        ITipoAtoPublicadoReader catalogoComOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoComOAto.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));

        DefinirEtapasCommand primeiraGravacao = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos:
                    [
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
                    ],
                    Bancas: [], Recursos: []),
            ]);

        (await Executar(primeiraGravacao, repository, unitOfWork, tipoAtoPublicadoReader: catalogoComOAto))
            .IsSuccess.Should().BeTrue();

        Guid etapaId = processo.Etapas.Single().Id;

        // O catálogo aposenta o ato: ObterVigenteAsync deixa de resolvê-lo.
        ITipoAtoPublicadoReader catalogoSemOAto = Substitute.For<ITipoAtoPublicadoReader>();
        catalogoSemOAto.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);

        DefinirEtapasCommand segundaGravacao = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva — 1ª fase", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    etapaId,
                    Produtos:
                    [
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
                        new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
                    ],
                    Bancas: [], Recursos: []),
            ]);

        Result resultado = await Executar(
            segundaGravacao, repository, unitOfWork, tipoAtoPublicadoReader: catalogoSemOAto);

        resultado.IsSuccess.Should().BeTrue(
            $"o produto já estava declarado e não é o que esta gravação muda — veio '{resultado.Error?.Code}'");
        processo.Etapas.Single().Nome.Should().Be("Prova Objetiva — 1ª fase");
    }
}
