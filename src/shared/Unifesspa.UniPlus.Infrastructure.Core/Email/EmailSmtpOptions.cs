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

    public string Host { get; set; } = string.Empty;

    public int Porta { get; set; }

    public SegurancaSmtp Seguranca { get; set; } = SegurancaSmtp.StartTls;

    /// <summary>Opcional: o servidor de captura local não autentica.</summary>
    public string? Usuario { get; set; }

    public string? Senha { get; set; }

    public string Remetente { get; set; } = string.Empty;

    public string? NomeRemetente { get; set; }
}

internal sealed class EmailSmtpOptionsValidator : IValidateOptions<EmailSmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailSmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> falhas = [];

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

        if (string.IsNullOrEmpty(options.Usuario) != string.IsNullOrEmpty(options.Senha))
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}:Usuario e Senha devem ser informados juntos.");
        }

        if (options.Seguranca == SegurancaSmtp.Nenhuma && !string.IsNullOrEmpty(options.Usuario))
        {
            falhas.Add($"{EmailSmtpOptions.SectionName}: credencial não pode trafegar sem TLS (Seguranca=Nenhuma).");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }
}
