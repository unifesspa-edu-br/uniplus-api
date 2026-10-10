namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Bônus regional do <see cref="ProcessoSeletivo"/> (RN05, Story #774):
/// referencia a regra tipada <c>BONUS-MULTIPLICATIVO</c>
/// do <c>rol_de_regras</c> e seus args (<see cref="Fator"/>, <see cref="Teto"/>).
/// </summary>
/// <remarks>
/// <strong>Declaração e configuração (RN05):</strong> não existe "BONUS-NENHUM". O processo
/// declara se aplica o bônus (<see cref="ProcessoSeletivo.AplicaBonusRegional"/>): esta entidade
/// só existe quando a declaração é verdadeira, e é a sua presença, com a regra tipada, que
/// habilita o bônus. A ausência da entidade só significa "sem bônus" quando a declaração é falsa;
/// com a declaração nula, significa "por declarar" e bloqueia a publicação. O bônus se aplica
/// sobre a nota final, após os pesos (decisão do P.O.: multiplicativo, ex. ×1,20, sem teto).
/// <para>
/// <strong>Elegibilidade (ADR-0142):</strong> quem recebe o Fator de Atenuação das Desigualdades
/// de Acesso é decidido pelo fato booleano que a configuração cita
/// (<see cref="FatoElegibilidadeCodigo"/>): o motor aplica o acréscimo ao candidato em quem esse
/// fato resolve verdadeiro. O fato é do processo — coletado no formulário ou derivado por regra —,
/// nunca um código fixo no sistema. Com inscrição própria, a citação é exigida para publicar; com
/// resultado importado, a classificação já vem pronta e não há o que citar.
/// </para>
/// </remarks>
public sealed class ConfiguracaoBonusRegional : EntityBase
{
    private readonly List<ConfiguracaoBonusRegionalMunicipio> _municipios = [];

    public Guid ProcessoSeletivoId { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public decimal Fator { get; private set; }
    public decimal? Teto { get; private set; }

    /// <summary>O fato booleano do processo que decide quem recebe o acréscimo (ADR-0142).</summary>
    public string? FatoElegibilidadeCodigo { get; private set; }

    /// <summary>Id do cadastro de Base Legal de Bônus Regional (Configuração, Story #1465) referenciado.</summary>
    public Guid BaseLegalBonusRegionalId { get; private set; }

    /// <summary>Snapshot congelado no momento da gravação — código canônico UPPER_SNAKE (Story #1464).</summary>
    public string TipoInstrumento { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado da identificação da Base Legal (ex.: "Portaria Unifesspa nº 2514/2023").</summary>
    public string Identificacao { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado da descrição da Base Legal.</summary>
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Snapshot congelado dos municípios abrangidos pela Base Legal no momento da gravação.</summary>
    public IReadOnlyList<ConfiguracaoBonusRegionalMunicipio> Municipios => _municipios.AsReadOnly();

    private ConfiguracaoBonusRegional() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o array
    /// <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as regras violadas no
    /// mesmo lote. Os campos de snapshot (<see cref="TipoInstrumento"/>, <see cref="Identificacao"/>,
    /// <see cref="Descricao"/>, <see cref="Municipios"/>) não são revalidados aqui: chegam já
    /// congelados de um <c>BaseLegalBonusRegionalView</c> resolvido e validado pelo handler
    /// (Story #1465) — revalidar duplicaria a fonte de verdade do cadastro de origem.
    /// </summary>
    public static Result<ConfiguracaoBonusRegional> Criar(
        ReferenciaRegra regra,
        decimal fator,
        decimal? teto,
        string? fatoElegibilidadeCodigo,
        Guid baseLegalBonusRegionalId,
        string tipoInstrumento,
        string identificacao,
        string descricao,
        IEnumerable<(string CodigoIbge, string Nome, string Uf)> municipios)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(municipios);

        List<FieldError> erros = [];
        List<(string CodigoIbge, string Nome, string Uf)> municipiosLista = [.. municipios];

        if (regra.Codigo != RegraBonusCodigo.Multiplicativo)
        {
            erros.Add(new("regraCodigo", new DomainError(
                "ConfiguracaoBonusRegional.RegraInvalida",
                $"A regra do bônus precisa ser do código {RegraBonusCodigo.Multiplicativo}.")));
        }

        if (fator <= 0)
        {
            erros.Add(new("fator", new DomainError(
                "ConfiguracaoBonusRegional.FatorInvalido", "O fator do bônus deve ser maior que zero.")));
        }

        if (teto is <= 0)
        {
            erros.Add(new("teto", new DomainError(
                "ConfiguracaoBonusRegional.TetoInvalido", "O teto do bônus, quando informado, deve ser maior que zero.")));
        }

        string? fatoElegibilidade = fatoElegibilidadeCodigo?.Trim();
        if (fatoElegibilidade is { Length: 0 } || fatoElegibilidade?.Length > FormaDoItem.FatoCodigoMaxLength)
        {
            erros.Add(new("fatoElegibilidadeCodigo", new DomainError(
                "ConfiguracaoBonusRegional.FatoElegibilidadeInvalido",
                $"O código do fato que decide quem recebe o acréscimo, quando citado, tem de 1 a {FormaDoItem.FatoCodigoMaxLength} caracteres.")));
        }

        // Defesa em profundidade: o handler já resolve municipiosLista a partir de um
        // BaseLegalBonusRegionalView que a Story #1465 garante não-vazio, mas o decoder do
        // envelope (Story #1466) reconstrói ConfiguracaoBonusRegional a partir de bytes —
        // e precisa da MESMA invariante para recusar um envelope adulterado com
        // municipios:[] em vez de aceitá-lo como bônus válido.
        if (municipiosLista.Count == 0)
        {
            erros.Add(new(null, new DomainError(
                "ConfiguracaoBonusRegional.SemMunicipios",
                "A base legal do bônus regional deve abranger pelo menos um município.")));
        }

        if (erros.Count > 0)
        {
            return Result<ConfiguracaoBonusRegional>.ValidationFailure(erros);
        }

        ConfiguracaoBonusRegional bonus = new()
        {
            Regra = regra,
            Fator = fator,
            Teto = teto,
            FatoElegibilidadeCodigo = fatoElegibilidade,
            BaseLegalBonusRegionalId = baseLegalBonusRegionalId,
            TipoInstrumento = tipoInstrumento,
            Identificacao = identificacao,
            Descricao = descricao,
        };

        foreach ((string codigoIbge, string nome, string uf) in municipiosLista)
        {
            ConfiguracaoBonusRegionalMunicipio municipio = ConfiguracaoBonusRegionalMunicipio.Criar(codigoIbge, nome, uf);
            municipio.VincularConfiguracao(bonus.Id);
            bonus._municipios.Add(municipio);
        }

        return Result<ConfiguracaoBonusRegional>.Success(bonus);
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
