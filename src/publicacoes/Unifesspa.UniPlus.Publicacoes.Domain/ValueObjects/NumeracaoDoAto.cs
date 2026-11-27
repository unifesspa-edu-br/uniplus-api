namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// A numeração que o órgão deu ao ato: a série (órgão, ano) atravessa certames, e o número é
/// declarado, opcional e sem unicidade imposta (ADR-0105). É a chave do aviso de número duplicado.
/// </summary>
public readonly record struct NumeracaoDoAto(string Orgao, string Serie, int Ano, string? Numero)
{
    private const int OrgaoMaximo = 200;
    private const int SerieMaximo = 100;
    private const int NumeroMaximo = 60;

    /// <summary>
    /// Todas as recusas da numeração, na ordem do corpo (órgão, série, ano, número), ou a numeração com
    /// os textos sem espaços nas pontas e o número vazio como ausente.
    /// </summary>
    internal ParteConferida<NumeracaoDoAto> Conferir()
    {
        ParteConferida<string> orgao = TextoDoAto.Obrigatorio(Orgao, OrgaoMaximo, "orgao", "O órgão");
        ParteConferida<string> serie = TextoDoAto.Obrigatorio(Serie, SerieMaximo, "serie", "A série");
        ParteConferida<int> ano = ConferirAno();
        ParteConferida<string> numero = TextoDoAto.Opcional(Numero, NumeroMaximo, "numero", "O número");

        return ParteConferida<NumeracaoDoAto>.Compor(
            [.. orgao.Recusas, .. serie.Recusas, .. ano.Recusas, .. numero.Recusas],
            () => new NumeracaoDoAto(orgao.Valor!, serie.Valor!, ano.Valor, numero.Valor));
    }

    /// <summary>A numeração conferida, ou a exceção da primeira recusa de <see cref="Conferir"/>.</summary>
    internal NumeracaoDoAto Conferida() => Conferir().ValorOuLancar();

    private ParteConferida<int> ConferirAno() =>
        ParteConferida<int>.Decidida(
            Ano,
            Ano > 0
                ? null
                : new FieldError("ano", new DomainError(AtoNormativoErrorCodes.AnoInvalido, "O ano da numeração deve ser maior que zero.")));

    /// <summary>
    /// Como a recusa nomeia o ato que a causa: pela numeração oficial (série, número e ano), que o
    /// operador reconhece, e nunca pelo identificador.
    /// </summary>
    public string Descrever() =>
        Numero is null
            ? string.Create(CultureInfo.InvariantCulture, $"{Serie} sem número de {Ano}")
            : string.Create(CultureInfo.InvariantCulture, $"{Serie} nº {Numero}/{Ano}");
}
