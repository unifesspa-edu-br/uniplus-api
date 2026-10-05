namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using ValueObjects;

/// <summary>
/// A chave do documento no acervo público (ADR-0132). É parte do endereço divulgado no edital: muda
/// só por decisão, porque um endereço publicado pode ter sido citado e não se desfaz.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identidade da publicação, e não só o hash.</b> O processo e o ato preservam a procedência — o
/// mesmo arquivo publicado por dois atos vira dois objetos, cada um com os metadados do seu —, e a
/// retificação que reaproveita o arquivo do ato anterior não colide com ele, porque é outro ato.
/// </para>
/// <para>
/// <b>O modelo também entra.</b> O nome de apresentação está gravado no objeto, e o reenvio do mesmo
/// arquivo com outro nome — o caminho natural para corrigir um nome ruim — gera outro modelo. Sem ele
/// na chave, o objeto já gravado prenderia o nome antigo.
/// </para>
/// <para>
/// Todos os segmentos vêm de dado congelado — o ato da versão, o resto do envelope —, e nenhum
/// identifica pessoa (ADR-0019).
/// </para>
/// </remarks>
public static class ChaveNoAcervoPublico
{
    /// <summary>A chave do modelo de documento que a exigência oferece, no ato que o publicou.</summary>
    public static string DoModelo(Guid processoSeletivoId, Guid atoId, ModeloDaExigencia modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        return $"selecao/processos-seletivos/{processoSeletivoId:D}/atos/{atoId:D}/modelos-de-documento/{modelo.ModeloId:D}/{modelo.HashSha256}.{ModeloDeDocumento.ExtensaoDe(modelo.Formato)}";
    }
}
