namespace Unifesspa.UniPlus.Verificador.Configuracao;

using Microsoft.Extensions.Options;

/// <summary>
/// Barra a subida de um verificador configurado contra o clamd. O arquivo acima do
/// <c>StreamMaxLength</c> deixa o clamd sem veredicto, e pedidos em paralelo que ocupam todas as
/// <c>MaxThreads</c> deixam o PING da liveness e o VERSION esperando atrás das varreduras.
/// </summary>
public sealed class ValidadorDasOpcoes : IValidateOptions<VerificadorOptions>
{
    public ValidateOptionsResult Validate(string? name, VerificadorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> falhas =
        [
            .. FalhasDoTamanhoMaximo(options),
            .. FalhasDoParalelismo(options),
            .. FalhasDoClamd(options.Clamd),
            .. FalhasDoArmazenamento(options.Armazenamento),
            .. FalhasDoRabbitMq(options.RabbitMq),
        ];

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static IEnumerable<string> FalhasDoTamanhoMaximo(VerificadorOptions options)
    {
        if (options.TamanhoMaximoBytes <= 0)
        {
            yield return "O tamanho máximo do arquivo deve ser maior que zero.";
        }
        else if (options.TamanhoMaximoBytes >= options.Clamd.StreamMaxLengthBytes)
        {
            yield return "O tamanho máximo do arquivo deve ficar abaixo do StreamMaxLength do clamd.";
        }
    }

    private static IEnumerable<string> FalhasDoParalelismo(VerificadorOptions options)
    {
        if (options.Paralelismo < 1)
        {
            yield return "O paralelismo deve ser de pelo menos 1.";
        }
        else if (options.Paralelismo >= options.Clamd.MaxThreads)
        {
            // A thread que sobra atende o PING e o VERSION; sem ela, eles esperam atrás das varreduras.
            yield return "O paralelismo deve ficar abaixo do MaxThreads do clamd.";
        }
    }

    private static IEnumerable<string> FalhasDoClamd(ClamdOptions clamd)
    {
        if (clamd.TimeoutSegundos <= 0)
        {
            yield return "O prazo do clamd deve ser maior que zero.";
        }

        if (clamd.Port <= 0)
        {
            yield return "A porta do clamd deve ser maior que zero.";
        }
    }

    private static IEnumerable<string> FalhasDoArmazenamento(ArmazenamentoOptions armazenamento) =>
        FalhaSeVazio(armazenamento.Endpoint, "O endereço do MinIO é obrigatório.")
            .Concat(FalhaSeVazio(armazenamento.AccessKey, "A chave de acesso do MinIO é obrigatória."))
            .Concat(FalhaSeVazio(armazenamento.SecretKey, "A chave secreta do MinIO é obrigatória."))
            .Concat(FalhaSeVazio(armazenamento.BucketEnvios, "O bucket de envio é obrigatório."))
            .Concat(FalhaSeVazio(armazenamento.BucketArquivos, "O bucket definitivo é obrigatório."));

    private static IEnumerable<string> FalhasDoRabbitMq(RabbitMqOptions rabbit)
    {
        if (string.IsNullOrWhiteSpace(rabbit.Uri))
        {
            return FalhaSeVazio(rabbit.Host, "O endereço do RabbitMQ é obrigatório.");
        }

        return Uri.TryCreate(rabbit.Uri, UriKind.Absolute, out _) ? [] : ["A URI do RabbitMQ é inválida."];
    }

    private static IEnumerable<string> FalhaSeVazio(string valor, string mensagem) =>
        string.IsNullOrWhiteSpace(valor) ? [mensagem] : [];
}
