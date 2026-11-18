namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
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

using static SalvarDeTeste;

/// <summary>
/// Salvar o rascunho recusa quem não é o dono da inscrição, a versão fixada que não existe mais e o período encerrado,
/// e aceita o período que uma retificação prorrogou; em toda recusa nada é gravado.
/// </summary>
public sealed class SalvarInscricaoAcessoEPeriodoTests
{
    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(),
            Substitute.For<IUnitOfWork>(), userContext, DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterAtivaPorProcessoECpfAsync(Arg.Any<Guid>(), Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem inscrição para o CPF e o processo, recusa como não encontrada")]
    public async Task Handle_SemInscricao_RecusaComoNaoEncontrada()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(),
            Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "ContaId diferente do gravado para o mesmo CPF é recusado como dono divergente, sem tentar gravar")]
    public async Task Handle_ContaIdDivergente_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = NovaInscricao(cpf, "conta-original", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), Substitute.For<IRegistroCodecsEnvelope>(), DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(),
            unitOfWork, UsuarioComCpf(cpf, contaId: "conta-outro"), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Versão fixada que não existe mais entre as versões do processo aflora como corrupção, sem gravar nada")]
    public async Task Handle_VersaoFixadaInexistente_AfloraComoCorrupcao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao existente = NovaInscricao(cpf, "conta-1", Guid.CreateVersion7());
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarSemAVersaoFixadaAsync(existente, inscricaoRepository, unitOfWork);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    // A versão divulgada existe e o período está aberto; só a versão fixada pela inscrição sumiu.
    private static Task<Result<InscricaoDto>> SalvarSemAVersaoFixadaAsync(Inscricao existente, IInscricaoRepository inscricaoRepository, IUnitOfWork unitOfWork)
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        (VersaoConfiguracao divulgada, GrafoConfiguracao grafo) = MontarVersaoComItem(ItemApelido());
        DivulgacaoDeTeste.ConfigurarDivulgada(processoRepository, registroCodecs, divulgada, grafo);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        return SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), DivulgacaoDeTeste.Relogio, CancellationToken.None);
    }

    private static FatoColetado ItemApelido() => FatoColetado.Criar(
        "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
        classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    [Fact(DisplayName = "Fora do período da inscrição, salvar é recusado sem gravar nada")]
    public async Task Handle_ForaDoPeriodo_RecusaSemGravar()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Nunca, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(),
            unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)), CancellationToken.None);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O rascunho adota a versão divulgada que prorroga o período: salva depois do fim da versão em que ele nasceu")]
    public async Task Handle_PeriodoProrrogadoPelaRetificacao_Salva()
    {
        FatoColetado item = ItemApelido();
        (VersaoConfiguracao fixada, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        (VersaoConfiguracao divulgada, _) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(PersonasDeTeste.FernandaDrumond, "conta-1", fixada.Id);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarNoPeriodoProrrogadoAsync(existente, (fixada, divulgada), grafo, unitOfWork);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static Task<Result<InscricaoDto>> SalvarNoPeriodoProrrogadoAsync(
        Inscricao existente, (VersaoConfiguracao Fixada, VersaoConfiguracao Divulgada) versoes, GrafoConfiguracao grafo, IUnitOfWork unitOfWork)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, grafo, fixada: (versoes.Fixada, DivulgacaoDeTeste.FimDoPeriodo), divulgada: (versoes.Divulgada, DivulgacaoDeTeste.FimDoPeriodo.AddDays(30)));

        return SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([DivulgacaoDeTeste.ETagDepoisDeAdotar(existente, versoes.Divulgada)])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(),
            unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(10)), CancellationToken.None);
    }

    [Fact(DisplayName = "Inscrição já enviada recusa o rascunho antes de conferir as respostas, mesmo com resposta inválida")]
    public async Task Handle_InscricaoEnviadaComRespostaInvalida_RecusaPeloEnvio()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;

        FatoColetado item = FatoColetado.Criar(
            "APELIDO", 0, "Apelido", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        existente.Submeter(
            "000001", null, null, semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), new HashSet<Guid>(), new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)), FormularioDeTeste.AvaliacaoSemOcultos,
            semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([existente.ETag]), DateTimeOffset.UnixEpoch, []).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(cpf), DivulgacaoDeTeste.Relogio, CancellationToken.None);

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
            classificacaoProtecao: "PESSOAL", formato: "LIVRE", restricoes: [new TamanhoTexto(1, 5)], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        (VersaoConfiguracao versao, GrafoConfiguracao grafo) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(cpf, "conta-1", versao.Id);
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        existente.Submeter(
            "000001", null, null, semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), new HashSet<Guid>(), new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)), FormularioDeTeste.AvaliacaoSemOcultos,
            semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([existente.ETag]), DateTimeOffset.UnixEpoch, []).IsSuccess.Should().BeTrue();

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();

        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processoRepository, registroCodecs, versao, grafo);

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        InscricaoRequest request = new(
            null, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);

        Result<InscricaoDto> resultado = await SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request, PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, Substitute.For<IDocumentoDaInscricaoRepository>(), DivulgacaoDeTeste.Certame(ProcessoId), registroCodecs, DivulgacaoDeTeste.Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), DivulgacaoDeTeste.Enderecos(), unitOfWork, UsuarioComCpf(cpf), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
