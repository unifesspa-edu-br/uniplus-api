namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Códigos de erro do codec do envelope (ADR-0024 — códigos estáveis). São a
/// diferença entre uma recusa <b>nomeada</b> e um envelope que se degrada em silêncio.
/// </summary>
public static class ErrosCodecEnvelope
{
    /// <summary>Versão fora do registro — o sistema nem sabe que ela existe.</summary>
    public const string VersaoDesconhecida = "EnvelopeCodec.VersaoDesconhecida";

    /// <summary>Versão conhecida, mas sem codec completo — a <c>1.0</c>. Não reidrata parcialmente.</summary>
    public const string VersaoNaoReidratavel = "EnvelopeCodec.VersaoNaoReidratavel";

    /// <summary>O algoritmo de hash persistido não é o que o codec daquela versão declara.</summary>
    public const string AlgoritmoNaoSuportado = "EnvelopeCodec.AlgoritmoNaoSuportado";

    /// <summary>
    /// Os bytes não produzem o <c>HashConfiguracao</c> persistido — ou não são canônicos.
    /// Não se reidrata evidência que não prova o que diz provar.
    /// </summary>
    public const string IntegridadeViolada = "EnvelopeCodec.IntegridadeViolada";

    /// <summary>JSON inválido, chave ausente, chave desconhecida, chave duplicada ou valor fora da forma.</summary>
    public const string EnvelopeMalformado = "EnvelopeCodec.EnvelopeMalformado";

    /// <summary>O envelope contradiz a própria linha que o guarda (hash do ato, cadeia de retificação).</summary>
    public const string EnvelopeIncoerenteComAVersao = "EnvelopeCodec.EnvelopeIncoerenteComAVersao";

    /// <summary>
    /// <c>distribuicao</c>, <c>modalidades</c> e <c>ofertas</c> derivam da mesma coleção
    /// e não fecham (ADR-0110 D8). Recombiná-los em silêncio reconstruiria um agregado
    /// que nunca existiu.
    /// </summary>
    public const string BlocosDerivadosIncoerentes = "EnvelopeCodec.BlocosDerivadosIncoerentes";

    /// <summary>
    /// Código de regra fora do rol. As factories do domínio <b>não fecham o vocabulário</b>
    /// — um código desconhecido cairia no ramo default (institucional, cálculo local) e
    /// reconstruiria uma configuração diferente da congelada.
    /// </summary>
    public const string RegraDesconhecida = "EnvelopeCodec.RegraDesconhecida";
}
