namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;
using System.Text.Json;

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
/// Salvar o rascunho (issue #1859): a validação é estrutural — só recusa por campo a resposta que
/// viola uma restrição do formulário, nunca por obrigatoriedade. As regras vêm da versão FIXADA
/// pela inscrição ao iniciar, resolvida por <c>IProcessoSeletivoRepository.ObterVersoesAsync</c>,
/// nunca pela divulgação atual — uma retificação publicada depois não pode mudar a validação de
/// uma inscrição já em curso (UNI-REQ-0084).
/// </summary>
public sealed class SalvarInscricaoCommandHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private static Inscricao NovaInscricao(Cpf cpf, string sub, Guid versaoFormularioId) =>
        Inscricao.Iniciar(ProcessoId, cpf, sub, "https://issuer", versaoFormularioId).Value!;

    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(sub);
        contexto.Issuer.Returns("https://issuer");
        return contexto;
    }

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(),
            Substitute.For<ISelecaoUnitOfWork>(), userContext, DivulgacaoDeTeste.Relogio, CancellationToken.None);

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

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(),
            Substitute.For<ISelecaoUnitOfWork>(), UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "Sub diferente do gravado para o mesmo CPF é recusado como dono divergente, sem tentar gravar")]
    public async Task Handle_SubDivergente_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = NovaInscricao(cpf, "sub-original", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(),
            unitOfWork, UsuarioComCpf(cpf, sub: "sub-outro"), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Versão fixada que não existe mais entre as versões do processo aflora como corrupção, sem gravar nada")]
    public async Task Handle_VersaoFixadaInexistente_AfloraComoCorrupcao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao existente = NovaInscricao(cpf, "sub-1", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        // A versão divulgada existe e o período está aberto; só a versão fixada pela inscrição sumiu.
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        (VersaoConfiguracao divulgada, GrafoConfiguracao grafo) = MontarVersaoComItem(FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!);
        DivulgacaoDeTeste.ConfigurarDivulgada(processoRepository, registroCodecs, divulgada, grafo);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static (VersaoConfiguracao Versao, GrafoConfiguracao Grafo) MontarVersaoComItem(
        FatoColetado item, IReadOnlyList<GrupoColetado>? grupos = null)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Salvar rascunho");
        processoBase.DefinirItens([item], PrecondicaoIfMatch.Ausente, grupos: grupos).IsSuccess.Should().BeTrue();

        // A definição avaliável só reconhece um item que esteja numa etapa de um FORMULÁRIO
        // (RecorteDaFinalidade filtra por etapa de FormularioProcesso, não por EtapaProcesso do
        // certame) — por isso o grafo leva processoBase.Formularios, que FormularioDeTeste.DefinirItens
        // já colocou o item dentro da seção "DADOS" da finalidade Inscrição.
        GrafoConfiguracao grafo = new(
            [.. processoBase.Etapas], processoBase.OfertaAtendimento!, [.. processoBase.DistribuicaoVagas],
            processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
            [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null,
            fatosColetados: [.. processoBase.FatosColetados],
            formularios: [.. processoBase.Formularios],
            gruposColetados: [.. processoBase.GruposColetados]);

        // O bloco documentosExigidos.dataReferenciaFatos é lido do JSON cru por
        // ContextoDaVersaoFixadaResolver (issue #1859) — presente em todo envelope real; aqui
        // nulo, porque este processo de teste não declara referência temporal.
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":null}}"""), "0.0.22", "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        return (versao, grafo);
    }

    [Fact(DisplayName = "Fora do período da inscrição, salvar é recusado sem gravar nada")]
    public async Task Handle_ForaDoPeriodo_RecusaSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(),
            unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)), CancellationToken.None);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A retificação que prorroga o período vale para quem já iniciou: salva depois do fim da versão fixada")]
    public async Task Handle_PeriodoProrrogadoPelaRetificacao_Salva()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE").Value!;
        (VersaoConfiguracao fixada, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        (VersaoConfiguracao divulgada, _) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", fixada.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, grafo,
            fixada: (fixada, DivulgacaoDeTeste.FimDoPeriodo),
            divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo.AddDays(30)));
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(),
            unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(10)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta dentro do tamanho permitido é salva, sobre a versão fixada ao iniciar")]
    public async Task Handle_RespostaValida_Salva()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 10)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Nanda") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("Nanda");
        await inscricaoRepository.Received(1).AtualizarAsync(existente, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta que viola uma restrição do formulário é recusada por campo, sem gravar nada — obrigatoriedade sem resposta não é recusada")]
    public async Task Handle_RespostaViolaRestricao_RecusaPorCampoSemGravar()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // "Apelido Enorme" tem mais de 5 caracteres — viola o TamanhoTexto(1, 5) da versão fixada.
        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "APELIDO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já enviada recusa o rascunho antes de conferir as respostas, mesmo com resposta inválida")]
    public async Task Handle_InscricaoEnviadaComRespostaInvalida_RecusaPeloEnvio()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        existente.Submeter(
            "000001", null, semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<Guid>()),
            semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([existente.ETag]), DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já enviada, fora do período, recusa pelo período: o novo envio que a outra recusa orienta também seria recusado")]
    public async Task Handle_InscricaoEnviadaForaDoPeriodo_RecusaPeloPeriodo()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        existente.Submeter(
            "000001", null, semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<Guid>()),
            semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([existente.ETag]), DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Resposta de um membro de grupo repetível que viola uma restrição do formulário também é recusada por campo, sem gravar nada")]
    public async Task Handle_RespostaDeMembroDeGrupoViolaRestricao_RecusaPorCampoSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;

        FatoColetado itemDoTopo = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 10)]).Value!;
        GrupoColetado composicaoFamiliar = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 1, etapaCodigo: FormularioDeTeste.Secao, "Composição familiar",
            minimo: 0, maximo: null, exibicao: null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar(
                "RG_NUMERO_MEMBRO", 0, "RG do membro", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)]).Value!],
            FinalidadeFormulario.Inscricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(itemDoTopo, [composicaoFamiliar]);
        Inscricao existente = NovaInscricao(cpf, "sub-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        // O RG do membro excede o TamanhoTexto(1, 5) do subitem do grupo — a mesma violação do
        // teste anterior, agora dentro de uma ocorrência de COMPOSICAO_FAMILIAR.
        InscricaoRequest request = new(
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Nanda") },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricaoInput("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("12.345.678-9"),
                    }),
                ],
            },
            null);

        Result<InscricaoDto> resultado = await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "COMPOSICAO_FAMILIAR.membro-1.RG_NUMERO_MEMBRO" && e.Error.Code == "Inscricao.RespostaInvalida");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
