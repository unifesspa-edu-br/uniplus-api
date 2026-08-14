namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Kernel.Results;

/// <summary>
/// Resolve a zona horária institucional contra a base de fusos do runtime, devolvendo recusa
/// nomeada em vez de exceção quando ela não é reconhecida.
/// </summary>
/// <remarks>
/// <para>Existe como porta, e não como acesso direto a <c>TimeZoneInfo</c>, por dois motivos. O
/// primeiro é que a falha é real e precisa ser tratável: um container sem <c>tzdata</c> faz
/// <c>FindSystemTimeZoneById</c> lançar, e uma versão publicada sob zona irresolvível teria âncora
/// que não converte para dia civil algum. O segundo é que esse caminho precisa ser exercitável em
/// teste sem depender do sistema de arquivos do runner.</para>
/// <para>A recusa é <strong>defeito da instalação, não da requisição</strong> — quem publica não
/// declara fuso, e nenhum campo da configuração produz esse erro. Por isso o código mapeia para 500,
/// junto com as outras causas internas do módulo.</para>
/// </remarks>
public interface IResolvedorFusoInstitucional
{
    /// <summary>
    /// Zona institucional resolvida, ou <c>ProcessoSeletivo.FusoInstitucionalNaoReconhecido</c>
    /// quando a base de fusos do runtime não a reconhece.
    /// </summary>
    Result<TimeZoneInfo> Resolver();
}
