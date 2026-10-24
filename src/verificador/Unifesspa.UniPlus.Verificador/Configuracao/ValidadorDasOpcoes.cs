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

        List<string> falhas = [];

        if (options.TamanhoMaximoBytes <= 0)
        {
            falhas.Add("O tamanho máximo do arquivo deve ser maior que zero.");
        }
        else if (options.TamanhoMaximoBytes >= options.Clamd.StreamMaxLengthBytes)
        {
            falhas.Add("O tamanho máximo do arquivo deve ficar abaixo do StreamMaxLength do clamd.");
        }

        if (options.Paralelismo < 1)
        {
            falhas.Add("O paralelismo deve ser de pelo menos 1.");
        }
        else if (options.Paralelismo >= options.Clamd.MaxThreads)
        {
            // A thread que sobra atende o PING e o VERSION; sem ela, eles esperam atrás das varreduras.
            falhas.Add("O paralelismo deve ficar abaixo do MaxThreads do clamd.");
        }

        if (options.Clamd.TimeoutSegundos <= 0)
        {
            falhas.Add("O prazo do clamd deve ser maior que zero.");
        }

        if (options.Clamd.Port <= 0)
        {
            falhas.Add("A porta do clamd deve ser maior que zero.");
        }

        ExigirTexto(falhas, options.Armazenamento.Endpoint, "O endereço do MinIO é obrigatório.");
        ExigirTexto(falhas, options.Armazenamento.AccessKey, "A chave de acesso do MinIO é obrigatória.");
        ExigirTexto(falhas, options.Armazenamento.SecretKey, "A chave secreta do MinIO é obrigatória.");
        ExigirTexto(falhas, options.Armazenamento.BucketEnvios, "O bucket de envio é obrigatório.");
        ExigirTexto(falhas, options.Armazenamento.BucketArquivos, "O bucket definitivo é obrigatório.");
        ExigirTexto(falhas, options.RabbitMq.Host, "O endereço do RabbitMQ é obrigatório.");

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static void ExigirTexto(List<string> falhas, string valor, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            falhas.Add(mensagem);
        }
    }
}
