namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class RemocaoDeArquivosPendentesVencidosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>O instante até o qual o pendente conta como vencido: a tolerância antes de agora.</summary>
    private static readonly DateTimeOffset Corte = Agora - RemocaoDeArquivosPendentesVencidos.Tolerancia;

    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();
    private readonly IModeloDeDocumentoRepository _modelos = Substitute.For<IModeloDeDocumentoRepository>();
    private readonly IDocumentoDaInscricaoRepository _inscricoes = Substitute.For<IDocumentoDaInscricaoRepository>();
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();

    public RemocaoDeArquivosPendentesVencidosTests()
    {
        foreach (IArquivoEnviadoRepository repositorio in new IArquivoEnviadoRepository[] { _documentos, _modelos, _inscricoes })
        {
            repositorio.ListarPendentesVencidosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns([]);
            repositorio.RemoverSePendenteVencidoAsync(
                    Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
                .Returns(ExecutaAsRemocoesDosObjetos);
        }
    }

    [Fact(DisplayName = "O pendente vencido do documento do Edital e do modelo sai com todas as chaves de objeto")]
    public async Task PendenteVencido_RemoveTodasAsChavesDeObjeto()
    {
        ArquivoPendenteVencido documento = Pendente("documentos-a-publicar", 2);
        ArquivoPendenteVencido modelo = Pendente("modelos-de-documento", 2);
        _documentos.ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([documento]);
        _modelos.ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([modelo]);

        int removidos = await Remocao().ExecutarAsync(CancellationToken.None);

        removidos.Should().Be(2);
        string[] chaves = [.. documento.ChavesDeObjeto, .. modelo.ChavesDeObjeto];
        await Task.WhenAll(chaves.Select(chave => _storage.Received(1).RemoverAsync(chave, Arg.Any<CancellationToken>())));
    }

    [Fact(DisplayName = "O pendente do candidato sai sem tocar o armazenamento")]
    public async Task PendenteDoCandidato_NaoTocaOArmazenamento()
    {
        ArquivoPendenteVencido candidato = Pendente("candidato", 0);
        _inscricoes.ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([candidato]);

        int removidos = await Remocao().ExecutarAsync(CancellationToken.None);

        removidos.Should().Be(1);
        await _storage.DidNotReceive().RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Só conta como vencido o pendente que passou do prazo há mais que a tolerância")]
    public async Task Vencido_ContaATolerancia()
    {
        await Remocao().ExecutarAsync(CancellationToken.None);

        await _documentos.Received(1).ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _modelos.Received(1).ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _inscricoes.Received(1).ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Falha ao remover a cópia gravada não conta o registro como removido")]
    public async Task FalhaAoRemoverObjeto_NaoContaORegistro()
    {
        ArquivoPendenteVencido documento = Pendente("documentos-a-publicar", 2);
        _documentos.ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([documento]);
        _storage.RemoverAsync(documento.ChavesDeObjeto[1], Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("armazenamento indisponível"));

        int removidos = await Remocao().ExecutarAsync(CancellationToken.None);

        removidos.Should().Be(0);
    }

    [Fact(DisplayName = "Lote cheio sem falha leva à consulta do lote seguinte")]
    public async Task LoteCheio_ConsultaOLoteSeguinte()
    {
        ArquivoPendenteVencido[] primeiro = [.. Enumerable.Range(0, RemocaoDeArquivosPendentesVencidos.TamanhoDoLote)
            .Select(_ => Pendente("documentos-a-publicar", 1))];
        ArquivoPendenteVencido ultimo = Pendente("documentos-a-publicar", 1);
        _documentos.ListarPendentesVencidosAsync(Corte, RemocaoDeArquivosPendentesVencidos.TamanhoDoLote, Arg.Any<CancellationToken>())
            .Returns(primeiro, [ultimo]);

        int removidos = await Remocao().ExecutarAsync(CancellationToken.None);

        removidos.Should().Be(RemocaoDeArquivosPendentesVencidos.TamanhoDoLote + 1);
        await _documentos.Received(1).RemoverSePendenteVencidoAsync(
            ultimo.Id, Corte, Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lote com falha encerra a execução em vez de insistir contra o armazenamento")]
    public async Task LoteComFalha_EncerraAExecucao()
    {
        ArquivoPendenteVencido[] lote = [.. Enumerable.Range(0, RemocaoDeArquivosPendentesVencidos.TamanhoDoLote)
            .Select(_ => Pendente("documentos-a-publicar", 1))];
        // A segunda consulta devolveria o mesmo lote — o que falhou continua vencido.
        _documentos.ListarPendentesVencidosAsync(Corte, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(lote, lote, []);
        _storage.RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("armazenamento indisponível"));

        await Remocao().ExecutarAsync(CancellationToken.None);

        await _documentos.Received(1)
            .ListarPendentesVencidosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private RemocaoDeArquivosPendentesVencidos Remocao() => new(
        _documentos, _modelos, _inscricoes, _storage, Substitute.For<IArquivoDoCandidatoStorage>(), new RelogioFixo(Agora), NullLogger<RemocaoDeArquivosPendentesVencidos>.Instance);

    private static async Task<bool> ExecutaAsRemocoesDosObjetos(NSubstitute.Core.CallInfo chamada)
    {
        await chamada.Arg<Func<CancellationToken, Task>>()(chamada.Arg<CancellationToken>()).ConfigureAwait(false);
        return true;
    }

    private static ArquivoPendenteVencido Pendente(string prefixo, int quantidadeDeChaves)
    {
        Guid id = Guid.CreateVersion7();
        return new ArquivoPendenteVencido(
            id, [.. Enumerable.Range(0, quantidadeDeChaves).Select(n => $"selecao/{prefixo}/{id:D}/{n}.bin")]);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
