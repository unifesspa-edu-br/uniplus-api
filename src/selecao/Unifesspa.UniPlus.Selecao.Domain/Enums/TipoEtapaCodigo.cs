namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos do cadastro de tipos de etapa (módulo Configuração) aos quais o
/// Seleção atribui comportamento próprio. O cadastro é aberto — novos tipos
/// entram sem código aqui —; só o tipo cujo código muda uma regra do agregado
/// é nomeado.
/// </summary>
public static class TipoEtapaCodigo
{
    /// <summary>
    /// Etapa cuja nota tem origem no ENEM: não é lançada por banca, é composta
    /// pelo motor de classificação a partir das áreas do candidato, dos pesos
    /// congelados e do grupo de área da oferta.
    /// </summary>
    public const string NotaEnem = "NOTA_ENEM";
}
