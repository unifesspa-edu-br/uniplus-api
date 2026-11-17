namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>O tipo de etapa que exige correção independente, lido do cadastro e congelado na etapa, e o que a gravação recusa por ele.</summary>
public sealed class DefinirEtapasCorrecaoIndependenteTests
{
    private static ITipoEtapaReader ReaderDoTipoQueExige(bool exigeCorrecaoIndependente)
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(
                TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", null,
                AdmitePontuacao: true, AdmiteEliminacao: true, NotaDeOrigemNoEnem: false,
                ExigeCorrecaoIndependente: exigeCorrecaoIndependente, ConceitosAdmitidos: []));
        return reader;
    }

    private static EtapaProcessoInput EtapaObjetiva(Guid? id = null, CaraterEtapa carater = CaraterEtapa.Classificatoria) =>
        new("Prova Objetiva", carater, TipoProvaObjetivaOrigemId, 1m, null, 1, id, FormaDeAvaliacao: FormaDeAvaliacaoCodigo.Objetiva, Produtos: [], Bancas: [], Recursos: []);

    [Fact(DisplayName = "Etapa nova de tipo que exige correção independente, em avaliação objetiva, é recusada no campo da forma")]
    public async Task EtapaNova_FormaObjetivaNoTipoQueExige_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result result = await Executar(
            new DefinirEtapasCommand(processo.Id, [EtapaObjetiva()]),
            repository, unitOfWork, ReaderDoTipoQueExige(exigeCorrecaoIndependente: true));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Field.Should().Be("etapas[0].formaDeAvaliacao");
        result.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.FormaObjetivaEmTipoQueExigeCorrecaoIndependente);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Etapa nova de tipo que não exige correção independente aceita a avaliação objetiva e congela o sinalizador desligado")]
    public async Task EtapaNova_TipoQueNaoExige_AceitaECongelaDesligado()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        Result result = await Executar(
            new DefinirEtapasCommand(processo.Id, [EtapaObjetiva()]),
            repository, Substitute.For<IUnitOfWork>(), ReaderDoTipoQueExige(exigeCorrecaoIndependente: false));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.ExigeCorrecaoIndependente.Should().BeFalse();
    }

    [Fact(DisplayName = "Mudar o caráter de etapa existente relê o tipo e congela o sinalizador que o cadastro declara hoje")]
    public async Task CaraterAlterado_RegravaOSinalizadorDeCorrecaoIndependente()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoEtapaSnapshot semExigir = TipoEtapaSnapshot.Criar(
            TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso existente = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: semExigir, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([existente]).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        EtapaProcessoInput humana = EtapaObjetiva(existente.Id, CaraterEtapa.Ambas) with { FormaDeAvaliacao = FormaDeAvaliacaoCodigo.Humana };
        Result result = await Executar(
            new DefinirEtapasCommand(processo.Id, [humana]),
            repository, Substitute.For<IUnitOfWork>(), ReaderDoTipoQueExige(exigeCorrecaoIndependente: true));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.ExigeCorrecaoIndependente.Should().BeTrue();
    }
}
