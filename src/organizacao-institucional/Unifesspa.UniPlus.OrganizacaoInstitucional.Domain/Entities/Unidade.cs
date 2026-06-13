namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Services;
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
    public OrigemUnidade Origem { get; private set; }

    /// <summary>
    /// Índice de busca desnormalizado (acento/caixa-insensível) sobre os campos
    /// pesquisáveis — nome, sigla, código, slug e alias (issue #640). Mantido
    /// pelo agregado em <see cref="Criar"/>/<see cref="Atualizar"/> via
    /// <see cref="NormalizadorTermoBusca"/>, espelhando o <c>normalizarBusca</c>
    /// do frontend. É projeção de leitura: não compõe o contrato de saída
    /// (<c>UnidadeDto</c>/<c>UnidadeView</c>), só alimenta o filtro server-side.
    /// </summary>
    public string BuscaNormalizada { get; private set; } = string.Empty;

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
        string nome,
        string? alias,
        Slug slug,
        string sigla,
        string codigo,
        Guid? unidadeSuperiorId,
        TipoUnidade tipo,
        bool unidadeAcademica,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        OrigemUnidade origem)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(codigo);

        Result validacao = ValidarCampos(nome, alias, sigla, codigo, tipo, origem, vigenciaInicio, vigenciaFim);
        if (validacao.IsFailure)
        {
            return Result<Unidade>.Failure(validacao.Error!);
        }

        var unidade = new Unidade
        {
            Nome = nome.Trim(),
            Alias = alias?.Trim(),
            Slug = slug,
            Sigla = sigla.Trim().ToUpperInvariant(),
            Codigo = codigo.Trim(),
            UnidadeSuperiorId = unidadeSuperiorId,
            Tipo = tipo,
            UnidadeAcademica = unidadeAcademica,
            VigenciaInicio = vigenciaInicio,
            VigenciaFim = vigenciaFim,
            Origem = origem,
        };

        // Abre histórico inicial para identificadores com variação temporal.
        unidade._historico.Add(
            UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Slug, slug.Valor, vigenciaInicio));
        unidade._historico.Add(
            UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Sigla, unidade.Sigla, vigenciaInicio));
        unidade._historico.Add(
            UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Codigo, unidade.Codigo, vigenciaInicio));
        if (unidade.Alias is not null)
        {
            unidade._historico.Add(
                UnidadeIdentificadorHistorico.Abrir(unidade.Id, TipoIdentificador.Alias, unidade.Alias, vigenciaInicio));
        }

        unidade.AtualizarIndiceDeBusca();

        return Result<Unidade>.Success(unidade);
    }

    /// <summary>
    /// Atualiza atributos da Unidade. Mudanças em Slug, Sigla, Codigo ou Alias
    /// encerram a vigência do valor anterior e abrem nova entrada de histórico.
    /// A data de mudança é o <paramref name="dataAtual"/> fornecido pelo handler.
    /// </summary>
    public Result Atualizar(
        string nome,
        string? alias,
        Slug slug,
        string sigla,
        string codigo,
        Guid? unidadeSuperiorId,
        TipoUnidade tipo,
        bool unidadeAcademica,
        DateOnly? vigenciaFim,
        DateOnly dataAtual,
        string? motivoMudancaIdentificador = null)
    {
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(codigo);

        Result validacao = ValidarCampos(nome, alias, sigla, codigo, tipo, Origem, VigenciaInicio, vigenciaFim);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        string siglaNormalizada = sigla.Trim().ToUpperInvariant();
        string codigoNormalizado = codigo.Trim();
        string? aliasNormalizado = alias?.Trim();

        RenomearIdentificadorSeNecessario(TipoIdentificador.Slug, Slug.Valor, slug.Valor, dataAtual, motivoMudancaIdentificador);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Sigla, Sigla, siglaNormalizada, dataAtual, motivoMudancaIdentificador);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Codigo, Codigo, codigoNormalizado, dataAtual, motivoMudancaIdentificador);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Alias, Alias, aliasNormalizado, dataAtual, motivoMudancaIdentificador);

        Nome = nome.Trim();
        Alias = aliasNormalizado;
        Slug = slug;
        Sigla = siglaNormalizada;
        Codigo = codigoNormalizado;
        UnidadeSuperiorId = unidadeSuperiorId;
        Tipo = tipo;
        UnidadeAcademica = unidadeAcademica;
        VigenciaFim = vigenciaFim;

        AtualizarIndiceDeBusca();

        return Result.Success();
    }

    private void AtualizarIndiceDeBusca()
    {
        BuscaNormalizada = NormalizadorTermoBusca.ParaIndice(Nome, Sigla, Codigo, Slug.Valor, Alias);
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
        // conta como mudança e gera entrada de histórico (CA-04).
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

    private static Result ValidarCampos(
        string nome,
        string? alias,
        string sigla,
        string codigo,
        TipoUnidade tipo,
        OrigemUnidade origem,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        if (!Enum.IsDefined(tipo) || tipo == TipoUnidade.Nenhum)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.TipoInvalido,
                "Tipo de Unidade inválido — use um valor definido em TipoUnidade, diferente de Nenhum."));
        }

        if (!Enum.IsDefined(origem) || origem == OrigemUnidade.Nenhum)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.OrigemInvalida,
                "Origem da Unidade inválida."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.NomeObrigatorio,
                "Nome da Unidade é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.NomeTamanho,
                $"Nome da Unidade deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(sigla))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.SiglaObrigatoria,
                "Sigla da Unidade é obrigatória."));
        }

        if (sigla.Trim().Length is < SiglaMinLength or > SiglaMaxLength)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.SiglaTamanho,
                $"Sigla da Unidade deve ter entre {SiglaMinLength} e {SiglaMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.CodigoObrigatorio,
                "Código da Unidade é obrigatório."));
        }

        if (codigo.Trim().Length is < CodigoMinLength or > CodigoMaxLength)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.CodigoTamanho,
                $"Código da Unidade deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres."));
        }

        if (alias is not null && alias.Trim().Length > AliasMaxLength)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.AliasTamanho,
                $"Alias da Unidade não pode ter mais que {AliasMaxLength} caracteres."));
        }

        if (vigenciaFim.HasValue && vigenciaFim.Value < vigenciaInicio)
        {
            return Result.Failure(new DomainError(
                UnidadeErrorCodes.VigenciaFimAnteriorAoInicio,
                "Data de encerramento da vigência deve ser igual ou posterior à data de início."));
        }

        return Result.Success();
    }
}
