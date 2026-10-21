namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Security.Cryptography;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A confirmação do arquivo do candidato: o antivírus olha o conteúdo antes de qualquer gravação, e o
/// que vai ao armazenamento selado é o conteúdo cifrado.
/// </summary>
public sealed class ConfirmacaoDoArquivoDoCandidatoTests
{
    private static readonly byte[] Conteudo = [.. "%PDF-1.7 laudo médico"u8];
    private static readonly byte[] Cifrado = [1, 2, 3, 4];

    private readonly IDocumentoDaInscricaoRepository _repositorio = Substitute.For<IDocumentoDaInscricaoRepository>();
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();
    private readonly IProtecaoDoConteudoSelado _protecao = Substitute.For<IProtecaoDoConteudoSelado>();
    private readonly ArquivoDaApresentacao _arquivo;

    public ConfirmacaoDoArquivoDoCandidatoTests()
    {
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "LAUDO", "Laudo médico", "SAUDE",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;
        _arquivo = ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), Guid.CreateVersion7(), exigencia, "application/pdf", Conteudo.Length, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;

        _storage.ObterInfoAsync(_arquivo.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(Conteudo.Length, "application/pdf"));
        _storage.AbrirLeituraAsync(_arquivo.ObjectKey, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(Conteudo));
        _repositorio.TentarReivindicarConfirmacaoAsync(_arquivo.Id, Arg.Any<CancellationToken>()).Returns(true);
        _protecao.VerificarAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _protecao.SelarAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Cifrado);
    }

    private Task<Result> Confirmar(Func<CancellationToken, Task<Result>>? aoReivindicar = null) =>
        ConfirmacaoDoArquivoEnviado.ConfirmarAsync(
            _arquivo, _arquivo.TamanhoMaximoBytes,
            new ConfirmacaoDoArquivoEnviado.Recusas(
                new DomainError("x.objeto", "objeto"), ArquivoDaApresentacao.TamanhoExcedidoAte(_arquivo.TamanhoMaximoBytes), ArquivoDaApresentacao.StatusInvalidoParaConfirmacao),
            _repositorio, _storage, TimeProvider.System, CancellationToken.None, _protecao, aoReivindicar);

    [Theory(DisplayName = "Arquivo infectado ou não verificado recusa a confirmação: nada é gravado e o arquivo continua pendente")]
    [InlineData(DocumentoDaInscricaoErrorCodes.ArquivoInfectado)]
    [InlineData(DocumentoDaInscricaoErrorCodes.AntivirusIndisponivel)]
    public async Task Confirmar_AntivirusRecusa_NaoGravaNada(string codigo)
    {
        _protecao.VerificarAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError(codigo, "recusa")));

        Result resultado = await Confirmar();

        resultado.Error!.Code.Should().Be(codigo);
        _arquivo.Status.Should().Be(StatusArquivoEnviado.Pendente);
        await _repositorio.DidNotReceive().TentarReivindicarConfirmacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().SalvarConteudoSeladoAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Arquivo limpo é selado cifrado: o armazenamento recebe o conteúdo cifrado, nunca o claro, e o hash é o do claro")]
    public async Task Confirmar_Limpo_SelaOConteudoCifrado()
    {
        Result resultado = await Confirmar();

        resultado.IsSuccess.Should().BeTrue();
        _arquivo.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(Conteudo)));
        await _storage.Received(1).SalvarConteudoSeladoAsync(
            _arquivo.ChaveSelada, Arg.Is<byte[]>(b => b.SequenceEqual(Cifrado)), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().SalvarConteudoSeladoAsync(
            Arg.Any<string>(), Arg.Is<byte[]>(b => b.SequenceEqual(Conteudo)), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Perdida a disputa pela inscrição, o arquivo não é reivindicado nem gravado")]
    public async Task Confirmar_DisputaPerdida_NaoReivindicaNemGrava()
    {
        DomainError perdida = new("x.inscricao_alterada", "perdida");

        Result resultado = await Confirmar(_ => Task.FromResult(Result.Failure(perdida)));

        resultado.Error.Should().Be(perdida);
        await _repositorio.DidNotReceive().TentarReivindicarConfirmacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().SalvarConteudoSeladoAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
