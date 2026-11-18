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
    private static ITipoEtapaReader ReaderDoTipoQueExige(bool exigeCorrecaoIndependente) =>
        ReaderDoTipo(TipoNoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", exigeCorrecaoIndependente: exigeCorrecaoIndependente));

    private static EtapaProcessoInput EtapaObjetiva(Guid? id = null, CaraterEtapa carater = CaraterEtapa.Classificatoria) =>
        new("Prova Objetiva", carater, TipoProvaObjetivaOrigemId, 1m, null, 1, id, FormaDeAvaliacao: FormaDeAvaliacaoCodigo.Objetiva, Produtos: [], Bancas: [], Recursos: []);

    [Fact(DisplayName = "Etapa nova de tipo que exige correção independente, em avaliação objetiva, é recusada no campo da forma")]
    public async Task EtapaNova_FormaObjetivaNoTipoQueExige_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result result = await Executar(
            new DefinirEtapasCommand(processo.Id, [EtapaObjetiva()]),
            RepositorioCom(processo), unitOfWork, ReaderDoTipoQueExige(exigeCorrecaoIndependente: true));

        result.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].formaDeAvaliacao");
        result.Errors[0].Error.Code.Should().Be(EtapaProcessoErrorCodes.FormaObjetivaEmTipoQueExigeCorrecaoIndependente);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Etapa nova de tipo que não exige correção independente aceita a avaliação objetiva e congela o sinalizador desligado")]
    public async Task EtapaNova_TipoQueNaoExige_AceitaECongelaDesligado()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await ExecutarSobre(processo, ReaderDoTipoQueExige(exigeCorrecaoIndependente: false), EtapaObjetiva());

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.ExigeCorrecaoIndependente.Should().BeFalse();
    }

    [Fact(DisplayName = "Mudar o caráter de etapa existente relê o tipo e congela o sinalizador que o cadastro declara hoje")]
    public async Task CaraterAlterado_RegravaOSinalizadorDeCorrecaoIndependente()
    {
        TipoEtapaSnapshot semExigir = TipoCongelado(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true);
        EtapaProcesso existente = EtapaDoProcesso("Prova Objetiva", CaraterEtapa.Classificatoria, 1, semExigir, 1m);
        ProcessoSeletivo processo = ProcessoComAsEtapas(existente);
        EtapaProcessoInput humana = EtapaObjetiva(existente.Id, CaraterEtapa.Ambas) with { FormaDeAvaliacao = FormaDeAvaliacaoCodigo.Humana };

        Result result = await ExecutarSobre(processo, ReaderDoTipoQueExige(exigeCorrecaoIndependente: true), humana);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.ExigeCorrecaoIndependente.Should().BeTrue();
    }
}
