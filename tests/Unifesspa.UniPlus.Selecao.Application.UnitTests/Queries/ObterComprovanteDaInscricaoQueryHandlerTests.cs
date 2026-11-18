namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Ler um comprovante da inscrição pelo id e pelo número do envio: só o dono lê, a cancelada não tem comprovante e o
/// comprovante pedido é o único que se carrega.
/// </summary>
public sealed class ObterComprovanteDaInscricaoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed record Cenario(
        IInscricaoRepository Inscricoes, IComprovanteDaInscricaoRepository Comprovantes, IProcessoSeletivoRepository Processos, IRegistroCodecsEnvelope Registro, MontadorDoComprovante Montador);

    private static Cenario Novo(Inscricao inscricao, VersaoConfiguracao? versao = null, ConfiguracaoPublicada? grafo = null)
    {
        IInscricaoRepository inscricoes = Substitute.For<IInscricaoRepository>();
        inscricoes.ObterPorIdAsync(inscricao.Id, Arg.Any<CancellationToken>()).Returns(inscricao);
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        if (versao is not null && grafo is not null)
        {
            DivulgacaoDeTeste.ConfigurarVersao(processos, registro, versao, grafo);
        }

        PrazoDaCorrecaoDoComprovante prazo = new(
            Substitute.For<ICertameDivulgadoRepository>(), processos, registro, Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System);

        return new Cenario(inscricoes, Substitute.For<IComprovanteDaInscricaoRepository>(), processos, registro, new MontadorDoComprovante(processos, registro, prazo, new CancelamentoDoComprovante(DivulgacaoDeTeste.Fuso())));
    }

    private static IUserContext Usuario(Cpf cpf, string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    private static Inscricao Iniciar(Cpf cpf, string contaId, Guid versaoId) =>
        Inscricao.Iniciar(ProcessoId, cpf, contaId, "https://issuer", versaoId).Value!;

    private static Inscricao Cancelada(Cpf cpf) => Inscricao.Reidratar(Guid.CreateVersion7(), ProcessoId,
            TitularDaInscricao.Reidratar(cpf, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Cancelada, "000123", CancelamentoDaInscricao.Reidratar(Instante, "conta-1", "a pedido do candidato")),
            FormularioDaInscricao.Reidratar(Guid.CreateVersion7(), PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 2, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

    private static ComprovanteNaPosicao Posicao(Inscricao inscricao, int numero, int total) => new(
        ComprovanteInscricao.Emitir(
            inscricao.Id, "00000001", Instante, null, new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [], []),
        numero, total);

    private static Task<Result<ComprovanteDaInscricaoDto>> Ler(Cenario cenario, Guid inscricaoId, int numero, IUserContext usuario) =>
        ObterComprovanteDaInscricaoQueryHandler.Handle(
            new ObterComprovanteDaInscricaoQuery(inscricaoId, numero), cenario.Comprovantes, cenario.Montador, new InscricaoDoDono(cenario.Inscricoes, usuario), CancellationToken.None);

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        Cenario cenario = Novo(Iniciar(PersonasDeTeste.KevinCarvalho, "conta-1", Guid.CreateVersion7()));
        IUserContext semCpf = Substitute.For<IUserContext>();
        semCpf.Cpf.Returns((string?)null);

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(cenario, Guid.CreateVersion7(), 1, semCpf);

        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await cenario.Inscricoes.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A inscrição de outro CPF responde como inexistente, sem confirmar que o id existe")]
    public async Task Handle_InscricaoDeOutroCpf_NaoEncontrada()
    {
        Inscricao deOutro = Iniciar(PersonasDeTeste.KevinCarvalho, "conta-1", Guid.CreateVersion7());
        Cenario cenario = Novo(deOutro);

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(cenario, deOutro.Id, 1, Usuario(PersonasDeTeste.FernandaDrumond));

        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
        await cenario.Comprovantes.DidNotReceiveWithAnyArgs().ObterComprovanteAsync(default, default, default);
    }

    [Fact(DisplayName = "O mesmo CPF com outra conta é recusado como dono divergente")]
    public async Task Handle_ContaIdDivergente_DonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao inscricao = Iniciar(cpf, "conta-original", Guid.CreateVersion7());

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(Novo(inscricao), inscricao.Id, 1, Usuario(cpf, "conta-outro"));

        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
    }

    [Fact(DisplayName = "A inscrição cancelada recusa com 422 sem ler o comprovante nem carregar a versão")]
    public async Task Handle_Cancelada_Recusa422SemCarregarNada()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao cancelada = Cancelada(cpf);
        Cenario cenario = Novo(cancelada);

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(cenario, cancelada.Id, 1, Usuario(cpf));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ComprovanteDeInscricaoCancelada);
        await cenario.Comprovantes.DidNotReceiveWithAnyArgs().ObterComprovanteAsync(default, default, default);
        await cenario.Processos.DidNotReceiveWithAnyArgs().ObterVersoesAsync(default, default);
    }

    [Fact(DisplayName = "A cancelada de outro dono responde como inexistente, não como cancelada")]
    public async Task Handle_CanceladaDeOutroDono_NaoEncontrada()
    {
        Inscricao cancelada = Cancelada(PersonasDeTeste.KevinCarvalho);

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(Novo(cancelada), cancelada.Id, 1, Usuario(PersonasDeTeste.FernandaDrumond));

        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "O número que a inscrição não tem responde como comprovante não encontrado")]
    public async Task Handle_NumeroInexistente_ComprovanteNaoEncontrado()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao inscricao = Iniciar(cpf, "conta-1", Guid.CreateVersion7());
        Cenario cenario = Novo(inscricao);
        cenario.Comprovantes.ObterComprovanteAsync(inscricao.Id, 9, Arg.Any<CancellationToken>()).Returns((ComprovanteNaPosicao?)null);

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(cenario, inscricao.Id, 9, Usuario(cpf));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ComprovanteNaoEncontrado);
        resultado.Error.Message.Should().Be("O comprovante solicitado não foi encontrado.");
    }

    [Fact(DisplayName = "O comprovante pedido sai montado com a posição dele, e o prazo ausente não derruba o documento")]
    public async Task Handle_ComprovantePedido_MontaSemOPrazo()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, ConfiguracaoPublicada grafo) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        Inscricao inscricao = Iniciar(cpf, "conta-1", versao.Id);
        Cenario cenario = Novo(inscricao, versao, grafo);
        cenario.Comprovantes.ObterComprovanteAsync(inscricao.Id, 2, Arg.Any<CancellationToken>()).Returns(Posicao(inscricao, 2, 2));

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(cenario, inscricao.Id, 2, Usuario(cpf));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Inscricao.Numero.Should().Be(2);
        resultado.Value.Inscricao.Vale.Should().BeTrue();
        resultado.Value.Inscricao.PrazoDaCorrecao.Should().BeNull("o prazo é informação acessória: sem o certame divulgado, o comprovante sai sem ele");
        await cenario.Comprovantes.Received(1).ObterComprovanteAsync(inscricao.Id, 2, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O comprovante que não vale não traz prazo da correção")]
    public async Task Handle_ComprovanteSubstituido_SemPrazo()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, ConfiguracaoPublicada grafo) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        Inscricao inscricao = Iniciar(cpf, "conta-1", versao.Id);
        Cenario cenario = Novo(inscricao, versao, grafo);
        cenario.Comprovantes.ObterComprovanteAsync(inscricao.Id, 1, Arg.Any<CancellationToken>()).Returns(Posicao(inscricao, 1, 2));

        Result<ComprovanteDaInscricaoDto> resultado = await Ler(cenario, inscricao.Id, 1, Usuario(cpf));

        resultado.Value!.Inscricao.Vale.Should().BeFalse();
        resultado.Value.Inscricao.NumeroSeguinte.Should().Be(2);
        resultado.Value.Inscricao.PrazoDaCorrecao.Should().BeNull();
    }
}
