namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

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
/// data de abertura da nova vigência é a <see cref="MudancaDeIdentificadores.Data"/>
/// passada pelo handler. O domínio não consulta relógio diretamente.</para>
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
    /// Cria uma nova Unidade com identidade rica. A unicidade de Slug/Sigla/Codigo e a
    /// detecção de ciclo na hierarquia são do handler chamador; a factory valida só
    /// formato e domínio local.
    /// </summary>
    public static Result<Unidade> Criar(
        string? nome,
        IdentificadoresDaUnidade identificadores,
        CidadeDaUnidade cidade,
        VigenciaDaUnidade vigencia,
        Guid? unidadeSuperiorId,
        TipoUnidade tipo,
        bool unidadeAcademica)
    {
        Result validacao = ValidarCampos(nome, identificadores, cidade, tipo, vigencia);
        if (validacao.IsFailure)
        {
            return Result<Unidade>.ValidationFailure(validacao.Errors);
        }

        var unidade = new Unidade { VigenciaInicio = vigencia.Inicio };
        unidade.AplicarAtributos(nome, identificadores.Normalizar(), unidadeSuperiorId, tipo, unidadeAcademica, vigencia.Fim);
        unidade.AplicarCidade(cidade);
        unidade.AbrirHistoricoInicial();

        return Result<Unidade>.Success(unidade);
    }

    /// <summary>
    /// Atualiza atributos da Unidade. Mudanças em Slug, Sigla, Codigo ou Alias
    /// encerram a vigência do valor anterior e abrem nova entrada de histórico.
    /// </summary>
    public Result Atualizar(
        string? nome,
        MudancaDeIdentificadores mudanca,
        CidadeDaUnidade cidade,
        DateOnly? vigenciaFim,
        Guid? unidadeSuperiorId,
        TipoUnidade tipo,
        bool unidadeAcademica)
    {
        Result validacao = ValidarCampos(
            nome, mudanca.Novos, cidade, tipo, new VigenciaDaUnidade(VigenciaInicio, vigenciaFim));
        Result motivo = UnidadeIdentificadorHistorico.ValidarMotivoMudanca(mudanca.Motivo);
        if (validacao.IsFailure || motivo.IsFailure)
        {
            return Result.ValidationFailure([.. validacao.Errors, .. motivo.Errors]);
        }

        IdentificadoresNormalizados novos = mudanca.Novos.Normalizar();
        RenomearIdentificadores(novos, mudanca.Data, mudanca.Motivo);
        AplicarAtributos(nome, novos, unidadeSuperiorId, tipo, unidadeAcademica, vigenciaFim);
        AplicarCidade(cidade);

        return Result.Success();
    }

    /// <summary>
    /// Valida todos os campos da Unidade, acumulando cada violação em vez de retornar na
    /// primeira (ADR-0125): o array <c>errors[]</c> do contrato público (ADR-0023) leva todas
    /// de uma vez. Público para os handlers validarem o payload inteiro antes de qualquer I/O,
    /// porque validação sempre vence 404 e as checagens de unicidade.
    /// </summary>
    public static Result ValidarCampos(
        string? nome,
        IdentificadoresDaUnidade identificadores,
        CidadeDaUnidade cidade,
        TipoUnidade tipo,
        VigenciaDaUnidade vigencia)
    {
        List<FieldError> erros =
        [
            .. ValidarTipo(tipo),
            .. ValidarNome(nome),
            .. identificadores.Validar(),
            .. vigencia.Validar(),
            .. cidade.Validar(),
        ];

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    private static IEnumerable<FieldError> ValidarTipo(TipoUnidade tipo)
    {
        if (!Enum.IsDefined(tipo) || tipo == TipoUnidade.Nenhum)
        {
            yield return new FieldError("tipo", new DomainError(
                UnidadeErrorCodes.TipoInvalido,
                "Tipo de Unidade inválido — use um valor definido em TipoUnidade, diferente de Nenhum."));
        }
    }

    private static IEnumerable<FieldError> ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            yield return new FieldError("nome", new DomainError(
                UnidadeErrorCodes.NomeObrigatorio, "Nome da Unidade é obrigatório."));
        }
        else if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            yield return new FieldError("nome", new DomainError(
                UnidadeErrorCodes.NomeTamanho,
                $"Nome da Unidade deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }
    }

    private void AplicarAtributos(
        string? nome,
        IdentificadoresNormalizados identificadores,
        Guid? unidadeSuperiorId,
        TipoUnidade tipo,
        bool unidadeAcademica,
        DateOnly? vigenciaFim)
    {
        Nome = nome?.Trim() ?? string.Empty;
        Alias = identificadores.Alias;
        Slug = identificadores.Slug;
        Sigla = identificadores.Sigla;
        Codigo = identificadores.Codigo;
        UnidadeSuperiorId = unidadeSuperiorId;
        Tipo = tipo;
        UnidadeAcademica = unidadeAcademica;
        VigenciaFim = vigenciaFim;
    }

    // A cidade já foi validada: informada, tem o trio completo; ausente, zera os três campos.
    private void AplicarCidade(CidadeDaUnidade cidade)
    {
        CidadeCodigoIbge = cidade.Informada ? cidade.CodigoIbge!.Trim() : null;
        CidadeNome = cidade.Informada ? cidade.Nome!.Trim() : null;
        CidadeUf = cidade.Informada ? cidade.Uf!.Trim().ToUpperInvariant() : null;
    }

    private void AbrirHistoricoInicial()
    {
        AbrirEntrada(TipoIdentificador.Slug, Slug.Valor, VigenciaInicio);
        AbrirEntrada(TipoIdentificador.Sigla, Sigla, VigenciaInicio);
        AbrirEntrada(TipoIdentificador.Codigo, Codigo, VigenciaInicio);
        if (Alias is not null)
        {
            AbrirEntrada(TipoIdentificador.Alias, Alias, VigenciaInicio);
        }
    }

    private void AbrirEntrada(TipoIdentificador tipo, string valor, DateOnly inicio, string? motivo = null) =>
        _historico.Add(UnidadeIdentificadorHistorico.Abrir(Id, tipo, valor, inicio, motivo?.Trim()));

    private void RenomearIdentificadores(IdentificadoresNormalizados novos, DateOnly data, string? motivo)
    {
        RenomearIdentificadorSeNecessario(TipoIdentificador.Slug, Slug.Valor, novos.Slug.Valor, data, motivo);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Sigla, Sigla, novos.Sigla, data, motivo);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Codigo, Codigo, novos.Codigo, data, motivo);
        RenomearIdentificadorSeNecessario(TipoIdentificador.Alias, Alias, novos.Alias, data, motivo);
    }

    private void FecharEntradaAberta(TipoIdentificador tipo, DateOnly data) =>
        _historico.FirstOrDefault(h => h.TipoIdentificador == tipo && h.VigenciaFim is null)?.FecharVigencia(data);

    private void RenomearIdentificadorSeNecessario(
        TipoIdentificador tipoIdentificador,
        string? valorAtual,
        string? novoValor,
        DateOnly dataAtual,
        string? motivo)
    {
        // Ordinal: slug e sigla chegam normalizados, mas Codigo e Alias preservam a caixa,
        // então ABC→abc conta como mudança.
        if (string.Equals(valorAtual, novoValor, StringComparison.Ordinal))
        {
            return;
        }

        FecharEntradaAberta(tipoIdentificador, dataAtual);

        // Alias pode ser removido: nesse caso só se encerra a entrada aberta.
        if (novoValor is not null)
        {
            AbrirEntrada(tipoIdentificador, novoValor, dataAtual, motivo);
        }
    }
}
