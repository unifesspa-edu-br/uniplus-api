namespace Unifesspa.UniPlus.Verificador.UnitTests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Observabilidade;
using Unifesspa.UniPlus.Verificador.Verificacao;

internal sealed class ArmazenamentoFalso : IArmazenamentoDeArquivos
{
    public ObjetoArmazenado? Definitivo { get; set; }

    public ObjetoArmazenado? Envio { get; set; }

    public byte[] Conteudo { get; set; } = [];

    public bool VersaoLegivel { get; set; } = true;

    public ObjetoArmazenado? DefinitivoAoLer { get; set; }

    public int Leituras { get; private set; }

    public int Promocoes { get; private set; }

    public string? ContentTypePromovido { get; private set; }

    public int Remocoes { get; private set; }

    public Task<ObjetoArmazenado?> ConsultarDefinitivoAsync(string chave, CancellationToken cancellationToken) => Task.FromResult(Definitivo);

    public Task<ObjetoArmazenado?> ConsultarEnvioAsync(string chave, CancellationToken cancellationToken) => Task.FromResult(Envio);

    public async Task<bool> LerEnvioAsync(string chave, string etag, Stream destino, CancellationToken cancellationToken)
    {
        Leituras++;
        if (DefinitivoAoLer is not null)
        {
            Definitivo = DefinitivoAoLer;
        }

        if (!VersaoLegivel)
        {
            return false;
        }

        await destino.WriteAsync(Conteudo, cancellationToken);
        return true;
    }

    public Task PromoverAsync(string chaveDeEnvio, string etag, string chaveDefinitiva, string contentType, string sha256, CancellationToken cancellationToken)
    {
        Promocoes++;
        ContentTypePromovido = contentType;
        return Task.CompletedTask;
    }

    public Task RemoverEnvioAsync(string chave, string etag, CancellationToken cancellationToken)
    {
        Remocoes++;
        return Task.CompletedTask;
    }
}

internal sealed class ClamdFalso : IClamd
{
    public ResultadoDoClamd Resposta { get; set; } = new(false, null);

    public Exception? Falha { get; set; }

    public int Verificacoes { get; private set; }

    public Task<ResultadoDoClamd> VerificarAsync(Stream conteudo, CancellationToken cancellationToken)
    {
        Verificacoes++;
        return Falha is null ? Task.FromResult(Resposta) : Task.FromException<ResultadoDoClamd>(Falha);
    }

    public Task<DateTimeOffset> LerDataDaBaseAsync(CancellationToken cancellationToken) => Task.FromResult(DateTimeOffset.UnixEpoch);
}

internal static class Cenario
{
    public const string ETag = "\"etag-confirmado\"";

    public static PedidoDeVerificacaoDeArquivo Pedido(FormatoDoArquivo formato = FormatoDoArquivo.Pdf, long maximo = 20L * 1024 * 1024) =>
        new(Guid.NewGuid(), "envio/chave", ETag, "definitivo/chave", formato, maximo);

    public static ObjetoArmazenado EnvioComETag(string etag, long tamanho = 1024) => new(etag, tamanho, null, null);

    public static byte[] Fixture(string nome) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", nome));

    public static VerificarArquivoHandler Handler(IArmazenamentoDeArquivos armazenamento, ClamdFalso clamd) =>
        new(
            armazenamento,
            clamd,
            new MetricasDoVerificador(TimeProvider.System),
            Options.Create(new VerificadorOptions()),
            NullLogger<VerificarArquivoHandler>.Instance);
}
