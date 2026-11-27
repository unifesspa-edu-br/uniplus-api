namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um grupo de área do ENEM no quadro de pesos por área congelado na classificação: o
/// grupo (código e rótulo), a base legal da linha e o peso e o corte de cada área, copiados
/// por valor da resolução de Pesos por Área que o processo declarou (ADR-0061). Editar o
/// cadastro depois não alcança o processo; quem quiser os valores novos redefine a
/// classificação.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EntityBase"/> puro, filho de <see cref="ConfiguracaoClassificacao"/>,
/// substituído por inteiro junto com ela.
/// </para>
/// <para>
/// O caminho de comando entrega valores que o cadastro de origem já validou. As checagens
/// daqui existem para o decodificador do envelope, que reconstrói o grupo a partir de bytes
/// e não pode aceitar um documento adulterado que só falharia no <c>SaveChanges</c>.
/// </para>
/// </remarks>
public sealed class GrupoPesoAreaEnemCongelado : EntityBase
{
    /// <summary>Tamanho máximo da base legal congelada — o mesmo da coluna do cadastro de origem.</summary>
    public const int BaseLegalMaxLength = 500;

    /// <summary>Tamanho máximo do código de área congelado — o mesmo da coluna do cadastro de origem.</summary>
    public const int AreaCodigoMaxLength = 30;

    /// <summary>Tamanho máximo do rótulo de área congelado — o mesmo da coluna do cadastro de origem.</summary>
    public const int AreaRotuloMaxLength = 100;

    /// <summary>Nota máxima de uma área do ENEM, teto do corte.</summary>
    public const decimal CorteMaximo = 1000m;

    /// <summary>Casas decimais do peso e do corte, no banco, no documento congelado e na exportação (<c>multipleOf</c> 0,0001).</summary>
    public const int EscalaDoPesoEDoCorte = 4;

    private readonly List<AreaPesoAreaEnemCongelada> _areas = [];

    public Guid ConfiguracaoClassificacaoId { get; private set; }

    /// <summary>O grupo de área, com código e rótulo.</summary>
    public GrupoAreaEnemSnapshot GrupoAreaEnem { get; private set; } = null!;

    /// <summary>Dispositivo legal que fundamenta os pesos do grupo.</summary>
    public string BaseLegal { get; private set; } = string.Empty;

    /// <summary>As áreas do grupo, com peso e corte.</summary>
    public IReadOnlyList<AreaPesoAreaEnemCongelada> Areas => _areas.AsReadOnly();

    private GrupoPesoAreaEnemCongelado() { }

    /// <summary>
    /// Congela um grupo do quadro. Acumula toda violação independente (ADR-0125). Código,
    /// rótulo e base legal entram aparados e em NFC, a forma com que o envelope canônico os
    /// grava, para que um ciclo de retificação não os transforme em outro valor.
    /// </summary>
    public static Result<GrupoPesoAreaEnemCongelado> Criar(
        string? grupoCodigo,
        string? grupoRotulo,
        string? baseLegal,
        IEnumerable<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        List<FieldError> erros = [];
        Result<GrupoAreaEnemSnapshot> grupo = ValidarGrupo(grupoCodigo, grupoRotulo, erros);
        string? baseLegalNormalizada = ValidarBaseLegal(baseLegal, erros);
        List<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> areasValidas = ValidarAreas(areas, erros);
        if (erros.Count > 0)
        {
            return Result<GrupoPesoAreaEnemCongelado>.ValidationFailure(erros);
        }
        GrupoPesoAreaEnemCongelado congelado = new() { GrupoAreaEnem = grupo.Value!, BaseLegal = baseLegalNormalizada! };
        congelado.AdicionarAreas(areasValidas);
        return Result<GrupoPesoAreaEnemCongelado>.Success(congelado);
    }

    private static Result<GrupoAreaEnemSnapshot> ValidarGrupo(string? codigo, string? rotulo, List<FieldError> erros)
    {
        Result<GrupoAreaEnemSnapshot> grupo = GrupoAreaEnemSnapshot.Criar(codigo, rotulo);
        if (grupo.IsFailure)
        {
            erros.Add(new("grupoAreaEnem", grupo.Error!));
        }
        return grupo;
    }

    private static string? ValidarBaseLegal(string? baseLegal, List<FieldError> erros)
    {
        string? normalizada = string.IsNullOrWhiteSpace(baseLegal) ? null : TextoCongelado.Normalizar(baseLegal);
        DomainError? recusa = RecusaDaBaseLegal(baseLegal, normalizada);
        if (recusa is not null)
        {
            erros.Add(new("baseLegal", recusa));
        }
        return normalizada;
    }

    private static DomainError? RecusaDaBaseLegal(string? original, string? normalizada) =>
        string.IsNullOrWhiteSpace(original)
            ? new("GrupoPesoAreaEnemCongelado.BaseLegalObrigatoria",
                "A base legal do grupo no quadro de pesos por área é obrigatória.")
            : normalizada is null || !TextoCongelado.CabeNaColuna(normalizada, BaseLegalMaxLength)
                ? new("GrupoPesoAreaEnemCongelado.BaseLegalInvalida",
                    $"A base legal do grupo no quadro de pesos por área deve ter até {BaseLegalMaxLength} caracteres, sem o caractere nulo nem caractere que não seja texto.")
                : null;

    private static List<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> ValidarAreas(
        IEnumerable<(string? Codigo, string? Rotulo, decimal Peso, decimal? Corte)> areas, List<FieldError> erros)
    {
        List<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> validas = [];
        HashSet<string> codigosVistos = new(StringComparer.Ordinal);
        int indice = 0;
        foreach ((string? Codigo, string? Rotulo, decimal Peso, decimal? Corte) area in areas)
        {
            ValidarArea(area, indice++, codigosVistos, validas, erros);
        }
        DomainError? recusa = RecusaDoConjuntoDeAreas(indice, validas);
        if (recusa is not null)
        {
            erros.Add(new("areas", recusa));
        }
        return validas;
    }

    private static void ValidarArea(
        (string? Codigo, string? Rotulo, decimal Peso, decimal? Corte) area, int indice,
        HashSet<string> codigosVistos, List<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> validas,
        List<FieldError> erros)
    {
        string campo = $"areas[{indice}]";
        string? codigo = TextoCongelado.NormalizarOuNulo(area.Codigo);
        string? rotulo = TextoCongelado.NormalizarOuNulo(area.Rotulo);
        DomainError? recusa = RecusaDaIdentificacao(codigo, rotulo, codigosVistos);
        if (recusa is not null)
        {
            erros.Add(new(campo, recusa));
        }
        AdicionarRecusasDasMedidas(area.Peso, area.Corte, rotulo, campo, erros);
        if (recusa is null)
        {
            validas.Add((codigo!, rotulo!, area.Peso, area.Corte));
        }
    }

    private static DomainError? RecusaDaIdentificacao(string? codigo, string? rotulo, HashSet<string> codigosVistos)
    {
        if (!CodigoDeAreaValido(codigo) || !RotuloDeAreaValido(rotulo))
        {
            return new("GrupoPesoAreaEnemCongelado.AreaInvalida",
                $"Cada área do quadro de pesos por área precisa de código (até {AreaCodigoMaxLength} caracteres) e rótulo (até {AreaRotuloMaxLength} caracteres), sem o caractere nulo nem caractere que não seja texto.");
        }
        return !codigosVistos.Add(codigo!)
            ? new("GrupoPesoAreaEnemCongelado.AreaRepetida",
                $"A área {CaracteresInvisiveis.ParaEco(rotulo!, TextoCongelado.TamanhoMaximoEcoado)} aparece mais de uma vez no mesmo grupo do quadro de pesos por área.")
            : null;
    }

    private static bool CodigoDeAreaValido(string? codigo) =>
        codigo is not null && TextoCongelado.CabeNaColuna(codigo, AreaCodigoMaxLength);

    private static bool RotuloDeAreaValido(string? rotulo) =>
        rotulo is not null && TextoCongelado.CabeNaColuna(rotulo, AreaRotuloMaxLength);

    private static void AdicionarRecusasDasMedidas(decimal peso, decimal? corte, string? rotulo, string campo, List<FieldError> erros)
    {
        string daArea = RotuloDeAreaValido(rotulo)
            ? $"da área {CaracteresInvisiveis.ParaEco(rotulo!, TextoCongelado.TamanhoMaximoEcoado)}"
            : "da área";
        DomainError? recusaPeso = RecusaDoPeso(peso, daArea);
        DomainError? recusaCorte = RecusaDoCorte(corte, daArea);
        if (recusaPeso is not null)
        {
            erros.Add(new($"{campo}.peso", recusaPeso));
        }
        if (recusaCorte is not null)
        {
            erros.Add(new($"{campo}.corte", recusaCorte));
        }
    }

    private static DomainError? RecusaDoPeso(decimal peso, string daArea) => peso < 0
        ? new("GrupoPesoAreaEnemCongelado.PesoNegativo", $"O peso {daArea} não pode ser negativo.")
        : null;

    private static DomainError? RecusaDoCorte(decimal? corte, string daArea) => corte is < 0 or > CorteMaximo
        ? new("GrupoPesoAreaEnemCongelado.CorteForaDaFaixa", $"O corte {daArea} deve estar entre 0 e {CorteMaximo}.")
        : null;

    private static DomainError? RecusaDoConjuntoDeAreas(
        int quantidade, List<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> validas) =>
        quantidade == 0
            ? new("GrupoPesoAreaEnemCongelado.SemAreas", "Cada grupo do quadro de pesos por área precisa de ao menos uma área.")
            : validas.Count == quantidade && validas.All(static a => a.Peso == 0m)
                ? new("GrupoPesoAreaEnemCongelado.PesosSomamZero", "Os pesos das áreas do grupo do quadro de pesos por área não podem somar zero.")
                : null;

    private void AdicionarAreas(IEnumerable<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> areas)
    {
        foreach ((string codigo, string rotulo, decimal peso, decimal? corte) in areas)
        {
            AreaPesoAreaEnemCongelada area = AreaPesoAreaEnemCongelada.Criar(codigo, rotulo, peso, corte);
            area.VincularGrupo(Id);
            _areas.Add(area);
        }
    }

    internal void VincularConfiguracao(Guid configuracaoClassificacaoId) =>
        ConfiguracaoClassificacaoId = configuracaoClassificacaoId;
}
