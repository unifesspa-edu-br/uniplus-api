namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Entrada do vocabulário fechado de fatos do candidato — o catálogo
/// <c>rol_de_fatos_candidato</c> (UNI-REQ-0077, ADR-0111, refinada pela ADR-0116).
/// Descreve o que o sistema <em>sabe perguntar</em> sobre um candidato: para cada
/// fato, o seu <see cref="Dominio"/> (tipo de dado), a sua <see cref="Origem"/>
/// (como o valor chega ao sistema), a sua <see cref="Cardinalidade"/> (um valor ou
/// um conjunto), o seu <see cref="PontoResolucao"/> (a fase em que o valor fica
/// conhecido), o seu <see cref="Binding"/> (de onde/como o valor é produzido) e,
/// quando aplicável, o conjunto fechado de <see cref="ValoresDominio"/> ou de
/// <see cref="ValoresDominioDeclarados"/>. Não armazena o valor de nenhum
/// candidato — é metadado de classificação, sem PII.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Seed-governado e append-only</strong> (molde de <c>RegraCatalogo</c>):
/// não é CRUD de administrador — as entradas são semeadas por código, e a entidade
/// deriva de <see cref="EntityBase"/> puro (sem soft-delete). A evolução do
/// catálogo é feita por <em>seed + nova migration</em>, não por API: a ADR-0111
/// permite corrigir a <see cref="Descricao"/> (cosmética) e <strong>crescer</strong>
/// (nunca remover nem renomear) os <see cref="ValoresDominio"/>/
/// <see cref="ValoresDominioDeclarados"/> de um categórico estático — porque
/// remover um valor já citado orfanaria um predicado congelado (RN08). A única
/// mutação de instância é <see cref="AdicionarValorDominio"/>, exercida pelo
/// próprio seed para montar os <see cref="ValoresDominioDeclarados"/> — nunca em
/// runtime de requisição.
/// </para>
/// <para>
/// <strong>Estático × escopo-processo</strong> é a nulidade de
/// <see cref="ValoresDominio"/>/<see cref="ValoresDominioDeclarados"/>, não um
/// campo próprio: um categórico com valores é de conjunto <em>global</em> (ex.:
/// <c>COR_RACA</c>); um categórico com valores nulos é de <em>escopo-processo</em>
/// (ex.: <c>MODALIDADE</c>, <c>TIPO_DEFICIENCIA</c>) — os valores válidos são os
/// ofertados pelo processo, e quem os resolve é o consumidor, contra a oferta
/// congelada no snapshot (ADR-0061).
/// </para>
/// </remarks>
public sealed class FatoCandidato : EntityBase
{
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int BindingMaxLength = 200;

    private const string PrefixoBindingDerivadoAtributo = "ATRIBUTO_CANDIDATO";
    private const string PrefixoBindingDerivadoRegra = "REGRA_DERIVACAO";
    private const string PrefixoBindingDeclarado = "CAMPO_INSCRICAO";
    private const string PrefixoBindingIntegracao = "INTEGRACAO";

    // Um fato derivado tem dois mecanismos de produção de valor: computar de um atributo do
    // candidato (FAIXA_ETARIA, RENDA_PER_CAPITA) ou referenciar a regra de derivação congelada do
    // processo (MODALIDADE). O catálogo é global e diz o mecanismo; a config do edital diz o
    // conteúdo. Declarado e Integracao seguem com um prefixo cada (ADR-0116, emenda de 2026-07-22).
    private static readonly Dictionary<OrigemFato, IReadOnlyList<string>> PrefixosBindingPorOrigem =
        new()
        {
            [OrigemFato.Derivado] = [PrefixoBindingDerivadoAtributo, PrefixoBindingDerivadoRegra],
            [OrigemFato.Declarado] = [PrefixoBindingDeclarado],
            [OrigemFato.Integracao] = [PrefixoBindingIntegracao],
        };

    public string Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public DominioFato Dominio { get; private set; }
    public OrigemFato Origem { get; private set; }
    public CardinalidadeFato Cardinalidade { get; private set; }

    /// <summary>
    /// Código canônico da fase (<see cref="FaseCanonicaCatalogo"/>) em que o valor
    /// deste fato fica conhecido (ADR-0116). Não é FK — é referência por valor,
    /// como <see cref="Codigo"/> de <c>Modalidade</c>.
    /// </summary>
    public string PontoResolucao { get; private set; } = null!;

    /// <summary>
    /// Referência de onde/como o valor do fato é produzido (ADR-0116), no formato
    /// <c>"{PREFIXO}:{REFERENCIA}"</c> com prefixo coerente com <see cref="Origem"/>.
    /// </summary>
    public string Binding { get; private set; } = null!;

    /// <summary>
    /// Conjunto fechado de valores de um categórico <em>estático</em>. Nulo é
    /// significante: para categórico, nulo = escopo-processo; para booleano/numérico,
    /// é sempre nulo. Não confundir com lista vazia (proibida).
    /// </summary>
    public IReadOnlyList<string>? ValoresDominio { get; private set; }

    private readonly List<FatoValorDominio> _valoresDominioDeclarados = [];

    /// <summary>
    /// Descrição por valor de um categórico estático (ADR-0116) — complementa
    /// <see cref="ValoresDominio"/> com a descrição que orienta a escolha do
    /// candidato quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/>.
    /// Não ordenada aqui (mesmo padrão de <c>OfertaAtendimentoEspecializado.Condicoes</c>)
    /// — a ordenação por <c>Ordem</c>/<c>Codigo</c> é responsabilidade de quem
    /// projeta a leitura (<c>FatoCandidatoReader</c>), não do agregado.
    /// </summary>
    public IReadOnlyCollection<FatoValorDominio> ValoresDominioDeclarados => _valoresDominioDeclarados.AsReadOnly();

    // Construtor de materialização do EF Core.
    private FatoCandidato()
    {
    }

    private FatoCandidato(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        OrigemFato origem,
        CardinalidadeFato cardinalidade,
        IReadOnlyList<string>? valoresDominio,
        string pontoResolucao,
        string binding)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
        Dominio = dominio;
        Origem = origem;
        Cardinalidade = cardinalidade;
        ValoresDominio = valoresDominio;
        PontoResolucao = pontoResolucao;
        Binding = binding;
    }

    /// <summary>
    /// Factory canônica (usada pelo seed): valida o código, o domínio, a origem,
    /// a cardinalidade, o ponto de resolução, o binding e a coerência de
    /// <paramref name="valoresDominio"/> com o domínio. A única mutação após a
    /// criação é <see cref="AdicionarValorDominio"/>.
    /// </summary>
    public static Result<FatoCandidato> Criar(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        OrigemFato origem,
        CardinalidadeFato cardinalidade,
        IReadOnlyList<string>? valoresDominio,
        string pontoResolucao,
        string binding)
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

        if (origem == OrigemFato.Nenhuma)
        {
            return Falha(FatoCandidatoErrorCodes.OrigemObrigatoria, "Origem do fato é obrigatória.");
        }

        if (!Enum.IsDefined(origem))
        {
            return Falha(FatoCandidatoErrorCodes.OrigemInvalida, "Origem do fato fora do vocabulário fechado.");
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

        Result<string> pontoResolucaoResult = ValidarPontoResolucao(pontoResolucao);
        if (pontoResolucaoResult.IsFailure)
        {
            return Result<FatoCandidato>.Failure(pontoResolucaoResult.Error!);
        }

        Result<string> bindingResult = ValidarBinding(binding, origem);
        if (bindingResult.IsFailure)
        {
            return Result<FatoCandidato>.Failure(bindingResult.Error!);
        }

        return Result<FatoCandidato>.Success(new FatoCandidato(
            codigoResult.Value!.Valor,
            nomeNormalizado,
            descricaoNormalizada,
            dominio,
            origem,
            cardinalidade,
            valoresResult.Value,
            pontoResolucaoResult.Value!,
            bindingResult.Value!));
    }

    /// <summary>
    /// Adiciona um valor ao conjunto fechado de um categórico estático (ADR-0116).
    /// Só o agregado conhece o necessário para validar: que o próprio
    /// <see cref="Dominio"/> é <see cref="DominioFato.Categorico"/>, que o
    /// <paramref name="codigo"/> (normalizado por trim, comparação ordinal) não
    /// colide com um irmão já adicionado, e que a <paramref name="descricao"/> é
    /// obrigatória quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/>.
    /// </summary>
    public Result AdicionarValorDominio(string codigo, string? descricao, int ordem, bool ativo)
    {
        if (Dominio != DominioFato.Categorico)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico,
                "Valores de domínio só podem ser adicionados a um fato categórico."));
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.CodigoObrigatorio,
                "Código do valor de domínio é obrigatório."));
        }

        string codigoNormalizado = codigo.Trim();
        if (codigoNormalizado.Length > FatoValorDominio.CodigoMaxLength)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.CodigoTamanho,
                $"Código do valor de domínio deve ter no máximo {FatoValorDominio.CodigoMaxLength} caracteres."));
        }

        if (_valoresDominioDeclarados.Any(v => string.Equals(v.Codigo, codigoNormalizado, StringComparison.Ordinal)))
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.CodigoDuplicado,
                $"Já existe um valor de domínio com o código '{codigoNormalizado}' neste fato."));
        }

        string? descricaoNormalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoNormalizada is { Length: > FatoValorDominio.DescricaoMaxLength })
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.DescricaoTamanho,
                $"Descrição do valor de domínio deve ter no máximo {FatoValorDominio.DescricaoMaxLength} caracteres."));
        }

        if (Origem == OrigemFato.Declarado && descricaoNormalizada is null)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.DescricaoObrigatoria,
                "Descrição do valor de domínio é obrigatória quando a origem do fato é DECLARADO."));
        }

        if (ordem < 0)
        {
            return Result.Failure(new DomainError(
                FatoValorDominioErrorCodes.OrdemInvalida,
                "Ordem do valor de domínio não pode ser negativa."));
        }

        _valoresDominioDeclarados.Add(FatoValorDominio.Criar(Id, codigoNormalizado, descricaoNormalizada, ordem, ativo));
        return Result.Success();
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

    private static Result<string> ValidarPontoResolucao(string pontoResolucao)
    {
        if (string.IsNullOrWhiteSpace(pontoResolucao))
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.PontoResolucaoObrigatorio,
                "Ponto de resolução do fato é obrigatório."));
        }

        string normalizado = pontoResolucao.Trim();
        if (!FaseCanonicaCatalogo.EhCanonico(normalizado))
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.PontoResolucaoInvalido,
                "Ponto de resolução do fato fora do conjunto canônico das quatorze fases."));
        }

        return Result<string>.Success(normalizado);
    }

    private static Result<string> ValidarBinding(string binding, OrigemFato origem)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingObrigatorio,
                "Binding do fato é obrigatório."));
        }

        string normalizado = binding.Trim();
        if (normalizado.Length > BindingMaxLength)
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingFormatoInvalido,
                $"Binding do fato deve ter no máximo {BindingMaxLength} caracteres."));
        }

        int separador = normalizado.IndexOf(':', StringComparison.Ordinal);
        if (separador <= 0 || separador == normalizado.Length - 1)
        {
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingFormatoInvalido,
                "Binding deve seguir o formato \"{PREFIXO}:{REFERENCIA}\", com prefixo e referência não vazios."));
        }

        string prefixo = normalizado[..separador];
        IReadOnlyList<string> prefixosAceitos = PrefixosBindingAceitos(origem);
        if (!prefixosAceitos.Contains(prefixo, StringComparer.Ordinal))
        {
            string esperado = prefixosAceitos.Count == 1
                ? $"\"{prefixosAceitos[0]}\""
                : $"um de {string.Join(", ", prefixosAceitos.Select(p => $"\"{p}\""))}";
            return Result<string>.Failure(new DomainError(
                FatoCandidatoErrorCodes.BindingPrefixoIncoerenteComOrigem,
                $"O prefixo do binding deve ser {esperado} para a origem {origem} (recebido \"{prefixo}\")."));
        }

        return Result<string>.Success(normalizado);
    }

    private static IReadOnlyList<string> PrefixosBindingAceitos(OrigemFato origem) =>
        PrefixosBindingPorOrigem.TryGetValue(origem, out IReadOnlyList<string>? prefixos)
            ? prefixos
            : throw new ArgumentOutOfRangeException(nameof(origem), origem, "Origem de fato fora do domínio fechado.");

    private static Result<FatoCandidato> Falha(string codigo, string mensagem) =>
        Result<FatoCandidato>.Failure(new DomainError(codigo, mensagem));
}
