namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.RegrasLegaisDeTeste;

/// <summary>A operação que congela uma versão da configuração.</summary>
public enum OperacaoQueCongela
{
    Publicar,
    Retificar,
    FecharRetificacao,
}

/// <summary>Um defeito que o processo ou os cadastros podem ter na hora de congelar, cada um com a recusa que lhe cabe.</summary>
public enum DefeitoDoCongelamento
{
    Documento,
    TipoDeAto,
    AtoJaRetificado,
    ProtecaoPendente,
    VersoesPublicadasIlegiveis,
    UnidadeExcluida,
    Cronograma,
    TiposDeAtoCongelados,
    Anexos,
    ConformidadeLegal,
    Motivos,
}

/// <summary>
/// Uma operação que congela, sobre um processo conforme, a que os testes acrescentam defeitos. A recusa que vence
/// quando dois defeitos coincidem é a do portão de maior prioridade.
/// </summary>
internal sealed class CenarioDeRecusaNoCongelamento
{
    private static readonly Dictionary<DefeitoDoCongelamento, Action<CenarioDeRecusaNoCongelamento>> Aplicadores = new()
    {
        [DefeitoDoCongelamento.Documento] = static c => c.Servicos.ComDocumentos(Substitute.For<IDocumentoAPublicarRepository>()),
        [DefeitoDoCongelamento.TipoDeAto] = static c => c.TipoDeAtoQueNaoCongela(),
        [DefeitoDoCongelamento.AtoJaRetificado] = static c => c.Servicos.Vagas.AtoJaFoiRetificadoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true),
        [DefeitoDoCongelamento.ProtecaoPendente] = static c => c.Servicos.ComTiposDocumento(TiposDeDocumentoComExigenciaPendente()),
        [DefeitoDoCongelamento.VersoesPublicadasIlegiveis] = static c => c.Servicos.Codecs.Capacidades.Returns([]),
        [DefeitoDoCongelamento.UnidadeExcluida] = static c => c.Servicos.ComUnidades(ResolvedorDeUnidadeDeTeste.Com()),
        [DefeitoDoCongelamento.Cronograma] = static c => c.Processo.DefinirEtapas([], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue(),
        [DefeitoDoCongelamento.TiposDeAtoCongelados] = static c => c.CatalogoSemOsTiposDeclarados(),
        [DefeitoDoCongelamento.Anexos] = static c => c.PedirUmAnexoQueNaoExiste(),
        [DefeitoDoCongelamento.ConformidadeLegal] = static c => c.Servicos.ComObrigatoriedadesLegais(RepositorioCom(RegraQueReprova())),
        [DefeitoDoCongelamento.Motivos] = static c => IsencaoDeTeste.HabilitarSemMotivos(c.Processo),
    };

    private static readonly Dictionary<DefeitoDoCongelamento, string> Codigos = new()
    {
        [DefeitoDoCongelamento.Documento] = ConferenciaDoDocumentoDoEdital.NaoEncontrado,
        [DefeitoDoCongelamento.TipoDeAto] = "ProcessoSeletivo.TipoDeAtoNaoCongelaConfiguracao",
        [DefeitoDoCongelamento.AtoJaRetificado] = "ProcessoSeletivo.AtoJaRetificado",
        [DefeitoDoCongelamento.ProtecaoPendente] = "ProcessoSeletivo.TipoDocumentoComProtecaoPendente",
        [DefeitoDoCongelamento.VersoesPublicadasIlegiveis] = "EnvelopeCodec.VersaoDesconhecida",
        [DefeitoDoCongelamento.UnidadeExcluida] = "FaseCronograma.UnidadeResponsavelNaoEncontrada",
        [DefeitoDoCongelamento.Cronograma] = "ProcessoSeletivo.AvaliacaoSemEtapa",
        [DefeitoDoCongelamento.TiposDeAtoCongelados] = "ProcessoSeletivo.TipoDeAtoSemVersaoVigente",
        [DefeitoDoCongelamento.Anexos] = "AnexoDoEdital.NaoEncontrado",
        [DefeitoDoCongelamento.ConformidadeLegal] = "ProcessoSeletivo.ConformidadeLegalInsuficiente",
        [DefeitoDoCongelamento.Motivos] = MotivoDeDecisaoErrorCodes.CoberturaInsuficiente,
    };

    private static readonly DefeitoDoCongelamento[] DefeitosDaPublicacao =
    [
        DefeitoDoCongelamento.Documento, DefeitoDoCongelamento.TipoDeAto, DefeitoDoCongelamento.ProtecaoPendente,
        DefeitoDoCongelamento.UnidadeExcluida, DefeitoDoCongelamento.Cronograma, DefeitoDoCongelamento.TiposDeAtoCongelados,
        DefeitoDoCongelamento.Anexos, DefeitoDoCongelamento.ConformidadeLegal, DefeitoDoCongelamento.Motivos,
    ];

    private static readonly DefeitoDoCongelamento[] DefeitosDaRetificacao =
    [
        DefeitoDoCongelamento.Documento, DefeitoDoCongelamento.TipoDeAto, DefeitoDoCongelamento.AtoJaRetificado,
        DefeitoDoCongelamento.ProtecaoPendente, DefeitoDoCongelamento.VersoesPublicadasIlegiveis, DefeitoDoCongelamento.UnidadeExcluida,
        DefeitoDoCongelamento.TiposDeAtoCongelados, DefeitoDoCongelamento.Anexos, DefeitoDoCongelamento.ConformidadeLegal,
        DefeitoDoCongelamento.Motivos,
    ];

    private static readonly DefeitoDoCongelamento[] DefeitosDoFechamento =
    [
        DefeitoDoCongelamento.Documento, DefeitoDoCongelamento.TipoDeAto, DefeitoDoCongelamento.AtoJaRetificado,
        DefeitoDoCongelamento.ProtecaoPendente, DefeitoDoCongelamento.UnidadeExcluida, DefeitoDoCongelamento.Cronograma,
        DefeitoDoCongelamento.TiposDeAtoCongelados, DefeitoDoCongelamento.Anexos, DefeitoDoCongelamento.ConformidadeLegal,
        DefeitoDoCongelamento.Motivos,
    ];

    private readonly OperacaoQueCongela _operacao;
    private IReadOnlyList<AnexoSolicitadoDoEdital>? _anexos;

    private CenarioDeRecusaNoCongelamento(OperacaoQueCongela operacao, ProcessoSeletivo processo, ServicosDoCongelamentoDeTeste servicos)
    {
        _operacao = operacao;
        Processo = processo;
        Servicos = servicos;
    }

    public ProcessoSeletivo Processo { get; }

    public ServicosDoCongelamentoDeTeste Servicos { get; }

    public static CenarioDeRecusaNoCongelamento Para(OperacaoQueCongela operacao) =>
        operacao == OperacaoQueCongela.Publicar ? ParaPublicacao() : ParaProcessoPublicado(operacao);

    /// <summary>Os defeitos que a operação tem como acrescentar, do portão de maior prioridade ao de menor.</summary>
    public static IReadOnlyList<DefeitoDoCongelamento> PorPrioridade(OperacaoQueCongela operacao) => operacao switch
    {
        OperacaoQueCongela.Publicar => DefeitosDaPublicacao,
        OperacaoQueCongela.Retificar => DefeitosDaRetificacao,
        _ => DefeitosDoFechamento,
    };

    private static CenarioDeRecusaNoCongelamento ParaPublicacao()
    {
        DependenciasDosHandlersQueCongelam publicacao = DependenciasDosHandlersQueCongelam.Para(ProcessoQueColetaOFato(comItemNovo: false));
        publicacao.Servicos.ComCatalogoDeFatos(CatalogoDoFatoColetado());

        return new(OperacaoQueCongela.Publicar, publicacao.Processo, publicacao.Servicos);
    }

    private static CenarioDeRecusaNoCongelamento ParaProcessoPublicado(OperacaoQueCongela operacao)
    {
        CenarioDeRetificacaoPublicada publicado = CenarioDeRetificacaoPublicada.Publicado();
        if (operacao == OperacaoQueCongela.FecharRetificacao)
        {
            publicado.AbrirSessao();
        }

        return new(operacao, publicado.Processo, publicado.Servicos);
    }

    public static string CodigoDa(DefeitoDoCongelamento defeito) => Codigos[defeito];

    public CenarioDeRecusaNoCongelamento Com(DefeitoDoCongelamento defeito)
    {
        Aplicadores[defeito](this);

        return this;
    }

    public Task<(Result Resposta, IEnumerable<object> Eventos)> ExecutarAsync() => _operacao switch
    {
        OperacaoQueCongela.Publicar => Servicos.PublicarAsync(
            new PublicarProcessoSeletivoCommand(Processo.Id, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Anexos: _anexos)),
        OperacaoQueCongela.Retificar => Servicos.RetificarAsync(
            new RetificarProcessoSeletivoCommand(
                Processo.Id, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Anexos: _anexos)),
        _ => Servicos.FecharRetificacaoAsync(
            new FecharRetificacaoCommand(
                Processo.Id, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga, Anexos: _anexos)),
    };

    /// <summary>O processo não saiu do estado em que estava: nenhum evento de publicação ficou no agregado.</summary>
    public void AfirmarQueNaoTransitou() => Processo.DequeueDomainEvents().Should().BeEmpty();

    private static IFatoCandidatoReader CatalogoDoFatoColetado()
    {
        FatoCandidatoView vivo = FatoDoCatalogo("BOOLEANO", "PESSOAL");
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[vivo, FatoNovoDoCatalogo(vivo)]));

        return catalogo;
    }

    private static ITipoDocumentoReader TiposDeDocumentoComExigenciaPendente()
    {
        ITipoDocumentoReader tipos = Substitute.For<ITipoDocumentoReader>();
        tipos.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns([CadastrosVivos.TipoDocumento("LAUDO_MEDICO") with { ProtecaoAprovada = false }]);

        return tipos;
    }

    private void TipoDeAtoQueNaoCongela() =>
        Servicos.TiposDeAto.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("EDITAL_ABERTURA", "Edital de abertura", false, false, false, false));

    private void CatalogoSemOsTiposDeclarados() =>
        Servicos.TiposDeAto.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);

    private void PedirUmAnexoQueNaoExiste() => _anexos = [new AnexoSolicitadoDoEdital(Guid.CreateVersion7(), null)];
}
