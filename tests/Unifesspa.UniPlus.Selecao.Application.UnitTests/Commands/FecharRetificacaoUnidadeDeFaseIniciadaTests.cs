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

using static ConformidadeLegalGateTests;

/// <summary>
/// A troca de unidade aceita na edição do cronograma, quando a fase já começou antes do fechamento da retificação, é
/// recusada no fechamento: a unidade que vale é a da versão publicada vigente, conferida no instante do fechamento (UNI-REQ-0332).
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

        return FecharRetificacaoCommandHandler.Handle(
            new FecharRetificacaoCommand(
                processo.Id, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga),
            repositorio, RepositorioDeDocumento(processo.Id), Substitute.For<IRascunhoDePublicacaoRepository>(),
            Substitute.For<IAtoDoProcessoRepository>(), CanonicalizerSubstituto(), new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(), UsuarioAutenticado(), TipoDeAtoReader(), Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(), CadastrosVivos.Modalidades(), CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(),
            CadastrosVivos.CatalogoDeFatos(), Substitute.For<ICalendarioVigenteReader>(), ResolvedorDeUnidadeDeTeste.ComAsUnidadesDeTeste(),
            CodecsDeTeste.QueReidrataSemTiposDeAto(), FasesIniciadasDeTeste.Com(iniciadas), new ConformidadeLegalGateTests.RelogioFixo(Agora), CancellationToken.None);
    }
}
