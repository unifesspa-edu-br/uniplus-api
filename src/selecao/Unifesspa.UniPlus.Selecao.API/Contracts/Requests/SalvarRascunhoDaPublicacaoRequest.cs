namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json;
using System.Text.Json.Serialization;

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
/// <remarks>
/// <c>[JsonRequired]</c> em <paramref name="Conteudo"/> porque o parâmetro de construtor de
/// record não é exigido pelo System.Text.Json: omitido, ele chega como <c>JsonElement</c>
/// default (<c>ValueKind.Undefined</c>), e a primeira coisa que o handler faz com ele é
/// <c>GetRawText()</c>, que lança — um corpo malformado viraria 500. Com o atributo, a omissão
/// é recusada na desserialização, que é 400 e diz o que falta.
/// </remarks>
public sealed record SalvarRascunhoDaPublicacaoRequest(
    int Versao,
    [property: JsonRequired] JsonElement Conteudo);
