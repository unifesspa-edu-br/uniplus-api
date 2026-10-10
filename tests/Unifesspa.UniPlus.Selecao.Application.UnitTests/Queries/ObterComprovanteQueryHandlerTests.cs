namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Ler um comprovante por número (issue #1860, <c>GET .../inscricao/comprovantes/{n}</c>): o
/// comprovante 1 é a primeira submissão, cada correção emite o próximo — append-only, o anterior
/// nunca muda (CA-01).
/// </summary>
public sealed class ObterComprovanteQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    private static Inscricao InscricaoComComprovantes(Cpf cpf, string sub, int quantidade)
    {
        Inscricao inscricao = Inscricao.Iniciar(
            ProcessoId, cpf, sub, "https://issuer", Guid.CreateVersion7()).Value!;

        for (int i = 1; i <= quantidade; i++)
        {
            Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal) { ["APELIDO"] = JsonSerializer.SerializeToElement($"versao-{i}") };
            Result<ComprovanteInscricao> submetido = inscricao.Submeter(
                $"0000000{i}",
                null,
                respostas,
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
                [],
                TestSupport.DefinicoesDeTeste.Das(respostas),
                FormularioDeTeste.AvaliacaoSemOcultos,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                TestSupport.DefinicoesDeTeste.Nenhuma(),
                PrecondicaoIfMatch.DeTags([inscricao.ETag]),
                new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero).AddMinutes(i));
            submetido.IsSuccess.Should().BeTrue(submetido.Error?.Message);
        }

        return inscricao;
    }

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<ComprovanteInscricaoDto> resultado = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(ProcessoId, 1), inscricaoRepository, userContext, CancellationToken.None);

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

        Result<ComprovanteInscricaoDto> resultado = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(ProcessoId, 1), inscricaoRepository, UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Fact(DisplayName = "Sub diferente do gravado para o mesmo CPF é recusado como dono divergente")]
    public async Task Handle_SubDivergente_RecusaComoDonoDivergente()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = InscricaoComComprovantes(cpf, "sub-original", quantidade: 1);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        inscricaoRepository.ObterComprovantesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(existente.Comprovantes);

        Result<ComprovanteInscricaoDto> resultado = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(ProcessoId, 1), inscricaoRepository, UsuarioComCpf(cpf, sub: "sub-outro"), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
    }

    [Fact(DisplayName = "Comprovante 1 é a primeira submissão")]
    public async Task Handle_Comprovante1_DevolveAPrimeiraSubmissao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao existente = InscricaoComComprovantes(cpf, "sub-1", quantidade: 1);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        inscricaoRepository.ObterComprovantesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(existente.Comprovantes);

        Result<ComprovanteInscricaoDto> resultado = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(ProcessoId, 1), inscricaoRepository, UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.NumeroInscricao.Should().Be("00000001");
        resultado.Value!.CpfMascarado.Should().Be(cpf.Mascarado);
        resultado.Value!.Respostas.Should().ContainKey("APELIDO").WhoseValue.GetString().Should().Be("versao-1");
    }

    [Fact(DisplayName = "Comprovante 2 é a correção, distinto do comprovante 1 — append-only (CA-01)")]
    public async Task Handle_Comprovante2_DevolveACorrecaoSemAlterarAPrimeira()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao existente = InscricaoComComprovantes(cpf, "sub-1", quantidade: 2);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        inscricaoRepository.ObterComprovantesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(existente.Comprovantes);

        Result<ComprovanteInscricaoDto> primeiro = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(ProcessoId, 1), inscricaoRepository, UsuarioComCpf(cpf), CancellationToken.None);
        Result<ComprovanteInscricaoDto> segundo = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(ProcessoId, 2), inscricaoRepository, UsuarioComCpf(cpf), CancellationToken.None);

        primeiro.Value!.Respostas["APELIDO"].GetString().Should().Be("versao-1");
        segundo.Value!.Respostas["APELIDO"].GetString().Should().Be("versao-2");
    }

    [Theory(DisplayName = "Número fora do intervalo emitido recusa como comprovante não encontrado")]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public async Task Handle_NumeroForaDoIntervalo_RecusaComoNaoEncontrado(int numero)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao existente = InscricaoComComprovantes(cpf, "sub-1", quantidade: 1);

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(existente);
        inscricaoRepository.ObterComprovantesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(existente.Comprovantes);

        Result<ComprovanteInscricaoDto> resultado = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(ProcessoId, numero), inscricaoRepository, UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.ComprovanteNaoEncontrado");
    }
}
