namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Início da inscrição (issue #1858): a resolução da versão divulgada (UNI-REQ-0084, mesmo
/// caminho de <c>ObterFormularioRenderizavelQueryHandler</c>).
/// A tradução da colisão do índice único (duas requisições simultâneas) exige uma
/// <c>DbUpdateException</c> real, compatível por reflection — provada contra Postgres real em
/// <c>InscricaoPersistenceTests.IndiceUnico_RecusaDuasInscricoesDoMesmoCpfNoMesmoProcesso</c>, não
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
        userContext.Cpf.Returns((string?)null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<InscricaoDto> resultado = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            inscricaoRepository,
            Substitute.For<ICertameDivulgadoRepository>(),
            Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IRegistroCodecsEnvelope>(),
            Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            TimeProvider.System,
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterAtivaPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já existente para o CPF e o processo: devolve a existente, mesmo fora do período, sem gravar nada")]
    public async Task Handle_InscricaoJaExiste_DevolveAExistenteSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Cenario cenario = MontarCriacao(cpf);
        Inscricao existente = Inscricao.Iniciar(
            ProcessoId, cpf, "sub-1", "https://issuer", cenario.Versao.Id).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            inscricaoRepository,
            DivulgacaoDeTeste.Certame(ProcessoId),
            cenario.Processos,
            cenario.Codecs,
            Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            new RelogioFixo(DentroDoPeriodo.AddYears(4)),
            unitOfWork,
            UsuarioComCpf(cpf),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Id.Should().Be(existente.Id);
        await inscricaoRepository.DidNotReceive().AdicionarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição existente de outro sub com o mesmo CPF: recusa como dono divergente, sem devolvê-la")]
    public async Task Handle_InscricaoDeOutroSub_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Cenario cenario = MontarCriacao(cpf);
        Inscricao existente = Inscricao.Iniciar(
            ProcessoId, cpf, "sub-original", "https://issuer", cenario.Versao.Id).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        Result<InscricaoDto> resultado = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            inscricaoRepository,
            Substitute.For<ICertameDivulgadoRepository>(),
            cenario.Processos,
            cenario.Codecs,
            Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            new RelogioFixo(DentroDoPeriodo),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioComCpf(cpf, sub: "sub-outro"),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
    }

    [Fact(DisplayName = "Processo sem divulgação: não encontrado, sem tentar gravar")]
    public async Task Handle_ProcessoSemDivulgacao_RetornaNaoEncontrado()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        ICertameDivulgadoRepository divulgadoRepository = Substitute.For<ICertameDivulgadoRepository>();
        divulgadoRepository.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns((CertameDivulgado?)null);

        Result<InscricaoDto> resultado = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            inscricaoRepository,
            divulgadoRepository,
            Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IRegistroCodecsEnvelope>(),
            Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            TimeProvider.System,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioComCpf(cpf),
            CancellationToken.None);

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
        resultado.Value!.VersaoFormularioId.Should().Be(cenario.Versao.Id);
        gravada!.VersaoFormularioId.Should().Be(cenario.Versao.Id);
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

    private static readonly DateTimeOffset DentroDoPeriodo = DivulgacaoDeTeste.DentroDoPeriodo;

    private sealed record Cenario(
        IInscricaoRepository Inscricoes,
        ICertameDivulgadoRepository Divulgados,
        IProcessoSeletivoRepository Processos,
        IRegistroCodecsEnvelope Codecs,
        VersaoConfiguracao Versao);

    private static Cenario MontarCriacao(Cpf cpf)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Inscrição");
        GrupoColetado grupoComDocumento = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 0, etapaCodigo: null, "Composição familiar",
            minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar(
                "RG_NUMERO_MEMBRO", 0, "RG do membro", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE").Value!],
            FinalidadeFormulario.Inscricao).Value!;

        GrafoConfiguracao grafo = new(
            [.. processoBase.Etapas], processoBase.OfertaAtendimento!, [.. processoBase.DistribuicaoVagas],
            processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
            [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null,
            fatosColetados:
            [
                FatoColetado.Criar("RG_NUMERO", 0, "RG", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null, classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE").Value!,
                FatoColetado.Criar("NOME_SOCIAL", 1, "Nome social", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!,
            ],
            formularios: [.. processoBase.Formularios],
            gruposColetados: [grupoComDocumento],
            numeroDeOpcoesDeCurso: 1);

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":null}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", DataHoraFixa);

        CertameDivulgado divulgado = CertameDivulgado.Criar(
            ProcessoId, numeroVersao: 1, AtoDaDivulgacao, new string('a', 64), versaoProjecao: "1",
            new FacetasDoCertameDivulgado("certame-inscricao", "Certame", "001/2026", ["AC"], DataHoraFixa, DataHoraFixa.AddDays(30)),
            """{"nome":"documento"}""", DataHoraFixa);

        EnvelopeReidratado envelopeReidratado = new(
            grafo,
            DadosEdital.Criar("001/2026", DivulgacaoDeTeste.InicioDoPeriodo, DivulgacaoDeTeste.FimDoPeriodo, Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        ICertameDivulgadoRepository divulgadoRepository = Substitute.For<ICertameDivulgadoRepository>();
        divulgadoRepository.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([versao]);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        registroCodecs.Capacidades.Returns([new CapacidadeCodec("0.0.22", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registroCodecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelopeReidratado));

        return new Cenario(inscricaoRepository, divulgadoRepository, processoRepository, registroCodecs, versao);
    }

    private static Task<Result<InscricaoDto>> Iniciar(Cenario cenario, Cpf cpf, TimeProvider relogio) =>
        IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            cenario.Inscricoes,
            cenario.Divulgados,
            cenario.Processos,
            cenario.Codecs,
            Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), Substitute.For<IEnderecoNoAcervoPublico>(),
            relogio,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioComCpf(cpf),
            CancellationToken.None);

    private static IResolvedorFusoInstitucional Fuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(sub);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }
}
