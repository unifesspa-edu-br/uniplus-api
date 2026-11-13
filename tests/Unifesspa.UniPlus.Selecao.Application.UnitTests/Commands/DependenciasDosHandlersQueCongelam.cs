namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.DublesDoCongelamentoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.RegrasLegaisDeTeste;

/// <summary>
/// As dependências dos três handlers que congelam uma versão (Publicar, Retificar e FecharRetificacao) e a
/// chamada a cada um. Os cadastros vêm com o que a suíte usa; o teste troca só o que quer descrever. A lista
/// de dependências de cada handler fica aqui, de modo que uma mudança de assinatura se faz num lugar só.
/// </summary>
internal sealed class DependenciasDosHandlersQueCongelam
{
    private DependenciasDosHandlersQueCongelam(ProcessoSeletivo processo)
    {
        Processo = processo;
        Repositorio = RepositorioDoProcesso(processo);
    }

    public ProcessoSeletivo Processo { get; }

    public IProcessoSeletivoRepository Repositorio { get; }

    public IAtoDoProcessoRepository Atos { get; set; } = Substitute.For<IAtoDoProcessoRepository>();

    public ISnapshotPublicacaoCanonicalizer Canonicalizer { get; set; } = CanonicalizerSubstituto();

    public ITipoAtoPublicadoReader TiposDeAto { get; set; } = TipoDeAtoReader();

    public IObrigatoriedadeLegalRepository ObrigatoriedadesLegais { get; set; } = RepositorioCom();

    public IModalidadeReader Modalidades { get; set; } = CadastrosVivos.Modalidades();

    public ITipoDocumentoReader TiposDocumento { get; set; } = CadastrosVivos.TiposDocumento();

    public IFatoCandidatoReader CatalogoDeFatos { get; set; } = CadastrosVivos.CatalogoDeFatos();

    public IResolvedorDaUnidadeResponsavel Unidades { get; set; } = ResolvedorDeUnidadeDeTeste.ComAsUnidadesDeTeste();

    public IRegistroCodecsEnvelope Codecs { get; set; } = CodecsDeTeste.QueReidrataSemTiposDeAto();

    public static DependenciasDosHandlersQueCongelam Para(ProcessoSeletivo processo) => new(processo);

    /// <summary>O processo já publicado, com a versão publicada como a vigente do repositório.</summary>
    public static DependenciasDosHandlersQueCongelam ParaProcessoPublicado()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();

        return ComVersaoVigente(processo, versaoAtual);
    }

    /// <summary>O processo publicado, com a sessão editorial de retificação aberta.</summary>
    public static DependenciasDosHandlersQueCongelam ParaProcessoEmRetificacao()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        AbrirRetificacao(processo, versaoAtual);

        return ComVersaoVigente(processo, versaoAtual);
    }

    public Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync() =>
        PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(Processo.Id, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            Repositorio, RepositorioDeDocumento(Processo.Id), Substitute.For<IRascunhoDePublicacaoRepository>(), Atos,
            Canonicalizer, new ResolvedorFusoDeTeste(), Substitute.For<ISelecaoUnitOfWork>(), UsuarioAutenticado(),
            TiposDeAto, Substitute.For<IVagaDeLinhagemReader>(), ObrigatoriedadesLegais, Modalidades, TiposDocumento,
            CadastrosVivos.TiposEtapa(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), CatalogoDeFatos,
            CalendarioVigenteReaderDeTeste.SemVigente(), Unidades, new RelogioFixo(Agora), CancellationToken.None);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync() =>
        RetificarProcessoSeletivoCommandHandler.Handle(
            new RetificarProcessoSeletivoCommand(
                Processo.Id, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            Repositorio, RepositorioDeDocumento(Processo.Id), Substitute.For<IRascunhoDePublicacaoRepository>(), Atos,
            Canonicalizer, new ResolvedorFusoDeTeste(), Substitute.For<ISelecaoUnitOfWork>(), UsuarioAutenticado(),
            TiposDeAto, Substitute.For<IVagaDeLinhagemReader>(), ObrigatoriedadesLegais, Modalidades, TiposDocumento,
            CadastrosVivos.TiposEtapa(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), CatalogoDeFatos,
            CalendarioVigenteReaderDeTeste.SemVigente(), Unidades, Codecs, new RelogioFixo(Agora), CancellationToken.None);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> FecharRetificacaoAsync() =>
        FecharRetificacaoCommandHandler.Handle(
            new FecharRetificacaoCommand(
                Processo.Id, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga),
            Repositorio, RepositorioDeDocumento(Processo.Id), Substitute.For<IRascunhoDePublicacaoRepository>(), Atos,
            Canonicalizer, new ResolvedorFusoDeTeste(), Substitute.For<ISelecaoUnitOfWork>(), UsuarioAutenticado(),
            TiposDeAto, Substitute.For<IVagaDeLinhagemReader>(), ObrigatoriedadesLegais, Modalidades, TiposDocumento,
            CadastrosVivos.TiposEtapa(), CadastrosVivos.TiposDeficiencia(), CadastrosVivos.RegrasDesempate(), CatalogoDeFatos,
            CalendarioVigenteReaderDeTeste.SemVigente(), Unidades, Codecs, FasesIniciadasDeTeste.Nenhuma(), new RelogioFixo(Agora),
            CancellationToken.None);

    private static DependenciasDosHandlersQueCongelam ComVersaoVigente(ProcessoSeletivo processo, VersaoConfiguracao versaoAtual)
    {
        DependenciasDosHandlersQueCongelam dependencias = Para(processo);
        dependencias.Repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        return dependencias;
    }
}
