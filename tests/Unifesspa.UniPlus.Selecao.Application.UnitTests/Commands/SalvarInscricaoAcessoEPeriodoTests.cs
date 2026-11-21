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
using Unifesspa.UniPlus.Selecao.Application.Services;
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

        Result<InscricaoDto> resultado = await SalvarSemDivulgacaoAsync(inscricaoRepository, Substitute.For<IUnitOfWork>(), userContext, "\"x\"");

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

        Result<InscricaoDto> resultado = await SalvarSemDivulgacaoAsync(inscricaoRepository, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf), "\"x\"");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "ContaId diferente do gravado para o mesmo CPF é recusado como dono divergente, sem tentar gravar")]
    public async Task Handle_ContaIdDivergente_RecusaComoDonoDivergente()
    {
        Inscricao existente = NovaInscricao(PersonasDeTeste.KevinCarvalho, "conta-original", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, PersonasDeTeste.KevinCarvalho, Arg.Any<CancellationToken>()).Returns(existente);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarSemDivulgacaoAsync(
            inscricaoRepository, unitOfWork, UsuarioComCpf(PersonasDeTeste.KevinCarvalho, contaId: "conta-outro"), existente.ETag);

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

    private static Task<Result<InscricaoDto>> SalvarSemDivulgacaoAsync(
        IInscricaoRepository inscricoes, IUnitOfWork unitOfWork, IUserContext usuario, string etag)
    {
        RascunhoDoCandidato rascunho = new(new InscricaoAtivaDoCandidato(inscricoes), DivulgacaoDeTeste.Certame(ProcessoId),
            Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            DivulgacaoDeTeste.Fuso(), DivulgacaoDeTeste.Relogio);
        SalvarInscricaoCommand command = new(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([etag]));
        return SalvarDeTeste.Handle(command, rascunho, inscricoes, unitOfWork, usuario);
    }

    // A versão divulgada existe e o período está aberto; só a versão fixada pela inscrição sumiu.
    private static Task<Result<InscricaoDto>> SalvarSemAVersaoFixadaAsync(Inscricao existente, IInscricaoRepository inscricaoRepository, IUnitOfWork unitOfWork)
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        (VersaoConfiguracao divulgada, ConfiguracaoPublicada configuracao) = MontarVersaoComItem(ItemApelido());
        DivulgacaoDeTeste.ConfigurarDivulgada(processoRepository, registroCodecs, divulgada, configuracao);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        return SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            new RascunhoDoCandidato(new InscricaoAtivaDoCandidato(inscricaoRepository), DivulgacaoDeTeste.Certame(ProcessoId), processoRepository, registroCodecs, DivulgacaoDeTeste.Fuso(), DivulgacaoDeTeste.Relogio),
            inscricaoRepository, unitOfWork, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));
    }

    private static FatoColetado ItemApelido() => FatoColetadoDeTeste.Coletando("APELIDO", 0).ComRotulo("Apelido").DeTexto("LIVRE").Construir();

    [Fact(DisplayName = "Fora do período da inscrição, salvar é recusado sem gravar nada")]
    public async Task Handle_ForaDoPeriodo_RecusaSemGravar()
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao) = MontarVersaoComItem(ItemApelido());
        Inscricao existente = NovaInscricao(PersonasDeTeste.FernandaDrumond, "conta-1", versao.Id);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        Result<InscricaoDto> resultado = await SalvarNaVersaoAsync(existente, versao, configuracao, unitOfWork,
            UsuarioComCpf(PersonasDeTeste.FernandaDrumond), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O rascunho adota a versão divulgada que prorroga o período: salva depois do fim da versão em que ele nasceu")]
    public async Task Handle_PeriodoProrrogadoPelaRetificacao_Salva()
    {
        FatoColetado item = ItemApelido();
        (VersaoConfiguracao fixada, ConfiguracaoPublicada configuracao) = MontarVersaoComItem(item);
        (VersaoConfiguracao divulgada, _) = MontarVersaoComItem(item);
        Inscricao existente = NovaInscricao(PersonasDeTeste.FernandaDrumond, "conta-1", fixada.Id);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<InscricaoDto> resultado = await SalvarNoPeriodoProrrogadoAsync(existente, (fixada, divulgada), configuracao, unitOfWork);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static Task<Result<InscricaoDto>> SalvarNoPeriodoProrrogadoAsync(
        Inscricao existente, (VersaoConfiguracao Fixada, VersaoConfiguracao Divulgada) versoes, ConfiguracaoPublicada configuracao, IUnitOfWork unitOfWork)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, configuracao, fixada: (versoes.Fixada, DivulgacaoDeTeste.FimDoPeriodo), divulgada: (versoes.Divulgada, DivulgacaoDeTeste.FimDoPeriodo.AddDays(30)));

        return SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([DivulgacaoDeTeste.ETagDepoisDeAdotar(existente, versoes.Divulgada)])),
            new RascunhoDoCandidato(new InscricaoAtivaDoCandidato(inscricaoRepository), DivulgacaoDeTeste.Certame(ProcessoId), processoRepository, registroCodecs, DivulgacaoDeTeste.Fuso(), new RelogioFixo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(10))),
            inscricaoRepository, unitOfWork, UsuarioComCpf(cpf));
    }

    [Fact(DisplayName = "Inscrição já enviada recusa o rascunho antes de conferir as respostas, mesmo com resposta inválida")]
    public async Task Handle_InscricaoEnviadaComRespostaInvalida_RecusaPeloEnvio()
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao) = MontarVersaoComItem(ApelidoComLimite());
        Inscricao existente = InscricaoEnviadaDeTeste.Enviar(NovaInscricao(PersonasDeTeste.KevinCarvalho, "conta-1", versao.Id));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        InscricaoRequest request = new(null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);
        Result<InscricaoDto> resultado = await SalvarNaVersaoAsync(existente, versao, configuracao, unitOfWork,
            UsuarioComCpf(PersonasDeTeste.KevinCarvalho), DivulgacaoDeTeste.Relogio, request);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição já enviada, fora do período, recusa pelo período: o novo envio que a outra recusa orienta também seria recusado")]
    public async Task Handle_InscricaoEnviadaForaDoPeriodo_RecusaPeloPeriodo()
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao) = MontarVersaoComItem(ApelidoComLimite());
        Inscricao existente = InscricaoEnviadaDeTeste.Enviar(NovaInscricao(PersonasDeTeste.KevinCarvalho, "conta-1", versao.Id));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        InscricaoRequest request = new(null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement("Apelido Enorme") }, null, null);
        Result<InscricaoDto> resultado = await SalvarNaVersaoAsync(existente, versao, configuracao, unitOfWork,
            UsuarioComCpf(PersonasDeTeste.KevinCarvalho), new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)), request);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static FatoColetado ApelidoComLimite() => FatoColetadoDeTeste.Coletando("APELIDO", 0)
        .ComRotulo("Apelido")
        .DeTexto("LIVRE")
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComRestricoes(new TamanhoTexto(1, 5))
        .Construir();

    private static Task<Result<InscricaoDto>> SalvarNaVersaoAsync(Inscricao existente, VersaoConfiguracao versao,
        ConfiguracaoPublicada configuracao, IUnitOfWork unitOfWork, IUserContext usuario, TimeProvider relogio, InscricaoRequest? request = null)
    {
        IInscricaoRepository inscricoes = Substitute.For<IInscricaoRepository>();
        inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, existente.Titular.Cpf, Arg.Any<CancellationToken>()).Returns(existente);
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(processos, codecs, versao, configuracao);
        return SalvarDeTeste.Handle(
            new SalvarInscricaoCommand(ProcessoId, request ?? new(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            new RascunhoDoCandidato(new InscricaoAtivaDoCandidato(inscricoes), DivulgacaoDeTeste.Certame(ProcessoId), processos, codecs, DivulgacaoDeTeste.Fuso(), relogio),
            inscricoes, unitOfWork, usuario);
    }
}
