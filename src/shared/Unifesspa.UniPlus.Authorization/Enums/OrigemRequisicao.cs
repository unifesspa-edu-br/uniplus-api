namespace Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Canal pelo qual a requisição autorizada chegou ao sistema, usado como
/// atributo de contexto na decisão de autorização (ADR-0078).
/// </summary>
/// <remarks>
/// Os identificadores C# estão em PascalCase; a serialização externa usa o
/// valor canônico em <c>kebab-case</c>/<c>snake_case</c> conforme indicado em
/// cada membro. A serialização canônica é responsabilidade da camada de borda
/// (outra story), não deste tipo de contrato em memória.
/// </remarks>
public enum OrigemRequisicao
{
    /// <summary>API HTTP pública. Valor canônico: <c>api</c>.</summary>
    Api = 0,

    /// <summary>Processamento assíncrono (jobs/outbox). Valor canônico: <c>jobs</c>.</summary>
    Jobs = 1,

    /// <summary>Ferramenta administrativa de linha de comando. Valor canônico: <c>admin-cli</c>/<c>admin_cli</c>.</summary>
    AdminCli = 2,
}
