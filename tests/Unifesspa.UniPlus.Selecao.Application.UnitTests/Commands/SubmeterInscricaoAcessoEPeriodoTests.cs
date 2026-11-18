namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// Submeter a inscrição recusa quem não é o dono, o que está fora do período e a versão fixada que sumiu,
/// e aceita o período que a versão divulgada depois prorrogou.
/// </summary>
public sealed class SubmeterInscricaoAcessoEPeriodoTests
{
    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<IUnitOfWork>(), userContext);

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

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags(["\"x\""])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "ContaId diferente do gravado para o mesmo CPF é recusado como dono divergente, sem tentar gravar")]
    public async Task Handle_ContaIdDivergente_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = NovaInscricao(cpf, "conta-original", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = InscricoesComAtiva(existente);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(),
            unitOfWork, UsuarioComCpf(cpf, contaId: "conta-outro"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Versão fixada que não existe mais entre as versões do processo aflora como corrupção, sem gravar nada")]
    public async Task Handle_VersaoFixadaInexistente_AfloraComoCorrupcao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao existente = NovaInscricao(cpf, "conta-1", Guid.CreateVersion7());

        IInscricaoRepository inscricaoRepository = InscricoesComAtiva(existente);

        (IProcessoSeletivoRepository processoRepository, IRegistroCodecsEnvelope registroCodecs) = DivulgadaSemVersoes();

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result<ReciboDaInscricaoDto> resultado = await SubmeterAsync(
            new SubmeterInscricaoCommand(ProcessoId, new InscricaoRequest(null, null, null, null), PrecondicaoIfMatch.DeTags([existente.ETag])),
            inscricaoRepository, processoRepository, registroCodecs, unitOfWork, UsuarioComCpf(cpf));

        await ConferirVersaoAusenteSemGravacaoAsync(resultado, inscricaoRepository, unitOfWork);
    }

    [Fact(DisplayName = "Fora do período da inscrição, enviar é recusado sem gerar número nem comprovante")]
    public async Task Handle_ForaDoPeriodo_RecusaSemComprovante()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo());
        IGeradorDeNumeroInscricao gerador = GeradorQueDevolve("00000001");

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(new InscricaoRequest(null, null, null, null),
            numeros: gerador, relogio: new RelogioFixo(DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1)));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        await gerador.DidNotReceive().ProximoAsync(Arg.Any<CancellationToken>());
        await cenario.Comprovantes.DidNotReceive().AdicionarComprovanteAsync(Arg.Any<ComprovanteInscricao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O rascunho adota a versão divulgada que prorroga o período: envia depois do fim da versão em que ele nasceu")]
    public async Task Handle_PeriodoProrrogadoPelaRetificacao_Submete()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao fixada, ConfiguracaoPublicada configuracao, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo();
        VersaoConfiguracao divulgada = VersaoDivulgadaQueProrroga();

        Result<ReciboDaInscricaoDto> resultado = await SubmeterNoPeriodoProrrogadoAsync(cpf, NovaInscricao(cpf, "conta-1", fixada.Id), (fixada, divulgada, configuracao, termo));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static (IProcessoSeletivoRepository, IRegistroCodecsEnvelope) DivulgadaSemVersoes()
    {
        // A versão divulgada existe e o período está aberto; só a versão fixada pela inscrição sumiu.
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        (VersaoConfiguracao divulgada, ConfiguracaoPublicada configuracao, _) = MontarVersaoComItemObrigatorioETermo();
        DivulgacaoDeTeste.ConfigurarDivulgada(processoRepository, registroCodecs, divulgada, configuracao);
        processoRepository.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);

        return (processoRepository, registroCodecs);
    }

    private static VersaoConfiguracao VersaoDivulgadaQueProrroga() => VersaoConfiguracao.Abrir(
        ProcessoId,
        new DocumentoCongelado(Encoding.UTF8.GetBytes("""{"documentosExigidos":{"dataReferenciaFatos":"2026-12-10"}}"""), "0.0.22", "canonical-json/sha256@v1", new string('c', 64)),
        Guid.CreateVersion7(), "user-conta-1", new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));

    private static Task<Result<ReciboDaInscricaoDto>> SubmeterNoPeriodoProrrogadoAsync(
        Cpf cpf, Inscricao existente, (VersaoConfiguracao Fixada, VersaoConfiguracao Divulgada, ConfiguracaoPublicada Configuracao, VersaoTermoEscolhida Termo) versoes)
    {
        IInscricaoRepository inscricaoRepository = InscricoesComAtiva(existente);
        (IProcessoSeletivoRepository processoRepository, IRegistroCodecsEnvelope registroCodecs) = VersoesDoPeriodoProrrogado(versoes.Fixada, versoes.Divulgada, versoes.Configuracao);
        SubmeterInscricaoCommand command = new(
            ProcessoId,
            RequestComApelidoETermoAceito(cpf, versoes.Termo, versoes.Configuracao),
            PrecondicaoIfMatch.DeTags([DivulgacaoDeTeste.ETagDepoisDeAdotar(existente, versoes.Divulgada)]));

        return new SubmissaoDeTeste(inscricaoRepository, processoRepository, registroCodecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(cpf))
        {
            Numeros = GeradorQueDevolve("00000007"),
            Relogio = new RelogioFixo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(10)),
        }.SubmeterAsync(command);
    }

    private static (IProcessoSeletivoRepository Processos, IRegistroCodecsEnvelope Codecs) VersoesDoPeriodoProrrogado(
        VersaoConfiguracao fixada, VersaoConfiguracao divulgada, ConfiguracaoPublicada configuracao)
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registroCodecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            processoRepository, registroCodecs, configuracao,
            fixada: (fixada, DivulgacaoDeTeste.FimDoPeriodo),
            divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo.AddDays(30)));

        return (processoRepository, registroCodecs);
    }
    private static async Task ConferirVersaoAusenteSemGravacaoAsync(
        Result<ReciboDaInscricaoDto> resultado, IInscricaoRepository inscricaoRepository, IUnitOfWork unitOfWork)
    {
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        await inscricaoRepository.DidNotReceive().AtualizarAsync(Arg.Any<Inscricao>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
