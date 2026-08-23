namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuração do destino dedicado onde as decisões de acesso são registradas.
/// Validada no início da aplicação.
/// </summary>
public sealed class RegistroOperacionalRestritoOptions
{
    /// <summary>Seção de configuração correspondente.</summary>
    public const string SectionName = "Autorizacao:RegistroOperacional";

    /// <summary>Nome de serviço padrão do fluxo dedicado.</summary>
    public const string NomeServicoPadrao = "uniplus-autorizacao";

    /// <summary>
    /// Escreve o registro. Desligar é decisão de operação — a decisão de acesso
    /// continua sendo tomada e aplicada; apenas deixa de ser registrada. A
    /// composição também o desliga quando a observabilidade está desativada:
    /// sem coletor, o exportador só produziria ruído de conexão.
    /// </summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>
    /// <c>service.name</c> com que as decisões chegam ao coletor. É o que separa
    /// este fluxo do da aplicação e o que o backend de observabilidade usa para
    /// restringir quem o lê — por isso é um nome próprio, e não o do serviço.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string NomeServico { get; set; } = NomeServicoPadrao;
}
