namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

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

        Result<int?> faixaEtaria = FaixaEtaria(respostas, dataReferenciaFatos);
        if (faixaEtaria.IsFailure)
        {
            return Result<IReadOnlyDictionary<string, JsonElement>>.Failure(faixaEtaria.Error!);
        }

        if (faixaEtaria.Value is { } idade)
        {
            derivados["FAIXA_ETARIA"] = JsonSerializer.SerializeToElement(idade);
        }

        Result<(string Uf, string Municipio)?> residencia = await ResidenciaAsync(respostas, conferencia, cancellationToken).ConfigureAwait(false);
        if (residencia.IsFailure)
        {
            return Result<IReadOnlyDictionary<string, JsonElement>>.Failure(residencia.Error!);
        }

        if (residencia.Value is { } conferida)
        {
            derivados["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement(conferida.Uf);
            derivados["MUNICIPIO_RESIDENCIA"] = JsonSerializer.SerializeToElement(conferida.Municipio);
        }

        return Result<IReadOnlyDictionary<string, JsonElement>>.Success(derivados);
    }

    /// <summary>
    /// Os derivados pedidos, como fatos conhecidos para a avaliação fora do envio: só os de
    /// <paramref name="pedidos"/>, para não consultar o Geo sem necessidade; o que não se consegue
    /// apurar — resposta ausente, endereço incompleto, Geo que recusa ou não responde — fica
    /// indeterminado, sem recusar nada. Só o envio recusa (<see cref="CalcularAsync"/>).
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, FatoResolvido>> ApurarAsync(
        IReadOnlyDictionary<string, JsonElement> respostas,
        DateOnly? dataReferenciaFatos,
        IConferenciaEnderecoGeo conferencia,
        IReadOnlySet<string> pedidos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(pedidos);

        Dictionary<string, FatoResolvido> apurados = new(StringComparer.Ordinal);
        if (pedidos.Contains("FAIXA_ETARIA"))
        {
            apurados["FAIXA_ETARIA"] = FaixaEtaria(respostas, dataReferenciaFatos) is { IsSuccess: true, Value: { } idade }
                ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(idade))
                : FatoResolvido.Indeterminado();
        }

        if (pedidos.Contains("UF_RESIDENCIA") || pedidos.Contains("MUNICIPIO_RESIDENCIA"))
        {
            Result<(string Uf, string Municipio)?> residencia = await ResidenciaAsync(respostas, conferencia, cancellationToken).ConfigureAwait(false);
            bool conferida = residencia is { IsSuccess: true, Value: not null };
            apurados["UF_RESIDENCIA"] = conferida
                ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(residencia.Value!.Value.Uf))
                : FatoResolvido.Indeterminado();
            apurados["MUNICIPIO_RESIDENCIA"] = conferida
                ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(residencia.Value!.Value.Municipio))
                : FatoResolvido.Indeterminado();
        }

        return apurados;
    }

    /// <summary>
    /// A idade na data de referência; nula quando não há o que apurar — sem data de referência,
    /// nenhuma regra do processo cita a faixa etária, e sem data de nascimento não há idade.
    /// </summary>
    private static Result<int?> FaixaEtaria(IReadOnlyDictionary<string, JsonElement> respostas, DateOnly? dataReferenciaFatos)
    {
        if (dataReferenciaFatos is not { } referencia
            || !respostas.TryGetValue("DATA_NASCIMENTO", out JsonElement dataNascimentoElemento))
        {
            return Result<int?>.Success(null);
        }

        if (DiaDoCalendario.Ler(dataNascimentoElemento) is not { } nascimento)
        {
            return Result<int?>.Failure(new DomainError(
                "Inscricao.DataNascimentoInvalida", "A data de nascimento não é uma data válida."));
        }

        return Result<int?>.Success(IdadeEmAnos(nascimento, referencia));
    }

    /// <summary>A UF e o município do endereço residencial, só depois de conferidos no Geo; nulo sem endereço.</summary>
    private static async Task<Result<(string Uf, string Municipio)?>> ResidenciaAsync(
        IReadOnlyDictionary<string, JsonElement> respostas, IConferenciaEnderecoGeo conferencia, CancellationToken cancellationToken)
    {
        if (!respostas.TryGetValue("ENDERECO_RESIDENCIAL", out JsonElement enderecoElemento))
        {
            return Result<(string Uf, string Municipio)?>.Success(null);
        }

        if (!EnderecoDeclarado.TentarLer(enderecoElemento, out EnderecoDeclarado endereco)
            || endereco is not { CidadeCodigoIbge: { Length: > 0 } codigoIbge, CidadeUf: { Length: > 0 } uf })
        {
            return Result<(string Uf, string Municipio)?>.Failure(new DomainError(
                "Inscricao.EnderecoResidencialInvalido", "O endereço residencial não tem a cidade que a conferência no Geo exige."));
        }

        // Salvar e Submeter já recusam o endereço sem CEP (ConferenciaDoEnderecoDaInscricao); o ramo
        // por município fica para a consulta fora do envio (ApurarAsync) e para o endereço gravado
        // antes dessa regra, que ainda pode estar sem CEP. Nesses casos confere pela cidade e nunca
        // recusa por falta de CEP, porque quem recusa é o envio.
        Result conferido = endereco.Cep is { Length: > 0 }
            ? await conferencia.ConferirCepAsync(endereco.Cep, codigoIbge, uf, cancellationToken).ConfigureAwait(false)
            : await conferencia.ConferirMunicipioAsync(codigoIbge, uf, cancellationToken).ConfigureAwait(false);

        // O pressuposto de elegibilidade é o que o Geo confirmou, nunca o que o candidato mandou.
        return conferido.IsFailure
            ? Result<(string Uf, string Municipio)?>.Failure(conferido.Error!)
            : Result<(string Uf, string Municipio)?>.Success((uf, codigoIbge));
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
