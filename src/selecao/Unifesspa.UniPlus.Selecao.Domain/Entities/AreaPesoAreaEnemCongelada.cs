namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma área de conhecimento do ENEM dentro de um grupo do quadro de pesos por área
/// congelado na classificação (<see cref="GrupoPesoAreaEnemCongelado"/>): código, rótulo,
/// peso e corte copiados por valor do cadastro de Pesos por Área no momento em que a
/// classificação foi definida.
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="ConfiguracaoBonusRegionalMunicipio"/>: filho substituível por inteiro junto
/// com a classificação que o congelou. A validação é do grupo, que conhece as áreas irmãs.
/// </remarks>
public sealed class AreaPesoAreaEnemCongelada : EntityBase
{
    public Guid GrupoPesoAreaEnemCongeladoId { get; private set; }

    /// <summary>Código da área, sem abreviação e sem acento.</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Rótulo oficial da área.</summary>
    public string Rotulo { get; private set; } = string.Empty;

    /// <summary>Peso da área na média do grupo.</summary>
    public decimal Peso { get; private set; }

    /// <summary>Nota mínima da área, ou <see langword="null"/> quando a área não tem corte.</summary>
    public decimal? Corte { get; private set; }

    private AreaPesoAreaEnemCongelada() { }

    internal static AreaPesoAreaEnemCongelada Criar(string codigo, string rotulo, decimal peso, decimal? corte) =>
        new()
        {
            Codigo = codigo,
            Rotulo = rotulo,
            Peso = peso,
            Corte = corte,
        };

    internal void VincularGrupo(Guid grupoPesoAreaEnemCongeladoId) =>
        GrupoPesoAreaEnemCongeladoId = grupoPesoAreaEnemCongeladoId;
}
