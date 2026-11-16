namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Regra de um campo textual: sem espaços nas pontas, em NFC, sem os caracteres que a <see cref="FormaDoTexto"/>
/// recusa e dentro do tamanho. A violação entra no lote de erros do chamador, e a mensagem nunca ecoa o valor
/// recusado.
/// </summary>
internal sealed record CampoTextual(string Nome, int Maximo, string ErroTamanho, string ErroCaractere, FormaDoTexto Forma)
{
    /// <summary>Tamanho mínimo, depois de normalizado.</summary>
    public int Minimo { get; init; } = 1;

    /// <summary>O nome do campo é feminino: a mensagem de ausência diz "obrigatória".</summary>
    public bool Feminino { get; init; }

    /// <summary>O texto normalizado, ou <see langword="null"/> quando ausente ou recusado.</summary>
    public string? Obrigatorio(string? valor, string caminho, string erroObrigatorio, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        if (string.IsNullOrWhiteSpace(valor))
        {
            erros.Add(new(caminho, new DomainError(erroObrigatorio, $"{Nome} é {(Feminino ? "obrigatória" : "obrigatório")}.")));
            return null;
        }

        return Normalizar(valor, caminho, erros);
    }

    /// <summary>O texto normalizado, ou <see langword="null"/> quando em branco ou recusado.</summary>
    public string? Opcional(string? valor, string caminho, List<FieldError> erros) =>
        string.IsNullOrWhiteSpace(valor) ? null : Normalizar(valor, caminho, erros);

    private string? Normalizar(string valor, string caminho, List<FieldError> erros)
    {
        SituacaoDoTexto situacao = Tentar(valor.Trim(), out string normalizado);
        if (situacao == SituacaoDoTexto.Valido && normalizado.Length < Minimo)
        {
            situacao = SituacaoDoTexto.TamanhoExcedido;
        }

        DomainError? recusa = Recusa(situacao);
        if (recusa is not null)
        {
            erros.Add(new(caminho, recusa));
        }

        return recusa is null ? normalizado : null;
    }

    private SituacaoDoTexto Tentar(string texto, out string normalizado) => Forma switch
    {
        FormaDoTexto.UmaLinha => TextoNormalizavel.TentarNfc(texto, Maximo, out normalizado),
        FormaDoTexto.VariasLinhas => TextoNormalizavel.TentarNfcDeTextoDeVariasLinhas(texto, Maximo, out normalizado),
        _ => TextoNormalizavel.TentarNfcDeTextoLivre(texto, Maximo, out normalizado),
    };

    private DomainError? Recusa(SituacaoDoTexto situacao) => situacao switch
    {
        SituacaoDoTexto.Valido => null,
        SituacaoDoTexto.TamanhoExcedido => new(ErroTamanho, MensagemDoTamanho()),
        _ => new(ErroCaractere, MensagemDoCaractere()),
    };

    private string MensagemDoTamanho() => Minimo > 1
        ? $"{Nome} deve ter entre {Minimo} e {Maximo} caracteres."
        : $"{Nome} deve ter no máximo {Maximo} caracteres.";

    private string MensagemDoCaractere() => Forma switch
    {
        FormaDoTexto.UmaLinha => $"{Nome} não pode conter o caractere nulo nem caractere que não seja texto.",
        FormaDoTexto.VariasLinhas => $"{Nome} não pode conter o caractere nulo, de controle ou de formatação; só a quebra de linha e a tabulação são aceitas.",
        _ => $"{Nome} não pode conter o caractere nulo (U+0000).",
    };
}
