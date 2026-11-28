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
    /// O que a configuração declara, inclusive a primeira fase depois da importação, que fica sem declarar
    /// enquanto a declaração da importação do resultado não existe.
    /// </summary>
    internal static ProcessoDosFormularios De(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return new(
            configuracao.Identidade.OrigemCandidatos == OrigemCandidatos.InscricaoPropria,
            configuracao.DivulgacaoETaxa.ConfiguracaoTaxaInscricao?.Cobra == true,
            configuracao.ImportacaoDoResultado?.PrimeiraFaseDepoisDaImportacaoId);
    }
}
