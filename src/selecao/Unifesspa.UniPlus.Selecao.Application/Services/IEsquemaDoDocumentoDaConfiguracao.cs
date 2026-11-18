namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json.Nodes;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// O esquema do documento de configuração do processo, que a Infrastructure guarda embutido e nunca busca fora. Entrega à
/// Application a forma que a normalização precisa e avalia o documento normalizado contra ele.
/// </summary>
public interface IEsquemaDoDocumentoDaConfiguracao
{
    /// <summary>A raiz do que o esquema declara: campos só de leitura e partes com chave local, ponto a ponto.</summary>
    FormaDoNo Forma { get; }

    /// <summary>
    /// As causas pelas quais o documento viola o esquema, uma por ponteiro, sem repetir texto do documento nas mensagens.
    /// Vazio quando o documento é válido; um documento incompleto é válido, porque a regra de negócio não é do esquema.
    /// </summary>
    IReadOnlyList<FieldError> Avaliar(JsonObject documento);
}
