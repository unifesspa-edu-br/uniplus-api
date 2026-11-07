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
        [UfDeResidencia] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
        ["MUNICIPIO_RESIDENCIA"] = new(TipoDoValorDeResposta.Municipio, Multivalorado: false, Identificador: false),
    };

    /// <summary>As definições das respostas do formulário e as dos derivados do sistema, para a avaliação saber quais valores são município.</summary>
    public static IReadOnlyDictionary<string, DefinicaoDaResposta> ComOsDerivados(IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes)
    {
        ArgumentNullException.ThrowIfNull(definicoes);

        return new Dictionary<string, DefinicaoDaResposta>(definicoes, StringComparer.Ordinal)
            .Concat(DefinicoesDasRespostas.Where(d => !definicoes.ContainsKey(d.Key)))
            .ToDictionary(static d => d.Key, static d => d.Value, StringComparer.Ordinal);
    }

    /// <summary>O fato do endereço de que saem os derivados de residência.</summary>
    public const string EnderecoResidencial = "ENDERECO_RESIDENCIAL";

    /// <summary>O derivado com a UF do endereço residencial, gravado pela sigla.</summary>
    public const string UfDeResidencia = "UF_RESIDENCIA";

    private static readonly IReadOnlySet<string> SemDerivadosIndeterminados = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Os derivados de residência: saem do endereço residencial e dependem do Geo.</summary>
    public static readonly IReadOnlySet<string> FatosDaResidencia =
        new HashSet<string>([UfDeResidencia, "MUNICIPIO_RESIDENCIA"], StringComparer.Ordinal);

    /// <summary>
    /// Calcula os derivados do envio. A residência sai do que o Geo resolveu pelo CEP do endereço residencial na conferência do
    /// endereço, a mesma consulta, e nunca do que o candidato declarou. O endereço que a conferência não confirmou — recusado, ou
    /// com o Geo fora do ar — não recusa aqui: os derivados de residência ficam indeterminados, e a recusa do endereço, quando há,
    /// é da conferência dele. A data de nascimento que não é uma data não gera faixa etária: a recusa é da forma do corpo.
    /// </summary>
    /// <param name="respostas">As respostas do estado final do envio, inclusive o endereço residencial já recusado.</param>
    /// <param name="enderecos">O que a conferência dos endereços apurou.</param>
    public static DerivadosCalculados Calcular(
        IReadOnlyDictionary<string, JsonElement> respostas, DateOnly? dataReferenciaFatos, ConferenciaDosEnderecos enderecos)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(enderecos);

        Dictionary<string, JsonElement> valores = new(StringComparer.Ordinal);
        Dictionary<string, string> nomes = new(StringComparer.Ordinal);

        if (FaixaEtaria(respostas, dataReferenciaFatos) is { } idade)
        {
            valores["FAIXA_ETARIA"] = JsonSerializer.SerializeToElement(idade);
        }

        if (!respostas.ContainsKey(EnderecoResidencial))
        {
            return new DerivadosCalculados(valores, nomes, SemDerivadosIndeterminados, GeoIndisponivel: null);
        }

        if (enderecos.Resolvidos.TryGetValue(EnderecoResidencial, out CepResolvido? resolvido))
        {
            valores["UF_RESIDENCIA"] = JsonSerializer.SerializeToElement(resolvido.Uf);
            valores["MUNICIPIO_RESIDENCIA"] = JsonSerializer.SerializeToElement(resolvido.CodigoIbge);
            nomes["MUNICIPIO_RESIDENCIA"] = resolvido.Cidade;
            return new DerivadosCalculados(valores, nomes, SemDerivadosIndeterminados, GeoIndisponivel: null);
        }

        return new DerivadosCalculados(valores, nomes, FatosDaResidencia, enderecos.GeoIndisponivel);
    }

    /// <summary>
    /// Os derivados pedidos, como fatos conhecidos para a avaliação fora do envio: só os de
    /// <paramref name="pedidos"/>, para não consultar o Geo sem necessidade; o que não se consegue
    /// apurar — resposta ausente, endereço incompleto, Geo que recusa ou não responde — fica
    /// indeterminado, sem recusar nada. Só o envio recusa.
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
            apurados["FAIXA_ETARIA"] = FaixaEtaria(respostas, dataReferenciaFatos) is { } idade
                ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(idade))
                : FatoResolvido.Indeterminado();
        }

        if (pedidos.Overlaps(FatosDaResidencia))
        {
            ResidenciaApurada residencia = await ResidenciaAsync(respostas, conferencia, cancellationToken).ConfigureAwait(false);
            bool conferida = residencia.Conferida;
            apurados["UF_RESIDENCIA"] = conferida
                ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(residencia.Uf))
                : FatoResolvido.Indeterminado();
            apurados["MUNICIPIO_RESIDENCIA"] = conferida
                ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(residencia.Codigo))
                : FatoResolvido.Indeterminado();
        }

        return apurados;
    }

    /// <summary>
    /// Os derivados de residência pedidos, como fatos conhecidos, a partir dos que o envio gravou: a inscrição enviada não
    /// reconfere o endereço no Geo, porque o que ela diz da residência não muda com o Geo. O que o envio não gravou fica indeterminado.
    /// </summary>
    public static IReadOnlyDictionary<string, FatoResolvido> ResidenciaGravada(
        IReadOnlyDictionary<string, JsonElement> derivadosGravados, IReadOnlySet<string> pedidos)
    {
        ArgumentNullException.ThrowIfNull(derivadosGravados);
        ArgumentNullException.ThrowIfNull(pedidos);

        return FatosDaResidencia.Where(pedidos.Contains).ToDictionary(
            static f => f,
            f => derivadosGravados.TryGetValue(f, out JsonElement valor) ? FatoResolvido.Resolvido(valor) : FatoResolvido.Indeterminado(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// A idade na data de referência; nula quando não há o que apurar — sem data de referência, nenhuma regra do processo cita
    /// a faixa etária, sem data de nascimento não há idade, e a data inválida não gera idade.
    /// </summary>
    private static int? FaixaEtaria(IReadOnlyDictionary<string, JsonElement> respostas, DateOnly? dataReferenciaFatos) =>
        dataReferenciaFatos is { } referencia
        && respostas.TryGetValue("DATA_NASCIMENTO", out JsonElement dataNascimentoElemento)
        && DiaDoCalendario.Ler(dataNascimentoElemento) is { } nascimento
            ? IdadeEmAnos(nascimento, referencia)
            : null;

    /// <summary>
    /// A UF e o município do endereço residencial, só depois de conferidos no Geo, fora do envio. O endereço sem a cidade que a
    /// conferência exige, o que o Geo recusa e o que ele não confere por estar fora do ar ficam indeterminados.
    /// </summary>
    private static async Task<ResidenciaApurada> ResidenciaAsync(
        IReadOnlyDictionary<string, JsonElement> respostas, IConferenciaEnderecoGeo conferencia, CancellationToken cancellationToken)
    {
        if (!respostas.TryGetValue(EnderecoResidencial, out JsonElement enderecoElemento))
        {
            return ResidenciaApurada.Indeterminada;
        }

        if (!EnderecoDeclarado.TentarLer(enderecoElemento, out EnderecoDeclarado endereco)
            || endereco is not { CidadeCodigoIbge: { Length: > 0 } codigoIbge, CidadeUf: { Length: > 0 } uf })
        {
            return ResidenciaApurada.Indeterminada;
        }

        // O ramo por município serve à apuração de quem ainda não tem CEP: confere pela cidade e nunca recusa por falta de CEP.
        Result<string> conferido = endereco.CepSomenteDigitos is { } cep
            ? await conferencia.ConferirCepAsync(cep, codigoIbge, uf, cancellationToken).ConfigureAwait(false)
            : await conferencia.ConferirMunicipioAsync(codigoIbge, uf, cancellationToken).ConfigureAwait(false);

        // O pressuposto de elegibilidade é o que o Geo confirmou, nunca o que o candidato mandou.
        return conferido.IsSuccess ? new ResidenciaApurada(uf, codigoIbge) : ResidenciaApurada.Indeterminada;
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

/// <summary>
/// Os derivados do envio: os valores, que são códigos; o nome que o Geo deu a cada derivado de município; os derivados de
/// residência que ficaram indeterminados; e o erro do Geo quando foi ele fora do ar que os deixou assim.
/// </summary>
public sealed record DerivadosCalculados(
    IReadOnlyDictionary<string, JsonElement> Valores,
    IReadOnlyDictionary<string, string> Nomes,
    IReadOnlySet<string> Indeterminados,
    DomainError? GeoIndisponivel);

internal sealed record ResidenciaApurada(string? Uf, string? Codigo)
{
    public static ResidenciaApurada Indeterminada { get; } = new(null, null);

    public bool Conferida => Uf is not null;
}
