namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Unidade organizacional da Unifesspa: reitoria, pró-reitorias, centros,
/// institutos, faculdades, departamentos, coordenações, diretorias, divisões
/// e núcleos (ADR-0055). Agregado raiz com identidade rica (Slug, Sigla,
/// Codigo, Alias) e histórico de identificadores para auditoria temporal.
/// </summary>
/// <remarks>
/// <para>Slug, Sigla e Codigo são únicos entre unidades vivas. Alias não é
/// único — serve como agrupamento popular. A unicidade entre vivos é validada
/// pelo handler via repositório antes de chamar a factory ou Atualizar; a
/// restrição de banco (índice parcial) é a segunda linha de defesa.</para>
/// <para>Alterações nos identificadores únicos (Slug, Sigla, Codigo) e no
/// Alias geram entradas em <see cref="UnidadeIdentificadorHistorico"/> — a
/// data de abertura da nova vigência é o <paramref name="dataAtual"/> passado
/// pelo handler. O domínio não consulta relógio diretamente.</para>
/// <para>Detecção de ciclo na hierarquia é responsabilidade do handler (via
/// repositório); este agregado recebe o ID do superior já validado.</para>
/// <para>A cidade (issue #1114) é uma <strong>referência de cidade do Geo</strong>
/// (mesmo padrão de <c>Instituicao</c>, ADR-0090): <see cref="CidadeCodigoIbge"/>
/// (código IBGE de 7 dígitos) + display cache (<see cref="CidadeNome"/>,
/// <see cref="CidadeUf"/>), sem FK cross-banco nem chamada ao Geo. É
/// <strong>opcional all-or-nothing</strong> — nem toda Unidade administra um
/// certame (há <see cref="UnidadeAcademica"/> como flag, mas mesmo unidades
/// acadêmicas podem não ser administradoras), então exigir cidade em toda
/// Unidade infla cadastro sem uso; a obrigatoriedade real é do PONTO DE USO
/// (<c>ProcessoSeletivo</c> exige Unidade administradora com cidade —
/// uniplus-api#1114).</para>
/// </remarks>
public sealed class Unidade : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMaxLength = 250;
    private const int NomeMinLength = 2;
    private const int SiglaMaxLength = 50;
    private const int SiglaMinLength = 1;
    private const int CodigoMaxLength = 50;
    private const int CodigoMinLength = 1;
    private const int AliasMaxLength = 100;

    private readonly List<UnidadeIdentificadorHistorico> _historico = [];

    public string Nome { get; private set; } = string.Empty;
    public string? Alias { get; private set; }
    public Slug Slug { get; private set; }
    public string Sigla { get; private set; } = string.Empty;
    public string Codigo { get; private set; } = string.Empty;
    public Guid? UnidadeSuperiorId { get; private set; }
    public TipoUnidade Tipo { get; private set; }
    public bool UnidadeAcademica { get; private set; }
    public DateOnly VigenciaInicio { get; private set; }
    public DateOnly? VigenciaFim { get; private set; }

    // Referência de cidade do Geo (ADR-0090) — código + display cache, opcional
    // all-or-nothing (issue #1114). Mesmo padrão de Instituicao.
    public string? CidadeCodigoIbge { get; private set; }
    public string? CidadeNome { get; private set; }
    public string? CidadeUf { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public IReadOnlyList<UnidadeIdentificadorHistorico> Historico =>
        _historico.AsReadOnly();

    // EF Core materialization
    private Unidade()
    {
    }

    /// <summary>
    /// Cria uma nova Unidade com identidade rica. Os handlers de unicidade
    /// (Slug/Sigla/Codigo) e de detecção de ciclo na hierarquia são
    /// responsabilidade do handler chamador — esta factory valida apenas
    /// formato e domínio local.
    /// </summary>
    public static Result<Unidade> Criar(
        string? nome,
        string? alias,
        string? slug,
        string? sigla,
        string? codigo,
        Guid? unidadeSuperiorId,
        TipoUnidade tipo,
        bool unidadeAcademica,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        string? cidadeCodigoIbge = null,
        string? cidadeNome = null,
        string? cidadeUf = null)
    {
        Result validacao = ValidarCampos(
            nome, alias, slug, sigla, codigo, tipo, vigenciaInicio, vigenciaFim,
            cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (validacao.IsFailure)
        {
            return Result<Unidade>.ValidationFailure(validacao.Errors);
        }

        // ValidarCampos, chamado acima, já provou o formato de slug — a segunda
        // chamada é determinística e não pode falhar.
        Slug slugValidado = Slug.From(slug).Value!;

        var unidade = new Unidade
        {
            Nome = nome!.Trim(),
            Alias = alias?.Trim(),
            Slug = slugValidado,
            Sigla = sigla!.Trim().ToUpperInvariant(),
            Codigo = codigo!.Trim(),
            UnidadeSuperiorId = unidadeSuperiorId,
            Tipo = tipo,
            UnidadeAcademica = unidadeAcademica,
            VigenciaInicio = vigenciaInicio,
            VigenciaFim = vigenciaFim,
        };
        // CA1062: ValidarCampos, chamado acima, já prova a bicondicional entre os três
        // parâmetros — o analisador não relaciona essa prova entre parâmetros distintos.
#pragma warning disable CA1062
        unidade.AplicarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);
#pragma warning restore CA1062

        // Abre histórico inicial para identificadores com variação temporal.
        unidade._historico.Add(
            UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Slug, slugValidado.Valor, vigenciaInicio));
        unidade._historico.Add(
            UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Sigla, unidade.Sigla, vigenciaInicio));
        unidade._historico.Add(
            UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Codigo, unidade.Codigo, vigenciaInicio));
        if (unidade.Alias is not null)
        {
            unidade._historico.Add(
                UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Alias, unidade.Alias, vigenciaInicio));
        }

        return Result<Unidade>.Success(unidade);
    }

    /// <summary>
    /// Atualiza atributos da Unidade. Mudanças em Slug, Sigla, Codigo ou Alias
    /// encerram a vigência do valor anterior e abrem nova entrada de histórico.
    /// A data de mudança é o <paramref name="dataAtual"/> fornecido pelo handler.
    /// </summary>
    public Result Atualizar(
        string? nome,
        string? alias,
        string? slug,
        string? sigla,
        string? codigo,
        Guid? unidadeSuperiorId,
        TipoUnidade tipo,
        bool unidadeAcademica,
        DateOnly? vigenciaFim,
        DateOnly dataAtual,
        string? motivoMudancaIdentificador = null,
        string? cidadeCodigoIbge = null,
        string? cidadeNome = null,
        string? cidadeUf = null)
    {
        Result validacao = ValidarCampos(
            nome, alias, slug, sigla, codigo, tipo, VigenciaInicio, vigenciaFim,
            cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        // ValidarCampos, chamado acima, já provou o formato de slug — a segunda
        // chamada é determinística e não pode falhar.
        Slug slugValidado = Slug.From(slug).Value!;

        string siglaNormalizada = sigla!.Trim().ToUpperInvariant();
        string codigoNormalizado = codigo!.Trim();
        string? aliasNormalizado = alias?.Trim();

        RenomearIdentificadorSeNecessario(TipoIdentificador.Slug, Slug.Valor, slugValidado.Valor, dataAtual, motivoMudancaIdentificador);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Sigla, Sigla, siglaNormalizada, dataAtual, motivoMudancaIdentificador);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Codigo, Codigo, codigoNormalizado, dataAtual, motivoMudancaIdentificador);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Alias, Alias, aliasNormalizado, dataAtual, motivoMudancaIdentificador);

        Nome = nome!.Trim();
        Alias = aliasNormalizado;
        Slug = slugValidado;
        Sigla = siglaNormalizada;
        Codigo = codigoNormalizado;
        UnidadeSuperiorId = unidadeSuperiorId;
        Tipo = tipo;
        UnidadeAcademica = unidadeAcademica;
        VigenciaFim = vigenciaFim;
        // CA1062: ValidarCampos, chamado acima, já prova a bicondicional entre os três
        // parâmetros — o analisador não relaciona essa prova entre parâmetros distintos.
#pragma warning disable CA1062
        AplicarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);
#pragma warning restore CA1062

        return Result.Success();
    }

    /// <summary>
    /// Aplica a referência de cidade já validada (bicondicional): código presente
    /// implica trio completo, ausente zera os três campos.
    /// </summary>
    private void AplicarReferenciaCidade(string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf)
    {
        bool temCidade = !string.IsNullOrWhiteSpace(cidadeCodigoIbge);
        CidadeCodigoIbge = temCidade ? cidadeCodigoIbge!.Trim() : null;
        CidadeNome = temCidade ? cidadeNome!.Trim() : null;
        CidadeUf = temCidade ? cidadeUf!.Trim().ToUpperInvariant() : null;
    }

    private void RenomearIdentificadorSeNecessario(
        TipoIdentificador tipoIdentificador,
        string? valorAtual,
        string? novoValor,
        DateOnly dataAtual,
        string? motivo)
    {
        // Nenhuma mudança — sem histórico. Comparação Ordinal (case-sensitive):
        // os valores chegam já normalizados (Slug lowercase, Sigla uppercase), e
        // Codigo/Alias preservam a caixa — então uma troca só na caixa (ABC→abc)
        // conta como mudança e gera entrada de histórico.
        if (string.Equals(valorAtual, novoValor, StringComparison.Ordinal))
        {
            return;
        }

        // Caso especial: Alias pode ir de null para valor ou vice-versa — só
        // abrimos nova entrada quando o novo valor é não nulo.
        bool novoValorNulo = novoValor is null;

        // Fecha entrada aberta do identificador.
        UnidadeIdentificadorHistorico? entradaAberta = _historico
            .FirstOrDefault(h => h.TipoIdentificador == tipoIdentificador && h.VigenciaFim is null);
        entradaAberta?.FecharVigencia(dataAtual);

        // Abre nova entrada apenas se o novo valor é não nulo.
        if (!novoValorNulo)
        {
            _historico.Add(
                UnidadeIdentificadorHistorico.Abrir(Id, tipoIdentificador, novoValor!, dataAtual, motivo));
        }
    }

    /// <summary>
    /// Valida todos os campos da Unidade, acumulando cada violação em vez de
    /// retornar na primeira (ADR-0125) — o array <c>errors[]</c> do contrato
    /// público (ADR-0023) precisa de todas as regras de campo violadas no mesmo
    /// lote, não só a primeira. Público para o handler de atualização poder
    /// validar o payload inteiro antes de buscar o registro por Id (validação
    /// sempre vence 404) — mesmo uso que o handler de criação faz antes das
    /// checagens de unicidade (Slug/Sigla/Codigo) e do vínculo com a superior.
    /// </summary>
    public static Result ValidarCampos(
        string? nome,
        string? alias,
        string? slug,
        string? sigla,
        string? codigo,
        TipoUnidade tipo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf)
    {
        List<FieldError> erros = [];

        if (!Enum.IsDefined(tipo) || tipo == TipoUnidade.Nenhum)
        {
            erros.Add(new("tipo", new DomainError(
                UnidadeErrorCodes.TipoInvalido,
                "Tipo de Unidade inválido — use um valor definido em TipoUnidade, diferente de Nenhum.")));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                UnidadeErrorCodes.NomeObrigatorio, "Nome da Unidade é obrigatório.")));
        }
        else if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            erros.Add(new("nome", new DomainError(
                UnidadeErrorCodes.NomeTamanho,
                $"Nome da Unidade deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
        }

        Result<Slug> slugResult = Slug.From(slug);
        if (slugResult.IsFailure)
        {
            erros.Add(new("slug", slugResult.Error!));
        }

        if (string.IsNullOrWhiteSpace(sigla))
        {
            erros.Add(new("sigla", new DomainError(
                UnidadeErrorCodes.SiglaObrigatoria, "Sigla da Unidade é obrigatória.")));
        }
        else if (sigla.Trim().Length is < SiglaMinLength or > SiglaMaxLength)
        {
            erros.Add(new("sigla", new DomainError(
                UnidadeErrorCodes.SiglaTamanho,
                $"Sigla da Unidade deve ter entre {SiglaMinLength} e {SiglaMaxLength} caracteres.")));
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(new("codigo", new DomainError(
                UnidadeErrorCodes.CodigoObrigatorio, "Código da Unidade é obrigatório.")));
        }
        else if (codigo.Trim().Length is < CodigoMinLength or > CodigoMaxLength)
        {
            erros.Add(new("codigo", new DomainError(
                UnidadeErrorCodes.CodigoTamanho,
                $"Código da Unidade deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres.")));
        }

        if (alias is not null && alias.Trim().Length > AliasMaxLength)
        {
            erros.Add(new("alias", new DomainError(
                UnidadeErrorCodes.AliasTamanho,
                $"Alias da Unidade não pode ter mais que {AliasMaxLength} caracteres.")));
        }

        if (vigenciaFim.HasValue && vigenciaFim.Value < vigenciaInicio)
        {
            erros.Add(new("vigenciaFim", new DomainError(
                UnidadeErrorCodes.VigenciaFimAnteriorAoInicio,
                "Data de encerramento da vigência deve ser igual ou posterior à data de início.")));
        }

        Result cidade = ValidarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidade.IsFailure)
        {
            // ValidarReferenciaCidade acumula toda violação independente do trio de
            // cidade — cada uma mapeada para o campo real que descreve.
            foreach (FieldError erroCidade in cidade.Errors)
            {
                erros.Add(new(CampoDaCidade(erroCidade.Error.Code), erroCidade.Error));
            }
        }

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Mapeia o código interno de <see cref="ReferenciaCidadeGeo.Validar"/> para o
    /// campo do payload (camelCase, ADR-0023) a que ele se refere de fato — mesmo
    /// padrão de <c>Instituicao.CampoDaCidade</c>.
    /// </summary>
    private static string CampoDaCidade(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereNulo
            or CidadeReferenciaErrorCodes.NomeTamanho => "cidadeNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "cidadeUf",
        _ => "cidadeCodigoIbge",
    };

    /// <summary>
    /// Valida a referência de cidade do Geo (issue #1114) como opcional
    /// <strong>all-or-nothing</strong>: ausente por completo é válido; qualquer
    /// fragmento presente exige o trio (código IBGE + nome + UF) com formato e
    /// coerência de UF — delegado ao <see cref="ReferenciaCidadeGeo"/>, que
    /// reporta o campo faltante quando o preenchimento é parcial. Mesmo padrão
    /// de <c>Instituicao.ValidarReferenciaCidade</c>.
    /// </summary>
    private static Result ValidarReferenciaCidade(
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf)
    {
        bool algumPresente = !string.IsNullOrWhiteSpace(cidadeCodigoIbge)
            || !string.IsNullOrWhiteSpace(cidadeNome)
            || !string.IsNullOrWhiteSpace(cidadeUf);

        return algumPresente
            ? ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf)
            : Result.Success();
    }
}
