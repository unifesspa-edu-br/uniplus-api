namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json.Nodes;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// Lê o texto do documento de configuração enviado: confere os limites e a estrutura, normaliza o documento, confere a
/// quantidade de valores do documento declarado e o avalia contra o esquema. A forma recusa o documento inteiro; a regra de
/// negócio da configuração não é daqui. Normalização e avaliação são independentes e acumulam todas as causas, que saem na
/// ordem do documento, uma por ponteiro.
/// </summary>
public sealed class LeituraDoDocumentoDaConfiguracao(IEsquemaDoDocumentoDaConfiguracao esquema)
{
    public Result<DocumentoLido> Ler(string texto)
    {
        Result<JsonObject> estrutura = ConferenciaDaEstruturaDoDocumento.Conferir(texto);

        return estrutura.IsFailure
            ? Result<DocumentoLido>.ValidationFailure(estrutura.Errors)
            : LerODocumentoDeclarado(estrutura.Value!);
    }

    private Result<DocumentoLido> LerODocumentoDeclarado(JsonObject documento)
    {
        IReadOnlyList<FieldError> chavesRepetidas = new NormalizacaoDoDocumento(esquema.Forma).Normalizar(documento);
        IReadOnlyList<FieldError> acimaDoLimite = ConferenciaDaEstruturaDoDocumento.ConferirQuantidadeDeNos(documento);
        if (acimaDoLimite.Count > 0)
        {
            return Result<DocumentoLido>.ValidationFailure(acimaDoLimite);
        }

        IReadOnlyList<FieldError> causas = OrdemDasCausas.Ordenar([.. esquema.Avaliar(documento), .. chavesRepetidas], documento);

        return causas.Count == 0
            ? Result<DocumentoLido>.Success(IdentificacaoDoDocumento.Ler(documento))
            : Result<DocumentoLido>.ValidationFailure(causas);
    }
}
