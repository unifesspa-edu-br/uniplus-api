namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Regra de um campo textual do tipo de conceito: sem espaços nas pontas, em NFC, sem caractere nulo e dentro
/// do tamanho. A violação entra no lote de erros do chamador, e a mensagem nunca ecoa o valor recusado.
/// </summary>
internal sealed record CampoTextual(string Nome, int Maximo, string ErroTamanho, string ErroCaractere)
{
    /// <summary>O texto normalizado, ou <see langword="null"/> quando ausente ou recusado.</summary>
    public string? Obrigatorio(string? valor, string caminho, string erroObrigatorio, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        if (string.IsNullOrWhiteSpace(valor))
        {
            erros.Add(new(caminho, new DomainError(erroObrigatorio, $"{Nome} é obrigatório.")));
            return null;
        }

        return Normalizar(valor, caminho, erros);
    }

    /// <summary>O texto normalizado, ou <see langword="null"/> quando em branco ou recusado.</summary>
    public string? Opcional(string? valor, string caminho, List<FieldError> erros) =>
        string.IsNullOrWhiteSpace(valor) ? null : Normalizar(valor, caminho, erros);

    private string? Normalizar(string valor, string caminho, List<FieldError> erros)
    {
        SituacaoDoTexto situacao = TextoNormalizavel.TentarNfcDeTextoLivre(valor.Trim(), Maximo, out string normalizado);
        DomainError? recusa = situacao switch
        {
            SituacaoDoTexto.Valido => null,
            SituacaoDoTexto.TamanhoExcedido => new(ErroTamanho, $"{Nome} deve ter no máximo {Maximo} caracteres."),
            _ => new(ErroCaractere, $"{Nome} não pode conter caractere inválido, como o nulo (U+0000)."),
        };
        if (recusa is not null)
        {
            erros.Add(new(caminho, recusa));
        }

        return recusa is null ? normalizado : null;
    }
}
