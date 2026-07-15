namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Entrada do vocabulário fechado de fatos do candidato — o catálogo
/// <c>rol_de_fatos_candidato</c> (UNI-REQ-0077, ADR-0111). Descreve o que o sistema
/// <em>sabe perguntar</em> sobre um candidato: para cada fato, o seu
/// <see cref="Dominio"/> (tipo de dado), a sua <see cref="Natureza"/> (origem do
/// dado), a sua <see cref="Cardinalidade"/> (um valor ou um conjunto) e, quando
/// aplicável, o conjunto fechado de <see cref="ValoresDominio"/>. Não armazena o
/// valor de nenhum candidato — é metadado de classificação, sem PII.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Seed-governado e append-only</strong> (molde de <c>RegraCatalogo</c>):
/// não é CRUD de administrador — as entradas são semeadas por código, e a entidade
/// deriva de <see cref="EntityBase"/> puro (sem soft-delete) e <strong>não expõe
/// método de mutação em runtime</strong>. A evolução do catálogo é feita por
/// <em>seed + nova migration</em>, não por API: a ADR-0111 permite corrigir a
/// <see cref="Descricao"/> (cosmética) e <strong>crescer</strong> (nunca remover
/// nem renomear) os <see cref="ValoresDominio"/> de um categórico estático — porque
/// remover um valor já citado orfanaria um predicado congelado (RN08).
/// </para>
/// <para>
/// <strong>Estático × escopo-processo</strong> é a nulidade de
/// <see cref="ValoresDominio"/>, não um campo próprio: um categórico com valores é
/// de conjunto <em>global</em> (ex.: <c>COR_RACA</c>); um categórico com
/// <see cref="ValoresDominio"/> nulo é de <em>escopo-processo</em> (ex.:
/// <c>MODALIDADE</c>) — os valores válidos são os ofertados pelo processo, e quem
/// os resolve é o consumidor, contra a oferta congelada no snapshot (ADR-0061).
/// </para>
/// </remarks>
public sealed class FatoCandidato : EntityBase
{
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public string Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public DominioFato Dominio { get; private set; }
    public NaturezaFato Natureza { get; private set; }
    public CardinalidadeFato Cardinalidade { get; private set; }

    /// <summary>
    /// Conjunto fechado de valores de um categórico <em>estático</em>. Nulo é
    /// significante: para categórico, nulo = escopo-processo; para booleano/numérico,
    /// é sempre nulo. Não confundir com lista vazia (proibida).
    /// </summary>
    public IReadOnlyList<string>? ValoresDominio { get; private set; }

    // Construtor de materialização do EF Core.
    private FatoCandidato()
    {
    }

    private FatoCandidato(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        NaturezaFato natureza,
        CardinalidadeFato cardinalidade,
        IReadOnlyList<string>? valoresDominio)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
        Dominio = dominio;
        Natureza = natureza;
        Cardinalidade = cardinalidade;
        ValoresDominio = valoresDominio;
    }

    /// <summary>
    /// Factory canônica (usada pelo seed): valida o código, o domínio, a natureza,
    /// a cardinalidade e a coerência de <paramref name="valoresDominio"/> com o
    /// domínio. Não há caminho de mutação após a criação.
    /// </summary>
    public static Result<FatoCandidato> Criar(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        NaturezaFato natureza,
        CardinalidadeFato cardinalidade,
        IReadOnlyList<string>? valoresDominio)
    {
        Result<CodigoFatoCandidato> codigoResult = CodigoFatoCandidato.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<FatoCandidato>.Failure(codigoResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha(FatoCandidatoErrorCodes.NomeObrigatorio, "Nome do fato é obrigatório.");
        }

        string nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length > NomeMaxLength)
        {
            return Falha(
                FatoCandidatoErrorCodes.NomeTamanho,
                $"Nome do fato deve ter no máximo {NomeMaxLength} caracteres.");
        }

        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > DescricaoMaxLength })
        {
            return Falha(
                FatoCandidatoErrorCodes.DescricaoTamanho,
                $"Descrição do fato deve ter no máximo {DescricaoMaxLength} caracteres.");
        }

        if (dominio == DominioFato.Nenhum)
        {
            return Falha(FatoCandidatoErrorCodes.DominioObrigatorio, "Domínio do fato é obrigatório.");
        }

        if (!Enum.IsDefined(dominio))
        {
            return Falha(FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado.");
        }

        if (natureza == NaturezaFato.Nenhuma)
        {
            return Falha(FatoCandidatoErrorCodes.NaturezaObrigatoria, "Natureza do fato é obrigatória.");
        }

        if (!Enum.IsDefined(natureza))
        {
            return Falha(FatoCandidatoErrorCodes.NaturezaInvalida, "Natureza do fato fora do vocabulário fechado.");
        }

        if (cardinalidade == CardinalidadeFato.Nenhuma)
        {
            return Falha(FatoCandidatoErrorCodes.CardinalidadeObrigatoria, "Cardinalidade do fato é obrigatória.");
        }

        if (!Enum.IsDefined(cardinalidade))
        {
            return Falha(FatoCandidatoErrorCodes.CardinalidadeInvalida, "Cardinalidade do fato fora do vocabulário fechado.");
        }

        Result<IReadOnlyList<string>?> valoresResult = ValidarValoresDominio(dominio, valoresDominio);
        if (valoresResult.IsFailure)
        {
            return Result<FatoCandidato>.Failure(valoresResult.Error!);
        }

        return Result<FatoCandidato>.Success(new FatoCandidato(
            codigoResult.Value!.Valor,
            nomeNormalizado,
            descricaoNormalizada,
            dominio,
            natureza,
            cardinalidade,
            valoresResult.Value));
    }

    private static Result<IReadOnlyList<string>?> ValidarValoresDominio(
        DominioFato dominio,
        IReadOnlyList<string>? valoresDominio)
    {
        if (valoresDominio is null)
        {
            return Result<IReadOnlyList<string>?>.Success(null);
        }

        if (dominio != DominioFato.Categorico)
        {
            return Result<IReadOnlyList<string>?>.Failure(new DomainError(
                FatoCandidatoErrorCodes.ValoresDominioNaoPermitidosForaDeCategorico,
                "Valores de domínio só são permitidos para fatos categóricos; "
                + "booleano e numérico têm valores nulos."));
        }

        if (valoresDominio.Count == 0 || valoresDominio.Any(string.IsNullOrWhiteSpace))
        {
            return Result<IReadOnlyList<string>?>.Failure(new DomainError(
                FatoCandidatoErrorCodes.ValoresDominioComItemEmBranco,
                "A lista de valores de domínio, quando presente, deve ser não vazia e sem itens em branco."));
        }

        string[] normalizados = [.. valoresDominio.Select(v => v.Trim())];
        if (normalizados.Distinct(StringComparer.Ordinal).Count() != normalizados.Length)
        {
            return Result<IReadOnlyList<string>?>.Failure(new DomainError(
                FatoCandidatoErrorCodes.ValoresDominioComDuplicata,
                "A lista de valores de domínio não pode conter duplicatas."));
        }

        return Result<IReadOnlyList<string>?>.Success(normalizados);
    }

    private static Result<FatoCandidato> Falha(string codigo, string mensagem) =>
        Result<FatoCandidato>.Failure(new DomainError(codigo, mensagem));
}
