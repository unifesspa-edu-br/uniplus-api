namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fada;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O Fator de Atenuação das Desigualdades de Acesso (FADA) (UNI-REQ-0015): aplicar exige a
/// configuração, não aplicar a recusa, e a área nova não pode deixar de fora um município que uma
/// condição viva ainda cita.
/// </summary>
internal static class RegrasDoFada
{
    internal static DomainError? Recusa(ConfiguracaoDeclarada configuracao, bool aplica, ConfiguracaoDoFada? configuracaoDoFada) =>
        RecusaDaConfiguracao(aplica, configuracaoDoFada)
        ?? (MunicipioCitadoForaDaArea(configuracao, configuracaoDoFada)
            ? new DomainError(
                "ProcessoSeletivo.MunicipioDoFadaReferenciadoPorCondicaoViva",
                "Existe condição citando um município que deixaria a área do Fator de Atenuação das Desigualdades de Acesso (FADA) — ajuste ou remova a condição antes de redefinir o FADA.")
            : null);

    private static DomainError? RecusaDaConfiguracao(bool aplica, ConfiguracaoDoFada? configuracaoDoFada) =>
        DeclaracaoDoFada.Recusa(aplica, configuracaoDoFada is not null);

    private static bool MunicipioCitadoForaDaArea(ConfiguracaoDeclarada configuracao, ConfiguracaoDoFada? configuracaoDoFada)
    {
        HashSet<string> novosMunicipios = [.. (configuracaoDoFada?.Municipios ?? []).Select(static m => m.CodigoIbge)];
        return configuracao.Coleta.Campos
            .Where(static f => f.OrigemValores == OrigemValoresColeta.MunicipiosDoFada)
            .Any(f => ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(configuracao, f.FatoCodigo, novosMunicipios));
    }
}
