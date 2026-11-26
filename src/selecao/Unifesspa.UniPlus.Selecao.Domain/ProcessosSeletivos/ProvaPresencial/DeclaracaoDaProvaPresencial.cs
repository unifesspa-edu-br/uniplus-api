namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o processo declara da prova presencial (UNI-REQ-0407): se a tem e, só então, os municípios em
/// que a aplica, qualquer município do Geo, cada um pelo código do IBGE com a UF coerente com o prefixo.
/// A declaração é do próprio processo, nunca deduzida do tipo de processo nem de etapa. A declaração
/// que não existe ainda é a do processo por declarar, e quem a guarda a representa por ausência.
/// </summary>
public sealed record DeclaracaoDaProvaPresencial
{
    /// <summary>O campo do bloco da declaração, no corpo do pedido e no documento.</summary>
    public const string Campo = "provaPresencial";

    private const string CampoDosMunicipios = Campo + ".municipios";

    private readonly List<MunicipioDeProva> _municipios = [];

    private DeclaracaoDaProvaPresencial(bool temProvaPresencial)
    {
        TemProvaPresencial = temProvaPresencial;
    }

    private DeclaracaoDaProvaPresencial(bool temProvaPresencial, IEnumerable<MunicipioDeProva> municipios)
        : this(temProvaPresencial) => _municipios.AddRange(municipios);

    public bool TemProvaPresencial { get; }

    /// <summary>Os municípios de prova; vazia sem prova presencial, e ainda vazia no rascunho que não os declarou.</summary>
    public IReadOnlyList<MunicipioDeProva> Municipios => [.. _municipios.OrderBy(static m => m.Posicao)];

    /// <summary>As declarações são iguais quando têm a mesma prova presencial e os mesmos municípios, na mesma ordem.</summary>
    public bool Equals(DeclaracaoDaProvaPresencial? outra) =>
        outra is not null && TemProvaPresencial == outra.TemProvaPresencial && Municipios.SequenceEqual(outra.Municipios);

    public override int GetHashCode() => HashCode.Combine(TemProvaPresencial, Municipios.Count);

    public DeclaracaoDaProvaPresencial ComNomesDoGeo(MunicipiosConferidosNoGeo conferidos)
    {
        ArgumentNullException.ThrowIfNull(conferidos);
        return new(TemProvaPresencial, [.. Municipios.Select(municipio => municipio with
        {
            Nome = conferidos.NomeConferido(municipio.CodigoIbge) ?? municipio.Nome,
        })]);
    }

    /// <summary>
    /// Confere a forma da declaração e acumula as recusas (ADR-0125): sem prova presencial não há município,
    /// cada código tem a forma de município e a UF do prefixo, nenhum se repete, e a lista cabe no limite
    /// técnico. O processo com prova presencial e sem município ainda é uma declaração: a publicação é que
    /// o recusa.
    /// </summary>
    /// <param name="limiteDeMunicipios">O limite técnico de municípios por processo, que protege a conferência no Geo.</param>
    public static Result<DeclaracaoDaProvaPresencial> Declarar(
        bool temProvaPresencial, IReadOnlyList<MunicipioDeProvaInformado> municipios, int limiteDeMunicipios)
    {
        ArgumentNullException.ThrowIfNull(municipios);

        List<FieldError> recusas =
        [
            .. RecusasSemProva(temProvaPresencial, municipios.Count),
            .. RecusasDeCadaMunicipio(municipios),
            .. RecusasAcimaDoLimite(municipios.Count, limiteDeMunicipios),
        ];
        return recusas.Count > 0
            ? Result<DeclaracaoDaProvaPresencial>.ValidationFailure(recusas)
            : Result<DeclaracaoDaProvaPresencial>.Success(new(
                temProvaPresencial,
                [.. municipios.Select(static (municipio, indice) => new MunicipioDeProva(municipio.CodigoIbge!, municipio.Uf!, null, indice + 1))]));
    }

    private static IEnumerable<FieldError> RecusasSemProva(bool temProvaPresencial, int quantidade)
    {
        if (!temProvaPresencial && quantidade > 0)
        {
            yield return new FieldError(CampoDosMunicipios, new DomainError(
                ProvaPresencialErrorCodes.MunicipiosSemProvaPresencial,
                "O processo não declara prova presencial e não pode declarar municípios de prova."));
        }
    }

    private static IEnumerable<FieldError> RecusasAcimaDoLimite(int quantidade, int limiteDeMunicipios)
    {
        if (quantidade > limiteDeMunicipios)
        {
            yield return new FieldError(CampoDosMunicipios, new DomainError(
                ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite,
                "O processo declara mais municípios de prova do que o limite admite."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeCadaMunicipio(IReadOnlyList<MunicipioDeProvaInformado> municipios)
    {
        HashSet<string> jaDeclarados = new(StringComparer.Ordinal);
        for (int indice = 0; indice < municipios.Count; indice++)
        {
            string campo = $"{CampoDosMunicipios}[{indice}]";
            MunicipioDeProvaInformado? municipio = municipios[indice];
            foreach (FieldError recusa in RecusasDoCodigo(municipio?.CodigoIbge, campo)
                .Concat(RecusasDaUf(municipio?.CodigoIbge, municipio?.Uf, campo))
                .Concat(RecusasDaRepeticao(municipio?.CodigoIbge, jaDeclarados, campo)))
            {
                yield return recusa;
            }
        }
    }

    private static IEnumerable<FieldError> RecusasDoCodigo(string? codigoIbge, string campo)
    {
        if (string.IsNullOrWhiteSpace(codigoIbge))
        {
            yield return Recusa($"{campo}.codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio, "O código do IBGE do município é obrigatório.");
        }
        else if (!ReferenciaCidadeGeo.EhCodigoMunicipioValido(codigoIbge))
        {
            yield return Recusa($"{campo}.codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido, "O código do IBGE deve ter sete dígitos e o prefixo de uma UF.");
        }
    }

    private static IEnumerable<FieldError> RecusasDaRepeticao(string? codigoIbge, HashSet<string> jaDeclarados, string campo)
    {
        if (codigoIbge is not null && ReferenciaCidadeGeo.EhCodigoMunicipioValido(codigoIbge) && !jaDeclarados.Add(codigoIbge))
        {
            yield return Recusa($"{campo}.codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido, "O município já está declarado como município de prova do processo.");
        }
    }

    private static IEnumerable<FieldError> RecusasDaUf(string? codigoIbge, string? uf, string campo)
    {
        if (string.IsNullOrWhiteSpace(uf))
        {
            yield return Recusa($"{campo}.uf", CidadeReferenciaErrorCodes.UfObrigatoria, "A UF do município é obrigatória.");
        }
        else if (codigoIbge is { } codigo && ReferenciaCidadeGeo.UfDoCodigoIbge(codigo) is { } ufDoCodigo
            && !string.Equals(ufDoCodigo, uf, StringComparison.Ordinal))
        {
            yield return Recusa($"{campo}.uf", CidadeReferenciaErrorCodes.UfIncoerente, "A UF não é a do município pelo código do IBGE.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
