namespace Unifesspa.UniPlus.Selecao.Infrastructure.Antivirus;

/// <summary>Onde está o clamd que verifica o arquivo do candidato (seção <c>Clamd</c>).</summary>
public sealed class ClamdOptions
{
    public const string SectionName = "Clamd";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 3310;

    /// <summary>Prazo da verificação inteira, da conexão à resposta.</summary>
    public int TimeoutSegundos { get; set; } = 30;
}
