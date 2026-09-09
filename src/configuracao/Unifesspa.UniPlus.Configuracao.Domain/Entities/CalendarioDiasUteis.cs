namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um dataset versionado do calendário de dias não úteis (feriados nacionais,
/// estaduais, municipais e recessos institucionais) usado para contar prazos
/// expressos em dias úteis (RN13). Cada entrada marca o dia civil inteiro, nunca meio
/// expediente (UNI-REQ-0114); ter um dataset vigente é condição para publicar certame
/// cuja contagem distinga dia útil de não útil (UNI-REQ-0116).
/// </summary>
/// <remarks>
/// <para>O dataset nasce completo em <see cref="Criar"/> e admite inclusão pontual de
/// data via <see cref="IncluirDiaNaoUtil"/> — correção ou complemento antes de o
/// calendário ser consolidado em um Processo Seletivo publicado (decisão de PO,
/// api#1458). A operação preserva <see cref="Id"/> e <see cref="VersaoDataset"/> e
/// nunca remove/edita data já cadastrada. Processos Seletivos já publicados não são
/// afetados: eles carregam cópia por valor do calendário congelada na publicação
/// (padrão snapshot-copy, ADR-0061), sem referência viva a este agregado.</para>
/// <para>No máximo um dataset é vigente por vez — a invariante cross-agregado é
/// aplicada pelo handler (que desmarca o vigente anterior na mesma transação) e
/// reforçada por índice único parcial de banco, nunca só por guarda em memória.</para>
/// <para>Motor de contagem de dias úteis (como um instante-âncora em dia não útil se
/// resolve) é escopo futuro do módulo de Recursos — esta entidade só entrega o
/// cadastro e a pergunta "há dataset vigente?".</para>
/// </remarks>
public sealed class CalendarioDiasUteis : SoftDeletableEntity, IAuditableEntity
{
    private const int VersaoDatasetMinLength = 1;
    private const int VersaoDatasetMaxLength = 60;
    private const int DescricaoMaxLength = 200;

    private readonly List<DiaNaoUtil> _diasNaoUteis = [];

    public string VersaoDataset { get; private set; } = string.Empty;
    public bool Vigente { get; private set; }
    public IReadOnlyList<DiaNaoUtil> DiasNaoUteis => _diasNaoUteis.AsReadOnly();

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private CalendarioDiasUteis()
    {
    }

    /// <summary>
    /// Cria um dataset novo, com a lista completa de dias não úteis. Nasce sempre
    /// <b>não vigente</b> — tornar-se o dataset corrente é uma operação explícita
    /// (<see cref="MarcarVigente"/>), nunca implícita na criação. <c>Abrangencia</c>
    /// chega como token canônico UPPER_SNAKE (ex.: <c>NACIONAL</c>) — a análise e a
    /// validação de campo ocorrem aqui, não no chamador.
    /// </summary>
    public static Result<CalendarioDiasUteis> Criar(string? versaoDataset, IReadOnlyList<DiaNaoUtilCriacao?>? diasNaoUteis)
    {
        Result<(string VersaoDataset, IReadOnlyList<DiaNaoUtilResolvido> DiasNaoUteis)> validacao =
            ValidarCampos(versaoDataset, diasNaoUteis);
        if (validacao.IsFailure)
        {
            return Result<CalendarioDiasUteis>.ValidationFailure(validacao.Errors);
        }

        var calendario = new CalendarioDiasUteis
        {
            VersaoDataset = validacao.Value.VersaoDataset,
            Vigente = false,
        };

        foreach (DiaNaoUtilResolvido dia in validacao.Value.DiasNaoUteis)
        {
            calendario._diasNaoUteis.Add(DiaNaoUtil.Abrir(
                calendario.Id,
                dia.Abrangencia,
                dia.MunicipioIbge,
                dia.MunicipioNome,
                dia.MunicipioUf,
                dia.Uf,
                dia.Data,
                dia.Descricao));
        }

        return Result<CalendarioDiasUteis>.Success(calendario);
    }

    /// <summary>
    /// Marca este dataset como o vigente. Falha se já estiver vigente — chamar de
    /// novo sobre o mesmo dataset é erro do caller, não um no-op silencioso. Não
    /// desmarca nenhum outro dataset: essa é responsabilidade do handler, que
    /// precisa localizar o vigente anterior (se houver) na mesma transação.
    /// </summary>
    public Result MarcarVigente()
    {
        if (Vigente)
        {
            return Result.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.JaVigente,
                "Este dataset já é o vigente."));
        }

        Vigente = true;
        return Result.Success();
    }

    /// <summary>Desmarca este dataset como vigente. Chamado pelo handler sobre o vigente anterior.</summary>
    public void MarcarNaoVigente() => Vigente = false;

    /// <summary>
    /// Inclui uma data não útil neste dataset já existente, preservando
    /// <see cref="Id"/>, <see cref="VersaoDataset"/> e todas as datas já
    /// cadastradas (api#1458). Aplica as mesmas validações de campo usadas em
    /// <see cref="Criar"/> e recusa duplicata contra as datas <b>já persistidas</b>
    /// no agregado — diferente da checagem de <see cref="Criar"/>, que só compara
    /// itens dentro do mesmo payload de criação, porque ali o dataset ainda não
    /// existe para nenhum outro chamador competir. A defesa final contra duplicata
    /// sob concorrência real é o índice único de banco (sem token de concorrência
    /// no pai: inserir um filho não muta o pai, então nada aqui depende de
    /// <c>xmin</c>).
    /// </summary>
    public Result<DiaNaoUtil> IncluirDiaNaoUtil(DiaNaoUtilCriacao? item)
    {
        Result<DiaNaoUtilResolvido> validacao = ValidarItem(item, "item");
        if (validacao.IsFailure)
        {
            return Result<DiaNaoUtil>.ValidationFailure(validacao.Errors);
        }

        DiaNaoUtilResolvido resolvido = validacao.Value!;

        bool duplicado = _diasNaoUteis.Any(existente =>
            existente.Data == resolvido.Data
            && existente.Abrangencia == resolvido.Abrangencia
            && existente.MunicipioIbge == resolvido.MunicipioIbge
            && existente.Uf == resolvido.Uf);
        if (duplicado)
        {
            return Result<DiaNaoUtil>.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset,
                "Data duplicada no dataset (mesma abrangência, município e UF)."));
        }

        DiaNaoUtil dia = DiaNaoUtil.Abrir(
            Id,
            resolvido.Abrangencia,
            resolvido.MunicipioIbge,
            resolvido.MunicipioNome,
            resolvido.MunicipioUf,
            resolvido.Uf,
            resolvido.Data,
            resolvido.Descricao);
        _diasNaoUteis.Add(dia);

        return Result<DiaNaoUtil>.Success(dia);
    }

    /// <summary>
    /// Valida e normaliza versão do dataset e a lista de dias não úteis,
    /// acumulando toda violação independente em vez de parar na primeira — cada
    /// item da lista é rotulado por posição (<c>diasNaoUteis[i]</c> e seus
    /// subcampos), já que a lista é heterogênea (cada item pode falhar por uma
    /// causa distinta). Checagens dependentes de um campo (coerência de
    /// município, obrigatoriedade de UF) só rodam quando esse campo já é válido,
    /// para não mascarar a causa raiz.
    /// </summary>
    private static Result<(string VersaoDataset, IReadOnlyList<DiaNaoUtilResolvido> DiasNaoUteis)> ValidarCampos(
        string? versaoDataset, IReadOnlyList<DiaNaoUtilCriacao?>? diasNaoUteis)
    {
        List<FieldError> erros = [];

        string? versaoNormalizada = null;
        if (string.IsNullOrWhiteSpace(versaoDataset))
        {
            erros.Add(new("versaoDataset", new DomainError(
                CalendarioDiasUteisErrorCodes.VersaoDatasetObrigatoria, "Versão do dataset é obrigatória.")));
        }
        else
        {
            versaoNormalizada = versaoDataset.Trim();
            if (versaoNormalizada.Length is < VersaoDatasetMinLength or > VersaoDatasetMaxLength)
            {
                erros.Add(new("versaoDataset", new DomainError(
                    CalendarioDiasUteisErrorCodes.VersaoDatasetTamanho,
                    $"Versão do dataset deve ter entre {VersaoDatasetMinLength} e {VersaoDatasetMaxLength} caracteres.")));
                versaoNormalizada = null;
            }
        }

        var resolvidos = new List<DiaNaoUtilResolvido>(diasNaoUteis?.Count ?? 0);

        if (diasNaoUteis is null || diasNaoUteis.Count == 0)
        {
            erros.Add(new("diasNaoUteis", new DomainError(
                CalendarioDiasUteisErrorCodes.SemDiaNaoUtil,
                "O dataset precisa de ao menos um dia não útil — um calendário vazio não conta nada.")));
        }
        else
        {
            var vistos = new HashSet<(DateOnly Data, Abrangencia Abrangencia, string? Municipio, string? Uf)>();

            for (int indice = 0; indice < diasNaoUteis.Count; indice++)
            {
                string prefixo = $"diasNaoUteis[{indice}]";
                Result<DiaNaoUtilResolvido> validacaoItem = ValidarItem(diasNaoUteis[indice], prefixo);
                if (validacaoItem.IsFailure)
                {
                    erros.AddRange(validacaoItem.Errors);
                    continue;
                }

                DiaNaoUtilResolvido resolvido = validacaoItem.Value!;

                if (!vistos.Add((resolvido.Data, resolvido.Abrangencia, resolvido.MunicipioIbge, resolvido.Uf)))
                {
                    erros.Add(new(prefixo, new DomainError(
                        CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset,
                        "Data duplicada no dataset (mesma abrangência, município e UF).")));
                    continue;
                }

                resolvidos.Add(resolvido);
            }
        }

        if (erros.Count > 0)
        {
            return Result<(string, IReadOnlyList<DiaNaoUtilResolvido>)>.ValidationFailure(erros);
        }

        return Result<(string, IReadOnlyList<DiaNaoUtilResolvido>)>.Success((versaoNormalizada!, resolvidos));
    }

    /// <summary>
    /// Valida e normaliza um único item de dia não útil, retornando os erros já
    /// rotulados por <paramref name="prefixo"/> — <c>"diasNaoUteis[i]"</c> quando
    /// chamado de dentro do laço de <see cref="ValidarCampos"/>, <c>"item"</c>
    /// quando chamado de <see cref="IncluirDiaNaoUtil"/>. Extraído de
    /// <see cref="ValidarCampos"/> para reuso entre os dois pontos de entrada — não
    /// faz checagem de duplicata: cada chamador decide contra qual conjunto (o
    /// próprio payload em <see cref="Criar"/>, ou o agregado já persistido em
    /// <see cref="IncluirDiaNaoUtil"/>).
    /// </summary>
    private static Result<DiaNaoUtilResolvido> ValidarItem(DiaNaoUtilCriacao? dia, string prefixo)
    {
        List<FieldError> erros = [];

        if (dia is null)
        {
            erros.Add(new(prefixo, new DomainError(
                CalendarioDiasUteisErrorCodes.DiaNaoUtilNulo, "Item de dia não útil não pode ser nulo.")));
            return Result<DiaNaoUtilResolvido>.ValidationFailure(erros);
        }

        bool abrangenciaValida = Abrangencias.TryAnalisar(dia.Abrangencia, out Abrangencia abrangencia);
        if (!abrangenciaValida)
        {
            erros.Add(new($"{prefixo}.abrangencia", new DomainError(
                CalendarioDiasUteisErrorCodes.AbrangenciaInvalida,
                "Abrangência inválida. Deve ser uma de: " + string.Join(", ", Abrangencias.TokensCanonicos) + ".")));
        }

        string? municipioIbgeNorm = string.IsNullOrWhiteSpace(dia.MunicipioIbge) ? null : dia.MunicipioIbge.Trim();
        string? municipioNomeNorm = string.IsNullOrWhiteSpace(dia.MunicipioNome) ? null : dia.MunicipioNome.Trim();
        string? municipioUfNorm = string.IsNullOrWhiteSpace(dia.MunicipioUf)
            ? null
            : dia.MunicipioUf.Trim().ToUpperInvariant();

        if (abrangenciaValida && abrangencia == Abrangencia.Municipal)
        {
            Result referenciaMunicipal = ReferenciaCidadeGeo.Validar(
                municipioIbgeNorm,
                municipioNomeNorm,
                municipioUfNorm);

            if (referenciaMunicipal.IsFailure)
            {
                foreach (FieldError erroMunicipio in referenciaMunicipal.Errors)
                {
                    erros.Add(new($"{prefixo}.{CampoDoMunicipio(erroMunicipio.Error.Code)}", erroMunicipio.Error));
                }
            }
        }
        else if (abrangenciaValida
            && (municipioIbgeNorm is not null || municipioNomeNorm is not null || municipioUfNorm is not null))
        {
            erros.Add(new(prefixo, new DomainError(
                CalendarioDiasUteisErrorCodes.SnapshotMunicipalApenasParaMunicipal,
                "Referência do município só se aplica a abrangência municipal.")));
        }

        string? ufNorm = string.IsNullOrWhiteSpace(dia.Uf) ? null : dia.Uf.Trim().ToUpperInvariant();

        if (abrangenciaValida && abrangencia == Abrangencia.Estadual)
        {
            if (ufNorm is null)
            {
                erros.Add(new($"{prefixo}.uf", new DomainError(
                    CalendarioDiasUteisErrorCodes.UfObrigatoriaParaEstadual,
                    "UF é obrigatória para abrangência estadual — sem ela, feriados de estados "
                    + "diferentes seriam indistinguíveis.")));
            }
            else if (ufNorm.Length != ReferenciaCidadeGeo.UfLength || !ReferenciaCidadeGeo.EhUfValida(ufNorm))
            {
                erros.Add(new($"{prefixo}.uf", new DomainError(
                    CalendarioDiasUteisErrorCodes.UfFormatoInvalido,
                    "UF deve ser uma das 27 siglas válidas.")));
                ufNorm = null;
            }
        }
        else if (abrangenciaValida && ufNorm is not null)
        {
            erros.Add(new($"{prefixo}.uf", new DomainError(
                CalendarioDiasUteisErrorCodes.UfApenasParaEstadual,
                "UF só se aplica a abrangência estadual.")));
            ufNorm = null;
        }

        string? descricaoNorm = null;
        if (string.IsNullOrWhiteSpace(dia.Descricao))
        {
            erros.Add(new($"{prefixo}.descricao", new DomainError(
                CalendarioDiasUteisErrorCodes.DescricaoObrigatoria, "Descrição é obrigatória.")));
        }
        else
        {
            descricaoNorm = dia.Descricao.Trim();
            if (descricaoNorm.Length > DescricaoMaxLength)
            {
                erros.Add(new($"{prefixo}.descricao", new DomainError(
                    CalendarioDiasUteisErrorCodes.DescricaoTamanho,
                    $"Descrição deve ter no máximo {DescricaoMaxLength} caracteres.")));
                descricaoNorm = null;
            }
        }

        if (erros.Count > 0)
        {
            return Result<DiaNaoUtilResolvido>.ValidationFailure(erros);
        }

        return Result<DiaNaoUtilResolvido>.Success(new DiaNaoUtilResolvido(
            abrangencia,
            municipioIbgeNorm,
            municipioNomeNorm,
            municipioUfNorm,
            ufNorm,
            dia.Data,
            descricaoNorm!));
    }

    /// <summary>
    /// Mapeia o código interno de <see cref="ReferenciaCidadeGeo.Validar"/> para o
    /// subcampo (camelCase, ADR-0023) do item de dia não útil a que ele se refere
    /// de fato — sem isso, todo erro de município seria rotulado com o mesmo
    /// campo, mesmo quando a causa é o nome ou a UF, não o código IBGE.
    /// </summary>
    private static string CampoDoMunicipio(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereNulo
            or CidadeReferenciaErrorCodes.NomeTamanho => "municipioNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "municipioUf",
        _ => "municipioIbge",
    };
}

/// <summary>
/// Entrada de criação de um <see cref="DiaNaoUtil"/>, usada só por
/// <see cref="CalendarioDiasUteis.Criar"/>. <c>Abrangencia</c> é o token canônico
/// UPPER_SNAKE (ex.: <c>NACIONAL</c>, <c>MUNICIPAL</c>), analisado internamente.
/// <c>MunicipioIbge</c>, <c>MunicipioNome</c> e <c>MunicipioUf</c> formam um
/// snapshot atômico obrigatório apenas para abrangência MUNICIPAL.
/// <c>Uf</c> é obrigatória para abrangência ESTADUAL — sem ela, feriados de
/// estados diferentes seriam indistinguíveis para o consumidor cross-módulo.
/// </summary>
/// <remarks>
/// <c>Abrangencia</c> e <c>Descricao</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// um item malformado no payload chega com esses campos nulos, e a validação
/// de domínio (não o model binding) precisa ser quem recusa.
/// </remarks>
public sealed record DiaNaoUtilCriacao(
    string? Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    DateOnly Data,
    string? Descricao,
    string? Uf = null);

/// <summary>Dia não útil já validado e com os campos analisados/normalizados, pronto para <see cref="DiaNaoUtil.Abrir"/>.</summary>
internal sealed record DiaNaoUtilResolvido(
    Abrangencia Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    string? Uf,
    DateOnly Data,
    string Descricao);
