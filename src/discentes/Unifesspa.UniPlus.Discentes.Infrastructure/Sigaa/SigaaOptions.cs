namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;

/// <summary>
/// Configuração de acesso à API do SIGAA, de onde vêm os vínculos de discentes.
/// Mapeia a seção <c>Sigaa</c> da configuração.
/// </summary>
/// <remarks>
/// A senha, em <see cref="Credenciais"/>, é credencial de sistema institucional e por isso
/// não tem chave correspondente em nenhum arquivo versionado — nem vazia. Chega ao processo
/// pela variável de ambiente <c>Sigaa__Senha</c>, populada a partir do cofre pelo
/// operador de segredos do cluster; em máquina de desenvolvimento, por <c>dotnet user-secrets</c>.
/// Toda duração é um <see cref="TimeSpan"/> e se escreve como <c>hh:mm:ss</c>.
/// </remarks>
public sealed class SigaaOptions
{
    public const string SectionName = "Sigaa";

    /// <summary>
    /// Endereço base da API, sem o caminho do recurso
    /// (ex.: <c>https://api-sigaa-v2.unifesspa.edu.br</c>).
    /// </summary>
    public Uri? BaseUrl { get; init; }

    /// <summary>Usuário de serviço do SIGAA (chave <c>Sigaa:Usuario</c>).</summary>
    public string Usuario { get; init; } = string.Empty;

    /// <summary>Senha do usuário de serviço (chave <c>Sigaa:Senha</c>); em execução real vem do cofre.</summary>
    public string Senha { get; init; } = string.Empty;

    /// <summary>As credenciais montadas a partir de <see cref="Usuario"/> e <see cref="Senha"/>; não é uma chave de configuração.</summary>
    public CredenciaisDoSigaa Credenciais => new(Usuario, Senha);

    /// <summary>Quanto vale o token e quando renová-lo.</summary>
    public ParametrosDoTokenDoSigaa Token { get; init; } = new();

    /// <summary>Tempos limite, retentativas e interrupção por falhas das chamadas.</summary>
    public ResilienciaDoSigaa Resiliencia { get; init; } = new();

    /// <summary>Tamanho da página e quantas páginas buscar ao mesmo tempo.</summary>
    public PaginacaoDoSigaa Paginacao { get; init; } = new();
}
