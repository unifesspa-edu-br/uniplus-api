namespace Unifesspa.UniPlus.Selecao.API.Http;

using Microsoft.Net.Http.Headers;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Decodifica o header <c>If-Match</c> no <b>boundary</b> (ADR-0031): o que atravessa para
/// dentro é o value object <see cref="PrecondicaoIfMatch"/>, nunca o texto cru.
/// </summary>
/// <remarks>
/// <para>
/// A divisão é a que a ADR-0110 D5 fixa: <b>o transporte valida só a sintaxe</b>; quem
/// decide se a precondição é <b>obrigatória</b>, e se ela <b>casa</b>, é o handler — sob o
/// lock, com o agregado carregado, porque a obrigatoriedade depende do estado (há sessão
/// editorial aberta?) e ninguém antes do handler sabe disso.
/// </para>
/// </remarks>
public static class IfMatchHeader
{
    /// <summary>
    /// <see cref="PrecondicaoIfMatch"/> correspondente ao header, ou <b>400</b>
    /// (<c>Precondicao.Malformada</c>) quando ele viola a gramática da RFC 9110 §13.1.1.
    /// </summary>
    /// <param name="valor">
    /// O header cru, como o model binder o entrega — <see langword="null"/> quando ausente.
    /// Múltiplos <c>If-Match</c> na mesma requisição chegam aqui já concatenados por vírgula,
    /// que é a mesma forma da lista (<c>1#entity-tag</c>): os dois casos caem no mesmo split.
    /// </param>
    public static Result<PrecondicaoIfMatch> Analisar(string? valor)
    {
        // Header ausente é ESTADO VÁLIDO, não erro: os seis Definir* servem também um
        // processo em Rascunho, que não tem sessão editorial e portanto não tem ETag a
        // fornecer. Quem transforma a ausência em 428 é o handler — e só quando há sessão.
        if (valor is null)
        {
            return Result<PrecondicaoIfMatch>.Success(PrecondicaoIfMatch.Ausente);
        }

        List<string> itens = [.. valor
            .Split(',')
            .Select(static v => v.Trim())
            .Where(static v => v.Length > 0)];

        // O header VEIO, mas não carrega entity-tag nenhuma — `If-Match:` vazio, ou só
        // vírgulas. Não é o mesmo que ausência: a gramática exige `1#entity-tag`, ao menos
        // uma. Tratá-lo como ausente faria o cliente que mandou lixo receber 428 ("informe o
        // If-Match") tendo informado — e ele o releria, o remandaria igual, e giraria.
        if (itens.Count == 0)
        {
            return Result<PrecondicaoIfMatch>.Failure(new DomainError(
                "Precondicao.Malformada",
                "If-Match veio sem entity-tag alguma — a gramática exige ao menos uma (RFC 9110 §13.1.1)."));
        }

        bool temCuringa = itens.Any(static i => i == "*");
        if (temCuringa)
        {
            // "*" é "qualquer representação existente" — misturá-lo com tags específicas é
            // contradição, não preferência: a RFC não define qual venceria. É sintaxe
            // inválida, e sai 400.
            if (itens.Count > 1)
            {
                return Result<PrecondicaoIfMatch>.Failure(new DomainError(
                    "Precondicao.Malformada",
                    "If-Match aceita '*' sozinho ou uma lista de entity-tags — nunca os dois juntos."));
            }

            return Result<PrecondicaoIfMatch>.Success(PrecondicaoIfMatch.Curinga);
        }

        if (!EntityTagHeaderValue.TryParseList(itens, out IList<EntityTagHeaderValue>? tags) || tags is null)
        {
            return Result<PrecondicaoIfMatch>.Failure(new DomainError(
                "Precondicao.Malformada",
                "If-Match malformado: cada entity-tag deve vir entre aspas duplas (RFC 9110 §8.8.3)."));
        }

        // As weak tags são DESCARTADAS, não recusadas — e a diferença importa. A gramática
        // do If-Match as aceita; o que a RFC 9110 §13.1.1 exige é que a comparação seja
        // FORTE, e uma weak tag nunca casa nela. Recusá-las com 400 diria ao cliente que o
        // header está malformado quando ele está apenas condenado a não casar. Filtrando-as
        // aqui, um If-Match só com weak tags chega ao domínio como uma lista vazia de tags
        // fortes, não casa, e sai 412 — que é a resposta correta.
        List<string> fortes = [.. tags
            .Where(static t => !t.IsWeak)
            .Select(static t => t.ToString())];

        return Result<PrecondicaoIfMatch>.Success(PrecondicaoIfMatch.DeTags(fortes));
    }
}
