namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Microsoft.Extensions.Options;

/// <summary>Segurança da conexão com o relay.</summary>
public enum SegurancaSmtp
{
    /// <summary>Sem criptografia (só captura local).</summary>
    Nenhuma = 0,

    /// <summary>Conexão aberta em claro e promovida por STARTTLS (porta 587).</summary>
    StartTls = 1,

    /// <summary>TLS desde o início da conexão (porta 465).</summary>
    TlsImplicito = 2,
}

/// <summary>Relay SMTP institucional (seção <c>Email:Smtp</c>).</summary>
public sealed class EmailSmtpOptions
{
    public const string SectionName = "Email:Smtp";

    /// <summary>O maior tempo limite aceito: acima disso, um relay lento volta a prender a fila.</summary>
    public static readonly TimeSpan TempoLimiteMaximo = TimeSpan.FromMinutes(5);

    public string Host { get; set; } = string.Empty;

    public int Porta { get; set; }

    public SegurancaSmtp Seguranca { get; set; } = SegurancaSmtp.StartTls;

    /// <summary>Opcional: o servidor de captura local não autentica.</summary>
    public string? Usuario { get; set; }

    public string? Senha { get; set; }

    public string Remetente { get; set; } = string.Empty;

    public string? NomeRemetente { get; set; }

    /// <summary>Prazo para abrir a conexão, negociar o TLS e receber a saudação do relay.</summary>
    public TimeSpan TempoLimiteDeConexao { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>O maior teto de envios simultâneos aceito: mais do que isso é rajada contra o relay institucional.</summary>
    public const int EnviosSimultaneosMaximo = 20;

    /// <summary>Prazo para autenticar e entregar a mensagem, e de cada leitura ou escrita na conexão.</summary>
    public TimeSpan TempoLimiteDeEnvio { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Teto de mensagens entregues ao mesmo tempo, e portanto de conexões abertas com o relay. Explícito para não variar
    /// com a CPU do pod: uma conexão lenta prende só uma das vagas, e o relay não recebe uma rajada de conexões que
    /// acione a limitação de taxa dele.
    /// </summary>
    public int EnviosSimultaneos { get; set; } = 5;
}

internal sealed class EmailSmtpOptionsValidator : IValidateOptions<EmailSmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailSmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> falhas = [];
        ValidarRelay(options, falhas);
        ValidarCredencial(options, falhas);
        ValidarTempoLimite(nameof(EmailSmtpOptions.TempoLimiteDeConexao), options.TempoLimiteDeConexao, falhas);
        ValidarTempoLimite(nameof(EmailSmtpOptions.TempoLimiteDeEnvio), options.TempoLimiteDeEnvio, falhas);
        ValidarEnviosSimultaneos(options.EnviosSimultaneos, falhas);

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static void ValidarRelay(EmailSmtpOptions options, List<string> falhas)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}:Host é obrigatório.");
        }

        if (options.Porta is < 1 or > 65535)
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}:Porta deve estar entre 1 e 65535.");
        }

        if (!EnderecoSmtp.EhAddrSpecComDominio(options.Remetente))
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}:Remetente é obrigatório e deve ser um endereço puro com domínio (sem nome de exibição).");
        }
    }

    private static void ValidarCredencial(EmailSmtpOptions options, List<string> falhas)
    {
        if (string.IsNullOrEmpty(options.Usuario) != string.IsNullOrEmpty(options.Senha))
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}:Usuario e Senha devem ser informados juntos.");
        }

        if (options.Seguranca == SegurancaSmtp.Nenhuma && !string.IsNullOrEmpty(options.Usuario))
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}: credencial não pode trafegar sem TLS (Seguranca=Nenhuma).");
        }
    }

    internal static void ValidarEnviosSimultaneos(int valor, List<string> falhas)
    {
        if (valor is < 1 or > EmailSmtpOptions.EnviosSimultaneosMaximo)
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}:EnviosSimultaneos deve estar entre 1 e {EmailSmtpOptions.EnviosSimultaneosMaximo}.");
        }
    }

    private static void ValidarTempoLimite(string nome, TimeSpan valor, List<string> falhas)
    {
        if (valor <= TimeSpan.Zero || valor > EmailSmtpOptions.TempoLimiteMaximo)
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}:{nome} deve ser maior que zero e de no máximo {EmailSmtpOptions.TempoLimiteMaximo.TotalMinutes:0} minutos.");
        }
    }
}
