namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

/// <summary>
/// Recupera vínculos de discentes do SIGAA para a sincronização da réplica.
/// </summary>
/// <remarks>
/// É a fronteira entre quem orquestra a sincronização e o que a origem oferece: acima
/// dela ninguém precisa saber de token, tamanho de página ou instabilidade de rede.
/// </remarks>
public interface ISigaaVinculoDiscenteClient
{
    /// <summary>
    /// Busca uma página de vínculos.
    /// </summary>
    /// <param name="filtro">Recorte pedido à origem.</param>
    /// <param name="pagina">Página desejada, começando em um.</param>
    Task<PaginaDeVinculos> ObterPaginaAsync(
        FiltroDeVinculos filtro,
        int pagina,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Recorte de vínculos pedido à origem. Só expõe os cortes que ela sabe aplicar.
/// </summary>
/// <param name="Nivel">Nível de ensino; a sincronização pede graduação.</param>
/// <param name="AnoIngressoMinimo">
/// Ano de ingresso a partir do qual os vínculos interessam, quando o recorte é por idade
/// do vínculo.
/// </param>
/// <param name="Situacoes">
/// Situações acadêmicas de interesse. Vazio não filtra por situação.
/// </param>
public sealed record FiltroDeVinculos(
    string Nivel,
    int? AnoIngressoMinimo = null,
    IReadOnlyList<int>? Situacoes = null);

/// <summary>
/// Uma página de vínculos como a origem a devolveu.
/// </summary>
/// <param name="Itens">Vínculos da página. Página vazia é resposta legítima.</param>
/// <param name="TotalDeItensNaOrigem">
/// Quantos vínculos o filtro alcança ao todo, segundo a origem, no instante desta
/// resposta. Serve para dimensionar a varredura — não é promessa de que a varredura
/// inteira encontrará exatamente essa quantidade, porque a base continua sendo alterada
/// enquanto as páginas são lidas.
/// </param>
/// <param name="TemProximaPagina">
/// Se a origem indicou haver página seguinte. É a condição de parada que não depende do
/// total declarado.
/// </param>
public sealed record PaginaDeVinculos(
    IReadOnlyList<VinculoDiscentePayload> Itens,
    int? TotalDeItensNaOrigem,
    bool TemProximaPagina);
