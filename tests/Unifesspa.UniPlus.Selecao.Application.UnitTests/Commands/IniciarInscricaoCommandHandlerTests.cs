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
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Início da inscrição (issue #1858): a resolução da versão divulgada (UNI-REQ-0084, mesmo
/// caminho de <c>ObterFormularioRenderizavelQueryHandler</c>) e o congelamento de
/// <c>FatosIdentificadorDeclarados</c> a partir da classificação de proteção do grafo reidratado.
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
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já existente para o CPF e o processo: devolve a existente, sem gravar nada")]
    public async Task Handle_InscricaoJaExiste_DevolveAExistenteSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao existente = Inscricao.Iniciar(
            ProcessoId, cpf, "sub-1", "https://issuer", Guid.CreateVersion7(),
            new HashSet<string>(StringComparer.Ordinal)).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            inscricaoRepository,
            Substitute.For<ICertameDivulgadoRepository>(),
            Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IRegistroCodecsEnvelope>(),
            unitOfWork,
            UsuarioComCpf(cpf),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Id.Should().Be(existente.Id);
        await inscricaoRepository.DidNotReceive().AdicionarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo sem divulgação: não encontrado, sem tentar gravar")]
    public async Task Handle_ProcessoSemDivulgacao_RetornaNaoEncontrado()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        ICertameDivulgadoRepository divulgadoRepository = Substitute.For<ICertameDivulgadoRepository>();
        divulgadoRepository.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns((CertameDivulgado?)null);

        Result<InscricaoDto> resultado = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            inscricaoRepository,
            divulgadoRepository,
            Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioComCpf(cpf),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Cria a inscrição fixando a versão divulgada e os fatos Identificador do grafo (de nível superior e de grupo)")]
    public async Task Handle_CriaNova_FixaVersaoEFatosIdentificadorDeclarados()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;

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
            gruposColetados: [grupoComDocumento]);

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("{}"), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", DataHoraFixa);

        CertameDivulgado divulgado = CertameDivulgado.Criar(
            ProcessoId, numeroVersao: 1, AtoDaDivulgacao, new string('a', 64), versaoProjecao: "1",
            new FacetasDoCertameDivulgado("certame-inscricao", "Certame", "001/2026", ["AC"], DataHoraFixa, DataHoraFixa.AddDays(30)),
            """{"nome":"documento"}""", DataHoraFixa);

        EnvelopeReidratado envelopeReidratado = new(
            grafo,
            DadosEdital.Criar("001/2026", DataHoraFixa, DataHoraFixa.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        ICertameDivulgadoRepository divulgadoRepository = Substitute.For<ICertameDivulgadoRepository>();
        divulgadoRepository.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([versao]);

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        registroCodecs.Capacidades.Returns([new CapacidadeCodec("0.0.22", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registroCodecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelopeReidratado));

        Inscricao? gravada = null;
        inscricaoRepository.WhenForAnyArgs(r => r.AdicionarAsync(default!, default))
            .Do(call => gravada = call.Arg<Inscricao>());

        Result<InscricaoDto> resultado = await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(ProcessoId),
            inscricaoRepository,
            divulgadoRepository,
            processoRepository,
            registroCodecs,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioComCpf(cpf),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.VersaoFormularioId.Should().Be(versao.Id);
        gravada.Should().NotBeNull();
        gravada!.FatosIdentificadorDeclarados.Should().BeEquivalentTo(["RG_NUMERO", "RG_NUMERO_MEMBRO"],
            "o conjunto junta os fatos Identificador de nível superior e os de campo de grupo");
    }

    private static IUserContext UsuarioComCpf(Cpf cpf)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns("sub-1");
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }
}
