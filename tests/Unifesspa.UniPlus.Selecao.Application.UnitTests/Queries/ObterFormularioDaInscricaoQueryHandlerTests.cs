namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O formulário renderizável da versão que a inscrição fixou ao iniciar (issue #1859): resolve
/// pelo <c>VersaoFormularioId</c> gravado, nunca pela divulgação atual — a mesma projeção de
/// <see cref="ProcessosSeletivos.ObterFormularioRenderizavelQueryHandlerTests"/>, só a resolução
/// da versão é diferente.
/// </summary>
public sealed class ObterFormularioDaInscricaoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private const string SchemaVersion = "0.0.22";

    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    private static IEnderecoNoAcervoPublico CriarAcervo()
    {
        IEnderecoNoAcervoPublico acervo = Substitute.For<IEnderecoNoAcervoPublico>();
        acervo.De(Arg.Any<string>()).Returns(static chamada => new Uri($"https://acervo.teste/{chamada.Arg<string>()}"));
        return acervo;
    }

    private static (VersaoConfiguracao Versao, EnvelopeReidratado Envelope) MontarVersaoComFormularioDeInscricao()
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Formulário da inscrição");
        FormularioProcesso formulario = FormularioProcesso.Criar(
            FinalidadeFormulario.Inscricao, null, null,
            [EtapaFormulario.Criar("REVISAO", 0, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!]).Value!;

        EnvelopeReidratado envelope = new(
            new GrafoConfiguracao(
                [.. processoBase.Etapas], processoBase.OfertaAtendimento!, [.. processoBase.DistribuicaoVagas],
                processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
                [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null, formularios: [formulario]),
            DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null);

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos": {"dataReferenciaFatos": null}}"""), SchemaVersion, "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        return (versao, envelope);
    }

    private static IRegistroCodecsEnvelope RegistroReidratando(EnvelopeReidratado envelope)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns([new CapacidadeCodec(SchemaVersion, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(envelope));
        return registro;
    }

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<FormularioRenderizavelDto> resultado = await ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(ProcessoId), inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IRegistroCodecsEnvelope>(), CriarAcervo(), userContext, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem inscrição para o CPF e o processo, recusa como não encontrada")]
    public async Task Handle_SemInscricao_RecusaComoNaoEncontrada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        Result<FormularioRenderizavelDto> resultado = await ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(ProcessoId), inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IRegistroCodecsEnvelope>(), CriarAcervo(), UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "Devolve o formulário da versão FIXADA pela inscrição, não a divulgada atual")]
    public async Task Handle_InscricaoComVersaoFixada_DevolveFormularioDaquelaVersao()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        (VersaoConfiguracao versao, EnvelopeReidratado envelope) = MontarVersaoComFormularioDeInscricao();
        Inscricao existente = Inscricao.Iniciar(
            ProcessoId, cpf, "sub-1", "https://issuer", versao.Id, new HashSet<string>(StringComparer.Ordinal)).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([versao]);

        Result<FormularioRenderizavelDto> resultado = await ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(ProcessoId), inscricaoRepository, processoRepository,
            RegistroReidratando(envelope), CriarAcervo(), UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Finalidade.Should().Be(EstruturaFormulario.ParaToken(FinalidadeFormulario.Inscricao));
    }
}
