namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// A gravação do rascunho aceita a banca de 2ª instância prevista numa fase ou numa etapa em que nenhum recurso admite instância
/// superior: a recusa fica para o momento de congelar, porque a banca da fase depende dos recursos das etapas, gravados em outra
/// operação, e o rascunho aceita declaração parcial.
/// </summary>
public sealed class GravacaoDaBancaDaSegundaInstanciaTests
{
    private static readonly TipoBancaView TipoDeSegundaInstancia = new(
        Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia, "Banca de análise de recursos de 2ª instância", null, null,
        PapelDaBanca.JulgaASegundaInstancia, false, false, []);

    [Fact(DisplayName = "Gravar a fase sem recurso e com banca de 2ª instância é aceito, e a banca guarda o papel de 2ª instância")]
    public async Task DefinirFases_BancaDeSegundaInstanciaSemRecurso_Aceita()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        cenario.Processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        cenario.TipoBancaReader.ObterPorIdAsync(TipoDeSegundaInstancia.Id, Arg.Any<CancellationToken>()).Returns(TipoDeSegundaInstancia);
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with
        {
            BancasRequeridas = [new BancaRequeridaInput(TipoDeSegundaInstancia.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso())],
            RegraRecurso = null,
        };

        Result resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Single().BancasRequeridas.Should().ContainSingle()
            .Which.Tipo.Papel.Should().Be(PapelDaBanca.JulgaASegundaInstancia);
    }

    [Fact(DisplayName = "Gravar a etapa sem recurso e com banca de 2ª instância é aceito, e a banca guarda o papel de 2ª instância")]
    public async Task DefinirEtapas_BancaDeSegundaInstanciaSemRecurso_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoBancaReader tiposDeBanca = Substitute.For<ITipoBancaReader>();
        tiposDeBanca.ObterPorIdAsync(TipoDeSegundaInstancia.Id, Arg.Any<CancellationToken>()).Returns(TipoDeSegundaInstancia);
        EtapaProcessoInput etapa = new(
            "Prova oral", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, Produtos: [], Recursos: [],
            Bancas: [new BancaDaEtapaInput(TipoDeSegundaInstancia.Id, RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso())]);

        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, [etapa]), RepositorioCom(processo), Substitute.For<IUnitOfWork>(),
            cadastros: new CadastrosDeApoioDeDefinirEtapas(TiposDeBanca: tiposDeBanca));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Single().Bancas.Should().ContainSingle().Which.Tipo.Papel.Should().Be(PapelDaBanca.JulgaASegundaInstancia);
    }
}
