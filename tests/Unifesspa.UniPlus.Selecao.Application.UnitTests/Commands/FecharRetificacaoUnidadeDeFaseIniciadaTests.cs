namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static DublesDoCongelamentoDeTeste;
using static PublicacaoDeTeste;

/// <summary>
/// A troca de unidade e a remoção aceitas na edição do cronograma, quando a fase já começou antes do fechamento da
/// retificação, são recusadas no fechamento: a unidade e a presença que valem são as da versão publicada vigente,
/// conferidas no instante do fechamento (UNI-REQ-0332, UNI-REQ-0364).
/// </summary>
public sealed class FecharRetificacaoUnidadeDeFaseIniciadaTests
{
    private const string CodigoDaRecusa = "FaseCronograma.UnidadeDeFaseIniciadaNaoMuda";

    [Fact(DisplayName = "Fechar a retificação recusa a unidade trocada numa fase que já começou, e a sessão editorial permanece aberta")]
    public async Task Fechar_FaseIniciadaComUnidadeTrocada_Recusa()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoComRetificacaoAberta();
        FasesIniciadas iniciadas = FaseIniciadaComAUnidade(processo, UnidadesDeTeste.Cga.OrigemId);

        (Result resposta, IEnumerable<object> eventos) = await FecharAsync(processo, versaoAtual, iniciadas);

        resposta.Error!.Code.Should().Be(CodigoDaRecusa);
        eventos.Should().BeEmpty();
        processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação não recusa a fase iniciada que mantém a unidade publicada")]
    public async Task Fechar_FaseIniciadaComAMesmaUnidade_NaoRecusaPelaUnidade()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoComRetificacaoAberta();
        FasesIniciadas iniciadas = FaseIniciadaComAUnidade(processo, UnidadesDeTeste.Ceps.OrigemId);

        (Result resposta, _) = await FecharAsync(processo, versaoAtual, iniciadas);

        resposta.Error?.Code.Should().NotBe(CodigoDaRecusa);
    }

    [Fact(DisplayName = "Fechar a retificação recusa a remoção de uma fase que começou depois da edição, e a sessão editorial permanece aberta")]
    public async Task Fechar_FaseIniciadaRemovidaDoRascunho_Recusa()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoComRetificacaoAberta();
        FasesIniciadas iniciadas = FaseIniciadaForaDoRascunho();

        (Result resposta, IEnumerable<object> eventos) = await FecharAsync(processo, versaoAtual, iniciadas);

        resposta.Error!.Code.Should().Be("FaseCronograma.FaseIniciadaNaoSaiDoCronograma");
        eventos.Should().BeEmpty();
        processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação não recusa a fase iniciada que continua no rascunho")]
    public async Task Fechar_FaseIniciadaMantidaNoRascunho_NaoRecusaPelaRemocao()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoComRetificacaoAberta();
        Guid origem = processo.CronogramaFases.Single().FaseCanonicaOrigemId;
        FasesIniciadas iniciadas = new(
            [KeyValuePair.Create<Guid, Guid?>(origem, UnidadesDeTeste.Ceps.OrigemId)], [KeyValuePair.Create(origem, "RESULTADO_FINAL")]);

        (Result resposta, _) = await FecharAsync(processo, versaoAtual, iniciadas);

        resposta.Error?.Code.Should().NotBe("FaseCronograma.FaseIniciadaNaoSaiDoCronograma");
    }

    [Fact(DisplayName = "Fechar a retificação recusa a remoção de uma etapa que começou depois da edição")]
    public async Task Fechar_EtapaIniciadaRemovidaDoRascunho_Recusa()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoComRetificacaoAberta();
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(Guid.CreateVersion7(), "Heteroidentificação", NaVersaoVigente: true)]);

        (Result resposta, IEnumerable<object> eventos) = await FecharAsync(processo, versaoAtual, iniciadas);

        resposta.Error!.Code.Should().Be("FaseCronograma.EtapaIniciadaNaoSaiDoCronograma");
        eventos.Should().BeEmpty();
        processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    private static FasesIniciadas FaseIniciadaForaDoRascunho()
    {
        Guid origem = Guid.CreateVersion7();
        return new([KeyValuePair.Create<Guid, Guid?>(origem, UnidadesDeTeste.Ceps.OrigemId)], [KeyValuePair.Create(origem, "HABILITACAO")]);
    }

    private static (ProcessoSeletivo Processo, VersaoConfiguracao VersaoAtual) ProcessoComRetificacaoAberta()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        processo.AbrirRetificacao(
            "Correção do prazo", versaoAtual, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", Agora)
            .IsSuccess.Should().BeTrue();
        processo.DequeueDomainEvents();

        return (processo, versaoAtual);
    }

    private static FasesIniciadas FaseIniciadaComAUnidade(ProcessoSeletivo processo, Guid unidadePublicada) =>
        new([KeyValuePair.Create<Guid, Guid?>(processo.CronogramaFases.Single().FaseCanonicaOrigemId, unidadePublicada)]);

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> FecharAsync(
        ProcessoSeletivo processo, VersaoConfiguracao versaoAtual, FasesIniciadas iniciadas)
    {
        IProcessoSeletivoRepository repositorio = RepositorioDoProcesso(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        return ServicosDoCongelamentoDeTeste.Para(processo)
            .ComRepositorio(repositorio)
            .ComCanonicalizer(CanonicalizerSubstituto())
            .ComCalendario(Substitute.For<ICalendarioVigenteReader>())
            .ComFasesIniciadas(FasesIniciadasDeTeste.Com(iniciadas))
            .FecharRetificacaoAsync(new FecharRetificacaoCommand(
                processo.Id, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga));
    }
}
