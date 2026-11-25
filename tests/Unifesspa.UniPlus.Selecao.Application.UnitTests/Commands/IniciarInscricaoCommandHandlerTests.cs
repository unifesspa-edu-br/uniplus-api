namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Início da inscrição (issue #1858): a resolução da versão divulgada (UNI-REQ-0084, mesmo
/// caminho de <c>ObterFormularioApresentadoQueryHandler</c>).
/// A tradução da colisão do índice único (duas requisições simultâneas) exige uma
/// <c>DbUpdateException</c> real, compatível por reflection — provada contra Postgres real em
/// <c>InscricaoAtivaDoCpfTests.IndiceUnico_RecusaDuasInscricoesDoMesmoCpfNoMesmoProcesso</c>, não
/// aqui: um dublê não reproduziria o shape de <c>Npgsql.PostgresException</c> com fidelidade.
/// </summary>
public sealed class IniciarInscricaoCommandHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid AtoDaDivulgacao = Guid.CreateVersion7();
    private static readonly DateTimeOffset DataHoraFixa = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<InscricaoDto> resultado = await new FluxoDaInscricaoDeTeste
        {
            Inscricoes = inscricaoRepository,
            Relogio = TimeProvider.System,
            Usuario = userContext,
        }.IniciarAsync(new IniciarInscricaoCommand(ProcessoId), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterAtivaPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já existente para o CPF e o processo: devolve a existente, mesmo fora do período, sem gravar nada")]
    public async Task Handle_InscricaoJaExiste_DevolveAExistenteSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (Cenario cenario, Inscricao existente) = ComInscricaoAtiva(cpf);
        IInscricaoRepository inscricaoRepository = cenario.Inscricoes;

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<InscricaoDto> resultado = await Iniciar(cenario, cpf, new RelogioFixo(DentroDoPeriodo.AddYears(4)), unitOfWork: unitOfWork);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Id.Should().Be(existente.Id);
        await inscricaoRepository.DidNotReceive().AdicionarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição existente de outra conta com o mesmo CPF: recusa como dono divergente, sem devolvê-la")]
    public async Task Handle_InscricaoDeOutroContaId_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Cenario cenario = MontarCriacao(cpf);
        Inscricao existente = Inscricao.Iniciar(
            ProcessoId, cpf, "conta-original", "https://issuer", cenario.Versao.Id).Value!;

        IInscricaoRepository inscricaoRepository = cenario.Inscricoes;
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        Result<InscricaoDto> resultado = await Iniciar(cenario, cpf, new RelogioFixo(DentroDoPeriodo), "conta-outro");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
    }

    [Fact(DisplayName = "Inscrição cancelada de outra conta com o mesmo CPF: iniciar é recusado como dono divergente, e a mesma conta se inscreve de novo")]
    public async Task Handle_CanceladaDeOutroContaId_RecusaEMesmoContaIdInscreveDeNovo()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Cenario cenario = MontarCriacao(cpf);
        Inscricao cancelada = Cancelada(cpf, cenario.Versao.Id);
        cenario.Inscricoes.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[cancelada]);

        Result<InscricaoDto> deOutro = await Iniciar(cenario, cpf, new RelogioFixo(DentroDoPeriodo), "conta-outro");
        deOutro.Error!.Code.Should().Be(InscricaoErrorCodes.DonoDivergente);
        await cenario.Inscricoes.DidNotReceive().AdicionarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());

        Result<InscricaoDto> domesmo = await Iniciar(cenario, cpf, new RelogioFixo(DentroDoPeriodo));
        domesmo.IsSuccess.Should().BeTrue(domesmo.Error?.Message);
        await cenario.Inscricoes.Received(1).AdicionarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo sem divulgação: não encontrado, sem tentar gravar")]
    public async Task Handle_ProcessoSemDivulgacao_RetornaNaoEncontrado()
    {
        Result<InscricaoDto> resultado = await new FluxoDaInscricaoDeTeste
        {
            Usuario = UsuarioComCpf(PersonasDeTeste.KevinCarvalho),
        }.IniciarAsync(new IniciarInscricaoCommand(ProcessoId), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Cria a inscrição fixando a versão divulgada")]
    public async Task Handle_CriaNova_FixaVersaoDivulgada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Cenario cenario = MontarCriacao(cpf);
        Inscricao? gravada = null;
        cenario.Inscricoes.WhenForAnyArgs(r => r.AdicionarAsync(default!, default))
            .Do(call => gravada = call.Arg<Inscricao>());

        Result<InscricaoDto> resultado = await Iniciar(cenario, cpf, new RelogioFixo(DentroDoPeriodo));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Formulario.VersaoFormularioId.Should().Be(cenario.Versao.Id);
        gravada!.Formulario.VersaoFormularioId.Should().Be(cenario.Versao.Id);
    }

    [Fact(DisplayName = "Depois do fim do período, criar a inscrição é recusado sem gravar nada")]
    public async Task Handle_CriaNovaForaDoPeriodo_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Cenario cenario = MontarCriacao(cpf);

        Result<InscricaoDto> resultado = await Iniciar(cenario, cpf, new RelogioFixo(DentroDoPeriodo.AddMonths(1)));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await cenario.Inscricoes.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    private static (Cenario, Inscricao) ComInscricaoAtiva(Cpf cpf)
    {
        Cenario cenario = MontarCriacao(cpf);
        Inscricao existente = Inscricao.Iniciar(ProcessoId, cpf, "conta-1", "https://issuer", cenario.Versao.Id).Value!;
        cenario.Inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        return (cenario, existente);
    }

    private static Inscricao Cancelada(Cpf cpf, Guid versaoId) =>
        Inscricao.Reidratar(Guid.CreateVersion7(), ProcessoId,
            TitularDaInscricao.Reidratar(cpf, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Cancelada, "000123", CancelamentoDaInscricao.Reidratar(DataHoraFixa, "conta-1", "a pedido do candidato")),
            FormularioDaInscricao.Reidratar(versaoId, PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 2, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

    private static readonly DateTimeOffset DentroDoPeriodo = DivulgacaoDeTeste.DentroDoPeriodo;

    private sealed record Cenario(
        IInscricaoRepository Inscricoes,
        ICertameDivulgadoRepository Divulgados,
        IProcessoSeletivoRepository Processos,
        IRegistroCodecsEnvelope Codecs,
        VersaoConfiguracao Versao);

    private static Cenario MontarCriacao(Cpf cpf)
    {
        ConfiguracaoPublicada configuracao = ConfiguracaoComDocumentoNoGrupo(ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Inscrição"));
        VersaoConfiguracao versao = VersaoDoCenario();
        EnvelopeReidratado envelope = new(
            configuracao,
            DadosEdital.Criar("001/2026", DivulgacaoDeTeste.InicioDoPeriodo, DivulgacaoDeTeste.FimDoPeriodo, Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null, dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada());

        return new Cenario(InscricoesSemAtiva(cpf), DivulgadosCom(DivulgadoDoCenario()), ProcessosCom(versao), CodecsQueLem(versao, envelope), versao);
    }

    private static FatoColetado Fato(string codigo, int ordem, string rotulo, Obrigatoriedade obrigatoriedade, string classificacao) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ComRotulo(rotulo)
            .DeTexto("LIVRE")
            .ComObrigatoriedade(obrigatoriedade)
            .ComClassificacao(classificacao)
            .Construir();

    private static GrupoColetado GrupoComDocumento() => GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 0)
        .NaSecao(null)
        .ComOcorrencias(0, null)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComCampos(Fato("RG_NUMERO_MEMBRO", 0, "RG do membro", Obrigatoriedade.Sempre, "IDENTIFICADOR"))
        .DaFinalidade(FinalidadeFormulario.Inscricao)
        .Construir();

    private static ConfiguracaoPublicada ConfiguracaoComDocumentoNoGrupo(ProcessoSeletivo processoBase) => new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processoBase.CronogramaFases]),
            new AvaliacaoDeclarada([.. processoBase.Etapas], [.. processoBase.CriteriosDesempate], processoBase.Classificacao!),
            new VagasDeclaradas([.. processoBase.DistribuicaoVagas], 1, processoBase.BonusRegional is not null,
                processoBase.BonusRegional, null, processoBase.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processoBase.DocumentosExigidos], []),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta([
            Fato("RG_NUMERO", 0, "RG", Obrigatoriedade.Sempre, "IDENTIFICADOR"),
            Fato("NOME_SOCIAL", 1, "Nome social", Obrigatoriedade.Nunca, "PESSOAL"),
        ], [GrupoComDocumento()]), [GrupoComDocumento()], [.. processoBase.Formularios], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

    private static VersaoConfiguracao VersaoDoCenario() => VersaoConfiguracao.Abrir(
        ProcessoId,
        new DocumentoCongelado(Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataDeReferenciaDosDados":null}}"""), "0.0.22", "canonical-json/sha256@v1", new string('a', 64)),
        Guid.CreateVersion7(), "user-conta-1", DataHoraFixa);

    private static CertameDivulgado DivulgadoDoCenario() => CertameDivulgado.Criar(
        ProcessoId, numeroVersao: 1, AtoDaDivulgacao, new string('a', 64), versaoProjecao: "1",
        new FacetasDoCertameDivulgado("certame-inscricao", "Certame", "001/2026", ["AC"], DataHoraFixa, DataHoraFixa.AddDays(30)),
        """{"nome":"documento"}""", DataHoraFixa);

    private static IInscricaoRepository InscricoesSemAtiva(Cpf cpf)
    {
        IInscricaoRepository inscricoes = Substitute.For<IInscricaoRepository>();
        inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        return inscricoes;
    }

    private static ICertameDivulgadoRepository DivulgadosCom(CertameDivulgado divulgado)
    {
        ICertameDivulgadoRepository divulgados = Substitute.For<ICertameDivulgadoRepository>();
        divulgados.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        return divulgados;
    }

    private static IProcessoSeletivoRepository ProcessosCom(VersaoConfiguracao versao)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([versao]);
        processos.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        return processos;
    }

    private static IRegistroCodecsEnvelope CodecsQueLem(VersaoConfiguracao versao, EnvelopeReidratado envelope)
    {
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        codecs.LeAVersao("0.0.22").Returns(true);
        codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelope));

        return codecs;
    }

    private static Task<Result<InscricaoDto>> Iniciar(Cenario cenario, Cpf cpf, TimeProvider relogio, string contaId = "conta-1", IUnitOfWork? unitOfWork = null) =>
        new FluxoDaInscricaoDeTeste
        {
            Inscricoes = cenario.Inscricoes,
            Certames = cenario.Divulgados,
            Processos = cenario.Processos,
            Codecs = cenario.Codecs,
            Relogio = relogio,
            Usuario = UsuarioComCpf(cpf, contaId),
            UnitOfWork = unitOfWork ?? Substitute.For<IUnitOfWork>(),
        }.IniciarAsync(new IniciarInscricaoCommand(ProcessoId), CancellationToken.None);

    private static IResolvedorFusoInstitucional Fuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    private static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(contaId);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }
}
