namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

/// <summary>Configuração da instalação dos primeiros gestores (<c>Autorizacao:Instalacao</c>).</summary>
public sealed class InstalacaoOptions
{
    public const string SectionName = "Autorizacao:Instalacao";

    /// <summary>
    /// Domínios de e-mail em que o código de confirmação pode ser entregue: o e-mail do token que
    /// vem do gov.br pode ser pessoal, e o código só vai a e-mail institucional. Vazia, vale
    /// <c>unifesspa.edu.br</c>.
    /// </summary>
    public string[] DominiosInstitucionais { get; init; } = [];
}
