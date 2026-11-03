namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bonus;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015): aplicar exige a
/// configuração, não aplicar a recusa, e a área nova não pode deixar de fora um município que uma
/// condição viva ainda cita.
/// </summary>
internal static class RegrasDoBonusRegional
{
    internal static DomainError? Recusa(EstadoDaConfiguracao estado, bool aplica, ConfiguracaoBonusRegional? bonus)
    {
        if (aplica && bonus is null)
        {
            return new DomainError(
                ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao,
                "O processo que aplica o bônus regional precisa da configuração do bônus.");
        }

        if (!aplica && bonus is not null)
        {
            return new DomainError(
                ProcessoSeletivoErrorCodes.BonusRegionalNaoAplicaComConfiguracao,
                "O processo que não aplica o bônus regional não pode ter a configuração do bônus.");
        }

        return MunicipioCitadoForaDaArea(estado, bonus)
            ? new DomainError(
                "ProcessoSeletivo.MunicipioDoBonusReferenciadoPorCondicaoViva",
                "Existe condição citando um município que deixaria a área do bônus regional — ajuste ou remova a condição antes de redefinir o bônus.")
            : null;
    }

    private static bool MunicipioCitadoForaDaArea(EstadoDaConfiguracao estado, ConfiguracaoBonusRegional? bonus)
    {
        HashSet<string> novosMunicipios = [.. (bonus?.Municipios ?? []).Select(static m => m.CodigoIbge)];
        return estado.Campos
            .Where(static f => f.OrigemValores == OrigemValoresColeta.MunicipiosDoBonus)
            .Any(f => ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, f.FatoCodigo, novosMunicipios));
    }
}
