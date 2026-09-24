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

    private const int TamanhoMaximoDoRotuloEcoado = 40;

    /// <summary>Nota máxima de uma área do ENEM, teto do corte.</summary>
    public const decimal CorteMaximo = 1000m;

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

        Result<GrupoAreaEnemSnapshot> grupo = GrupoAreaEnemSnapshot.Criar(grupoCodigo, grupoRotulo);
        if (grupo.IsFailure)
        {
            erros.Add(new("grupoAreaEnem", grupo.Error!));
        }

        string? baseLegalNormalizada = null;
        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            erros.Add(new("baseLegal", new DomainError(
                "GrupoPesoAreaEnemCongelado.BaseLegalObrigatoria",
                "A base legal do grupo no quadro de pesos por área é obrigatória.")));
        }
        else
        {
            baseLegalNormalizada = TextoCongelado.Normalizar(baseLegal);
            if (baseLegalNormalizada is null || !TextoCongelado.CabeNaColuna(baseLegalNormalizada, BaseLegalMaxLength))
            {
                erros.Add(new("baseLegal", new DomainError(
                    "GrupoPesoAreaEnemCongelado.BaseLegalInvalida",
                    $"A base legal do grupo no quadro de pesos por área deve ter até {BaseLegalMaxLength} caracteres, sem o caractere nulo nem caractere que não seja texto.")));
            }
        }

        List<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> areasValidas = [];
        HashSet<string> codigosVistos = new(StringComparer.Ordinal);
        int indice = 0;
        foreach ((string? codigo, string? rotulo, decimal peso, decimal? corte) in areas)
        {
            string campo = $"areas[{indice}]";
            indice++;

            string? codigoNormalizado = TextoCongelado.NormalizarOuNulo(codigo);
            string? rotuloNormalizado = TextoCongelado.NormalizarOuNulo(rotulo);
            bool identificada = codigoNormalizado is not null && TextoCongelado.CabeNaColuna(codigoNormalizado, AreaCodigoMaxLength)
                && rotuloNormalizado is not null && TextoCongelado.CabeNaColuna(rotuloNormalizado, AreaRotuloMaxLength);
            if (!identificada)
            {
                erros.Add(new(campo, new DomainError(
                    "GrupoPesoAreaEnemCongelado.AreaInvalida",
                    $"Cada área do quadro de pesos por área precisa de código (até {AreaCodigoMaxLength} caracteres) e rótulo (até {AreaRotuloMaxLength} caracteres), sem o caractere nulo nem caractere que não seja texto.")));
            }
            else if (!codigosVistos.Add(codigoNormalizado!))
            {
                erros.Add(new(campo, new DomainError(
                    "GrupoPesoAreaEnemCongelado.AreaRepetida",
                    $"A área {CaracteresInvisiveis.ParaEco(rotuloNormalizado!, TamanhoMaximoDoRotuloEcoado)} aparece mais de uma vez no mesmo grupo do quadro de pesos por área.")));
                identificada = false;
            }

            // O rótulo volta na mensagem só quando não é ele o motivo da recusa, e limitado e sem
            // caractere invisível: o texto volta na resposta e nos logs.
            string daArea = rotuloNormalizado is not null && TextoCongelado.CabeNaColuna(rotuloNormalizado, AreaRotuloMaxLength)
                ? $"da área {CaracteresInvisiveis.ParaEco(rotuloNormalizado, TamanhoMaximoDoRotuloEcoado)}"
                : "da área";
            if (peso < 0)
            {
                erros.Add(new($"{campo}.peso", new DomainError(
                    "GrupoPesoAreaEnemCongelado.PesoNegativo",
                    $"O peso {daArea} não pode ser negativo.")));
            }

            if (corte is < 0 or > CorteMaximo)
            {
                erros.Add(new($"{campo}.corte", new DomainError(
                    "GrupoPesoAreaEnemCongelado.CorteForaDaFaixa",
                    $"O corte {daArea} deve estar entre 0 e {CorteMaximo}.")));
            }

            if (identificada)
            {
                areasValidas.Add((codigoNormalizado!, rotuloNormalizado!, peso, corte));
            }
        }

        if (indice == 0)
        {
            erros.Add(new("areas", new DomainError(
                "GrupoPesoAreaEnemCongelado.SemAreas",
                "Cada grupo do quadro de pesos por área precisa de ao menos uma área.")));
        }

        if (erros.Count > 0)
        {
            return Result<GrupoPesoAreaEnemCongelado>.ValidationFailure(erros);
        }

        GrupoPesoAreaEnemCongelado congelado = new()
        {
            GrupoAreaEnem = grupo.Value!,
            BaseLegal = baseLegalNormalizada!,
        };

        foreach ((string codigo, string rotulo, decimal peso, decimal? corte) in areasValidas)
        {
            AreaPesoAreaEnemCongelada area = AreaPesoAreaEnemCongelada.Criar(codigo, rotulo, peso, corte);
            area.VincularGrupo(congelado.Id);
            congelado._areas.Add(area);
        }

        return Result<GrupoPesoAreaEnemCongelado>.Success(congelado);
    }

    internal void VincularConfiguracao(Guid configuracaoClassificacaoId) =>
        ConfiguracaoClassificacaoId = configuracaoClassificacaoId;
}
