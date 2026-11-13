namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Onde um evento de manifestação técnica está, derivado na leitura do histórico do item (ADR-0163): nada é gravado
/// sobre o evento depois do registro dele.
/// </summary>
public enum SituacaoDaManifestacao
{
    /// <summary>Sentinela — situação não derivada.</summary>
    Nenhuma = 0,

    /// <summary>Do ciclo corrente, e nenhum evento a substituiu: é a que a decisão leva em conta.</summary>
    Vigente,

    /// <summary>Outra manifestação da mesma pessoa, no mesmo ciclo, a substituiu.</summary>
    Substituida,

    /// <summary>De um ciclo encerrado pelo reinício do item, e nenhum evento a substituiu.</summary>
    Encerrada,
}
