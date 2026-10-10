namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Globalization;
using System.Text.Json;

using Abstractions;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Calcula no servidor os fatos que <c>Unifesspa.UniPlus.Regras.Services.DerivadosDoSistema</c>
/// declara (ADR-0136, UNI-REQ-0075): <c>FAIXA_ETARIA</c>, da data de nascimento e da data de
/// referência CONGELADA na publicação; <c>UF_RESIDENCIA</c>/<c>MUNICIPIO_RESIDENCIA</c>, do
/// endereço residencial, só depois de conferido no Geo (issue #1865) — nenhum pressuposto de
/// elegibilidade enviado pelo candidato é aceito, mesmo que ele mande os três fatos na resposta.
/// </summary>
public static class DerivadosDoSistemaInscricao
{
    public static readonly IReadOnlyDictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas = new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
    {
        ["FAIXA_ETARIA"] = new(TipoDoValorDeResposta.Numero, Multivalorado: false, Identificador: false),
        ["UF_RESIDENCIA"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
        ["MUNICIPIO_RESIDENCIA"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
    };

    public static async Task<Result<IReadOnlyDictionary<string, JsonElement>>> CalcularAsync(
        IReadOnlyDictionary<string, JsonElement> respostas,
        DateOnly? dataReferenciaFatos,
        IConferenciaEnderecoGeo conferencia,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(conferencia);

        Dictionary<string, JsonElement> derivados = new(StringComparer.Ordinal);

        if (respostas.TryGetValue("DATA_NASCIMENTO", out JsonElement dataNascimentoElemento))
        {
            if (dataNascimentoElemento.ValueKind != JsonValueKind.String
                || !DateOnly.TryParse(dataNascimentoElemento.GetString(), CultureInfo.InvariantCulture, out DateOnly nascimento))
            {
                return Result<IReadOnlyDictionary<string, JsonElement>>.Failure(new DomainError(
                    "Inscricao.DataNascimentoInvalida", "A data de nascimento não é uma data válida."));
            }

            if (dataReferenciaFatos is not { } referencia)
            {
                return Result<IReadOnlyDictionary<string, JsonElement>>.Failure(new DomainError(
                    "Inscricao.DataReferenciaFatosAusente",
                    "O processo não declara uma data de referência para apurar FAIXA_ETARIA, mas a resposta tem DATA_NASCIMENTO."));
            }

            derivados["FAIXA_ETARIA"] = JsonSerializer.SerializeToElement(IdadeEmAnos(nascimento, referencia));
        }

        if (respostas.TryGetValue("ENDERECO_RESIDENCIAL", out JsonElement enderecoElemento))
        {
            if (!EnderecoDeclarado.TentarLer(enderecoElemento, out EnderecoDeclarado endereco)
                || endereco is not { CidadeCodigoIbge: { Length: > 0 } codigoIbge, CidadeUf: { Length: > 0 } uf })
            {
                return Result<IReadOnlyDictionary<string, JsonElement>>.Failure(new DomainError(
                    "Inscricao.EnderecoResidencialInvalido", "O endereço residencial não tem a cidade que a conferência no Geo exige."));
            }

            // O componente de endereço admite o preenchimento sem CEP (cidade escolhida
            // manualmente, ex.: município sem cobertura no DNE) — nesse caso confere pela cidade,
            // nunca recusa por falta de CEP.
            Result conferido = endereco.Cep is { Length: > 0 }
                ? await conferencia.ConferirCepAsync(endereco.Cep, codigoIbge, uf, cancellationToken).ConfigureAwait(false)
                : await conferencia.ConferirMunicipioAsync(codigoIbge, uf, cancellationToken).ConfigureAwait(false);
            if (conferido.IsFailure)
            {
                return Result<IReadOnlyDictionary<string, JsonElement>>.Failure(conferido.Error!);
            }

            // O pressuposto de elegibilidade é o que o Geo confirmou, nunca o que o candidato
            // mandou — o endereço já foi conferido linha acima contra o mesmo cep/cidade/UF.
            derivados["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement(uf);
            derivados["MUNICIPIO_RESIDENCIA"] = JsonSerializer.SerializeToElement(codigoIbge);
        }

        return Result<IReadOnlyDictionary<string, JsonElement>>.Success(derivados);
    }

    /// <summary>Idade em anos completos na data de referência — nunca a de hoje.</summary>
    private static int IdadeEmAnos(DateOnly nascimento, DateOnly referencia)
    {
        int idade = referencia.Year - nascimento.Year;
        if (nascimento.AddYears(idade) > referencia)
        {
            idade--;
        }

        return idade;
    }
}
