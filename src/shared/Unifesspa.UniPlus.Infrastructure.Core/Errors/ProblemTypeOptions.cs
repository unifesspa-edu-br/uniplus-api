namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Base do campo <c>type</c> do corpo de erro RFC 9457: a URI sob a qual o catálogo
/// público publica a página de cada <c>code</c> (ADR-0023 e ADR-0024).
/// </summary>
/// <remarks>
/// <para>É configuração, e não constante de compilação, porque o endereço do catálogo
/// muda por decisão de infraestrutura, fora do controle do backend: enquanto o CNAME do
/// domínio institucional não é provisionado, o catálogo responde pelo bridge de GitHub
/// Pages do portal; emitir o domínio canônico antes disso devolveria ao consumidor um
/// link morto, e o <c>type</c> existe justamente para levá-lo à explicação da causa.</para>
/// <para>Sem valor default no código: cada ambiente declara o seu, e configuração
/// ausente derruba o boot em vez de emitir um <c>type</c> que não leva a lugar nenhum.</para>
/// </remarks>
public sealed class ProblemTypeOptions
{
    /// <summary>Seção de configuração (<c>ProblemType</c> em appsettings / <c>ProblemType__*</c> em env var).</summary>
    public const string SectionName = "ProblemType";

    /// <summary>
    /// URI absoluta HTTPS do catálogo, à qual o <c>code</c> do erro é concatenado.
    /// A barra final é opcional na configuração — a fábrica normaliza.
    /// </summary>
    public string BaseUri { get; set; } = string.Empty;
}
