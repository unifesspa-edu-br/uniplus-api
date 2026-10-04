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

    private readonly IDocumentoEditalRepository _documentos = Substitute.For<IDocumentoEditalRepository>();
    private readonly IModeloDeDocumentoRepository _modelos = Substitute.For<IModeloDeDocumentoRepository>();
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();

    public RemocaoDeArquivosPendentesVencidosTests()
    {
        _documentos.ListarPendentesVencidosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _modelos.ListarPendentesVencidosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _documentos.RemoverSePendenteVencidoAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _modelos.RemoverSePendenteVencidoAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact(DisplayName = "O pendente vencido do documento do Edital e do modelo sai com o objeto, e o objeto sai antes do registro")]
    public async Task PendenteVencido_RemoveObjetoEDepoisORegistro()
    {
        ArquivoPendenteVencido documento = Pendente("documentos-edital");
        ArquivoPendenteVencido modelo = Pendente("modelos-de-documento");
        _documentos.ListarPendentesVencidosAsync(Agora, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([documento]);
        _modelos.ListarPendentesVencidosAsync(Agora, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([modelo]);

        int removidos = await Remocao().ExecutarAsync(CancellationToken.None);

        removidos.Should().Be(2);
        Received.InOrder(() =>
        {
            _storage.RemoverAsync(documento.ObjectKey, Arg.Any<CancellationToken>());
            _documentos.RemoverSePendenteVencidoAsync(documento.Id, Agora, Arg.Any<CancellationToken>());
            _storage.RemoverAsync(modelo.ObjectKey, Arg.Any<CancellationToken>());
            _modelos.RemoverSePendenteVencidoAsync(modelo.Id, Agora, Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Falha ao remover o objeto mantém o registro para a próxima execução")]
    public async Task FalhaAoRemoverObjeto_MantemORegistro()
    {
        ArquivoPendenteVencido documento = Pendente("documentos-edital");
        _documentos.ListarPendentesVencidosAsync(Agora, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([documento]);
        _storage.RemoverAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("armazenamento indisponível"));

        int removidos = await Remocao().ExecutarAsync(CancellationToken.None);

        removidos.Should().Be(0);
        await _documentos.DidNotReceive()
            .RemoverSePendenteVencidoAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lote cheio sem falha leva à consulta do lote seguinte")]
    public async Task LoteCheio_ConsultaOLoteSeguinte()
    {
        ArquivoPendenteVencido[] primeiro = [.. Enumerable.Range(0, RemocaoDeArquivosPendentesVencidos.TamanhoDoLote)
            .Select(_ => Pendente("documentos-edital"))];
        ArquivoPendenteVencido ultimo = Pendente("documentos-edital");
        _documentos.ListarPendentesVencidosAsync(Agora, RemocaoDeArquivosPendentesVencidos.TamanhoDoLote, Arg.Any<CancellationToken>())
            .Returns(primeiro, [ultimo]);

        int removidos = await Remocao().ExecutarAsync(CancellationToken.None);

        removidos.Should().Be(RemocaoDeArquivosPendentesVencidos.TamanhoDoLote + 1);
        await _documentos.Received(1).RemoverSePendenteVencidoAsync(ultimo.Id, Agora, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lote com falha encerra a execução em vez de insistir contra o armazenamento")]
    public async Task LoteComFalha_EncerraAExecucao()
    {
        ArquivoPendenteVencido[] lote = [.. Enumerable.Range(0, RemocaoDeArquivosPendentesVencidos.TamanhoDoLote)
            .Select(_ => Pendente("documentos-edital"))];
        // A segunda consulta devolveria o mesmo lote — o que falhou continua vencido.
        _documentos.ListarPendentesVencidosAsync(Agora, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(lote, lote, []);
        _storage.RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("armazenamento indisponível"));

        await Remocao().ExecutarAsync(CancellationToken.None);

        await _documentos.Received(1)
            .ListarPendentesVencidosAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private RemocaoDeArquivosPendentesVencidos Remocao() => new(
        _documentos, _modelos, _storage, new RelogioFixo(Agora), NullLogger<RemocaoDeArquivosPendentesVencidos>.Instance);

    private static ArquivoPendenteVencido Pendente(string prefixo)
    {
        Guid id = Guid.CreateVersion7();
        return new ArquivoPendenteVencido(id, $"selecao/{prefixo}/{Guid.CreateVersion7():D}/{id:D}.bin");
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
