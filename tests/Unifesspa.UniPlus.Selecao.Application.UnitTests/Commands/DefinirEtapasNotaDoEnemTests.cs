namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasNotaDoEnemTests
{
    [Fact(DisplayName = "Etapa de nota do ENEM com recurso em ato é recusada pela regra do ENEM, antes da conferência da âncora")]
    public async Task Handle_NotaEnemComRecursoEmAto_RecusaPelaRegraDoEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", null, AdmitePontuacao: true, AdmiteEliminacao: true, NotaDeOrigemNoEnem: true, ConceitosAdmitidos: []));
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        regraCatalogoReader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
                JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement,
                "Lei 9.784/1999, art. 59").Value!);

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Nota do ENEM", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [],
                    Recursos:
                    [
                        new RecursoDaEtapaInput(
                            AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1",
                            2m, UnidadePrazo.DiasUteis, "RESULTADO_CLASSIFICACAO", null, null, false, null, null),
                    ], Bancas: []),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, reader, regraCatalogoReader: regraCatalogoReader);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Etapa nova congela a nota de origem no ENEM lida do cadastro")]
    public async Task Handle_EtapaNova_CongelaANotaDeOrigemNoEnem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", null, AdmitePontuacao: true, AdmiteEliminacao: true, NotaDeOrigemNoEnem: true, ConceitosAdmitidos: []));
        reader.ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoRedacaoOrigemId, "REDACAO", "Redação", null, AdmitePontuacao: true, AdmiteEliminacao: true, NotaDeOrigemNoEnem: false, ConceitosAdmitidos: []));

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Nota do ENEM", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 2, Produtos: [], Bancas: [], Recursos: []),
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, reader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoProvaObjetivaOrigemId).DeclaraNotaDoEnem.Should().BeTrue();
        processo.Etapas.Single(e => e.TipoEtapaOrigemId == TipoRedacaoOrigemId).DeclaraNotaDoEnem.Should().BeFalse();
    }

    /// <summary>
    /// A origem da nota é identidade: mudar o caráter da etapa relê o tipo para regravar os
    /// sinalizadores, e a origem congelada fica como estava, seja o que for que o cadastro diga.
    /// </summary>
    [Fact(DisplayName = "Mudar o caráter regrava os sinalizadores e mantém a nota de origem no ENEM congelada")]
    public async Task Handle_CaraterAlterado_MantemANotaDeOrigemNoEnemCongelada()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoEtapaSnapshot doEnem = TipoEtapaSnapshot.Criar(
            TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!;
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: doEnem, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "NOTA_ENEM", "Nota do ENEM", null, AdmitePontuacao: true, AdmiteEliminacao: true, NotaDeOrigemNoEnem: false, ConceitosAdmitidos: []));

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Nota do ENEM", CaraterEtapa.Ambas, TipoProvaObjetivaOrigemId, 1m, 5m, 1, etapa.Id, Produtos: [], Bancas: [], Recursos: [])],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, reader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.NotaDeOrigemNoEnem.Should().BeTrue(
            "a regravação dos sinalizadores não alcança a identidade congelada");
        await reader.Received().ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }
}
