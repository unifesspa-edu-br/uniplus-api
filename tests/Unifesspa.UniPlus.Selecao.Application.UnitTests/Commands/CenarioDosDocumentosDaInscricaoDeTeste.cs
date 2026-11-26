namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Processo com a versão congelada e três exigências documentais (duas da fase da inscrição, uma de
/// outra fase), com as portas substituídas, para os testes dos passos do envio de documento da inscrição.
/// </summary>
internal sealed class CenarioDosDocumentosDaInscricaoDeTeste
{
    private const string Grupo = "DEPENDENTES";

    private readonly ICertameDivulgadoRepository _certame;

    public CenarioDosDocumentosDaInscricaoDeTeste()
    {
        _certame = DivulgacaoDeTeste.Certame(ProcessoId);
        (VersaoConfiguracao versao, ConfiguracaoPublicada conforme) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        FaseCronograma faseDaInscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma outraFase = Fase(2, "RESULTADO_FINAL", coletaInscricao: false);
        Laudo = Exigencia("LAUDO", faseDaInscricao.Id);
        Certidao = Exigencia("CERTIDAO", faseDaInscricao.Id);
        DeOutraFase = Exigencia("RESULTADO", outraFase.Id);
        Versao = versao;
        Configuracao = ConfiguracaoComDocumentos(conforme, faseDaInscricao, outraFase);
        DivulgacaoDeTeste.ConfigurarVersao(Processos, Codecs, versao, Configuracao);
        ConfigurarRepositorios();
    }

    public Guid ProcessoId { get; } = Guid.CreateVersion7();
    public Cpf Dono { get; } = PersonasDeTeste.KevinCarvalho;
    public Guid VersaoId => Versao.Id;
    public VersaoConfiguracao Versao { get; }
    public ConfiguracaoPublicada Configuracao { get; }
    public DocumentoExigido Laudo { get; }
    public DocumentoExigido Certidao { get; }
    public DocumentoExigido DeOutraFase { get; }
    public IInscricaoRepository Inscricoes { get; } = Substitute.For<IInscricaoRepository>();
    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();
    public IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();
    public IDocumentoDaInscricaoRepository Documentos { get; } = Substitute.For<IDocumentoDaInscricaoRepository>();
    public IArquivoDoCandidatoStorage Armazenamento { get; } = Substitute.For<IArquivoDoCandidatoStorage>();
    public IUnitOfWork UnidadeDeTrabalho { get; } = Substitute.For<IUnitOfWork>();

    internal static DocumentoExigido Exigencia(string codigo, Guid faseId = default) => DocumentoExigidoDeTeste.Novo()
        .NaFase(faseId == Guid.Empty ? Guid.CreateVersion7() : faseId)
        .ComTipo(codigo, codigo, "PESSOAL")
        .Facultativa()
        .ComFormatos(FormatosPermitidos.Criar(false, [("PDF", null)]).Value!)
        .Criar().Value!;

    public Inscricao Rascunho(params string[] dependentes)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            [Grupo] = [.. dependentes.Select(id => new OcorrenciaDeGrupoInscricao(id, new Dictionary<string, JsonElement>(StringComparer.Ordinal)))],
        };
        Inscricao inscricao = Inscricao.Reidratar(Guid.CreateVersion7(), ProcessoId,
            TitularDaInscricao.Reidratar(Dono, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Rascunho, null, CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioDaInscricao.Reidratar(VersaoId, PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(StringComparer.Ordinal), grupos, []),
                new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)), 3, new DatasDeGravacaoDaInscricao(default, null));
        Inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns(inscricao);
        return inscricao;
    }

    public IUserContext Usuario(string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(Dono.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    public Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> Iniciar(
        DocumentoExigido exigencia, string? ocorrenciaId = null, Guid? apresentacaoId = null, DateTimeOffset? agora = null) =>
        IniciarEnvioDeDocumentoDaInscricaoCommandHandler.Handle(new IniciarEnvioDeDocumentoDaInscricaoCommand(ProcessoId, exigencia.Id, ocorrenciaId, apresentacaoId, "application/pdf", 2048),
            Acesso(relogio: new RelogioFixo(agora ?? DivulgacaoDeTeste.DentroDoPeriodo)),
            new InicioDoEnvioDeDocumento(new VersaoDaInscricaoResolver(_certame, Processos, Codecs),
                new ContextoDaVersaoFixadaResolver(Processos, Codecs), Inscricoes, Documentos, Armazenamento, new RelogioFixo(agora ?? DivulgacaoDeTeste.DentroDoPeriodo)),
            UnidadeDeTrabalho, CancellationToken.None);

    public Task<(Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens)> Confirmar(
        Guid arquivoId, IUserContext? usuario = null, string etag = "\"etag-do-put\"") =>
        ConfirmarEnvioDeDocumentoDaInscricaoCommandHandler.Handle(new ConfirmarEnvioDeDocumentoDaInscricaoCommand(ProcessoId, arquivoId, etag),
            Acesso(usuario), Confirmacao(), UnidadeDeTrabalho, CancellationToken.None);

    public AcessoAoDocumentoDaInscricao Acesso(IUserContext? usuario = null, TimeProvider? relogio = null) =>
        new(Inscricoes, Documentos, usuario ?? Usuario(),
            new PeriodoDaInscricaoResolver(_certame, Processos, Codecs, DivulgacaoDeTeste.Fuso()), relogio ?? DivulgacaoDeTeste.Relogio);

    public ConfirmacaoDoEnvioDeDocumento Confirmacao() =>
        new(Inscricoes, Documentos, new DocumentosEnviadosDaInscricao(Documentos),
            Options.Create(new ReconciliacaoDaVerificacaoOptions()), DivulgacaoDeTeste.Relogio);

    public Task<Result> Remover(Guid arquivoId) =>
        RemoverArquivoDaInscricaoCommandHandler.Handle(new RemoverArquivoDaInscricaoCommand(ProcessoId, arquivoId), Acesso(),
            new RemocaoDoArquivoDaInscricao(Inscricoes, Documentos, DivulgacaoDeTeste.Relogio), UnidadeDeTrabalho, CancellationToken.None);

    public ArquivoDaApresentacao ArquivoPendente(Inscricao inscricao, TimeProvider? relogio = null)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricao.Id, Laudo, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricao.Id, Laudo, "application/pdf", 2048, relogio ?? DivulgacaoDeTeste.Relogio, PrazosDoArquivoEnviado.Envio).Value!;
        Documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);
        Documentos.ObterApresentacaoAsync(arquivo.ApresentacaoId, Arg.Any<CancellationToken>()).Returns(apresentacao);
        Documentos.ListarApresentacoesDaInscricaoAsync(inscricao.Id, Arg.Any<CancellationToken>()).Returns([apresentacao]);
        Documentos.ListarArquivosDaInscricaoAsync(inscricao.Id, Arg.Any<CancellationToken>()).Returns([arquivo]);
        return arquivo;
    }

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: coletaInscricao, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            ordem, Inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero),
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(codigo, PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private ConfiguracaoPublicada ConfiguracaoComDocumentos(ConfiguracaoPublicada conforme, FaseCronograma faseDaInscricao, FaseCronograma outraFase)
    {
        RegraDeComprovacao regraDeLaudo = RegraDeComprovacao.CriarRegraDeDocumento(Laudo, 0).Value!;
        RegraDeComprovacao regraDeCertidao = RegraDeComprovacao.CriarRegraDeDocumento(Certidao, 1, grupoDoFormularioParaRepeticao: Grupo).Value!;
        RegraDeComprovacao regraDeOutraFase = RegraDeComprovacao.CriarRegraDeDocumento(DeOutraFase, 2).Value!;
        return new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [faseDaInscricao, outraFase]),
            new AvaliacaoDeclarada(conforme.Avaliacao.Etapas, conforme.Avaliacao.CriteriosDesempate, conforme.Avaliacao.Classificacao),
            new VagasDeclaradas(conforme.Vagas.DistribuicaoVagas, 1, conforme.Vagas.Fada is not null,
                conforme.Vagas.Fada, null, conforme.Vagas.AtendimentoEspecializado),
            new ExigenciasDeclaradas([Laudo, Certidao, DeOutraFase], [regraDeLaudo, regraDeCertidao, regraDeOutraFase]),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta(conforme.Coleta.Itens, conforme.Coleta.GruposColetados), conforme.Coleta.GruposColetados, conforme.Coleta.Formularios, [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);
    }

    private void ConfigurarRepositorios()
    {
        Armazenamento.GerarUrlUploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://armazenamento.exemplo/envio");
        Inscricoes.TentarAvancarRevisaoAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        Documentos.TentarReivindicarConfirmacaoAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true);
    }
}
