namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o processo diz de si às regras dos formulários (UNI-REQ-0144): se a inscrição é pelo sistema ou o
/// resultado é importado, se cobra taxa e, no resultado importado, a primeira fase depois da importação.
/// </summary>
public sealed record ProcessoDosFormularios(bool InscricaoPeloSistema, bool CobraTaxa, Guid? PrimeiraFaseDepoisDaImportacaoId)
{
    /// <summary>
    /// O que a configuração declara. A importação do resultado não faz parte dela, e por isso a primeira fase
    /// depois da importação fica sem declarar.
    /// </summary>
    internal static ProcessoDosFormularios De(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return new(
            configuracao.Identidade.OrigemCandidatos == OrigemCandidatos.InscricaoPropria,
            configuracao.DivulgacaoETaxa.ConfiguracaoTaxaInscricao?.Cobra == true,
            null);
    }
}
