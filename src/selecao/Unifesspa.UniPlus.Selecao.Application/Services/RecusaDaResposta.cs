namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Globalization;

using Domain.Errors;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// A recusa da resposta que viola uma restrição do formulário, a mesma no rascunho e no envio: a da
/// faixa de data diz ao candidato os limites no dia do envio, em dd/mm/aaaa; as demais, que a resposta
/// não atende a uma restrição.
/// </summary>
public static class RecusaDaResposta
{
    public static FieldError De(string campo, AvaliacaoItem item, DateOnly diaDoEnvio)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.RestricoesVioladas.OfType<FaixaDeData>().FirstOrDefault() is { } faixa
            ? new(campo, new DomainError(InscricaoErrorCodes.DataForaDaFaixa, MensagemDaFaixa(faixa.Limites(diaDoEnvio))))
            : new(campo, new DomainError(
                InscricaoErrorCodes.RespostaInvalida, $"A resposta de '{item.FatoCodigo}' não atende a uma restrição do formulário."));
    }

    /// <summary>A orientação pelos limites que se conhecem: a resposta só viola o limite que tem data.</summary>
    private static string MensagemDaFaixa((DateOnly? Minima, DateOnly? Maxima) limites) => limites switch
    {
        ({ } minima, { } maxima) => $"Informe uma data entre {Exibir(minima)} e {Exibir(maxima)}.",
        ({ } minima, null) => $"Informe uma data a partir de {Exibir(minima)}.",
        (null, { } maxima) => $"Informe uma data até {Exibir(maxima)}.",
        // A resposta só viola um limite que tem data; sem nenhum, a avaliação não a daria como violada.
        _ => throw new InvalidOperationException("A faixa de data foi dada como violada sem limite com data no dia do envio."),
    };

    private static string Exibir(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
