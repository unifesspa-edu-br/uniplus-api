namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json;

/// <summary>
/// O rascunho do bloco do ato, como o passo de Revisão o guarda entre uma sessão e outra.
/// </summary>
/// <param name="Versao">
/// Versão do <b>formato</b> de <paramref name="Conteudo"/>. É o cliente que a define e a
/// interpreta: o servidor só a devolve, para que uma versão futura da tela saiba distinguir o
/// que consegue reidratar do que teria de traduzir pela metade.
/// </param>
/// <param name="Conteudo">
/// Documento JSON <b>opaco</b>. O servidor guarda e devolve sem interpretar — não conhece os
/// campos do ato (ADR-0108) e não passa a conhecê-los por guardar o bloco.
/// </param>
public sealed record SalvarRascunhoDaPublicacaoRequest(int Versao, JsonElement Conteudo);
