namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A etapa de tipo que exige correção independente, uma causa por etapa, só é congelada com a regra de consolidação de ao menos dois
/// corretores distintos. O número vem da versão da regra no rol, lido ao congelar, e não do que a etapa declara: o rol
/// pode trazer uma versão com um só corretor. A regra cujo número não foi lido é recusada, e nenhum número é presumido.
/// </summary>
internal static class PendenciasDaCorrecaoIndependente
{
    private const int MinimoDeCorretores = 2;

    internal static IReadOnlyList<FieldError> CausasDaCorrecaoIndependente(EstadoDaConfiguracao estado, CorretoresDasEtapas corretores)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(corretores);

        return CausasDaRecusa.Da(estado.Etapas
            .Where(static etapa => etapa.TipoEtapa.ExigeCorrecaoIndependente && etapa.RegraConsolidacao is not null)
            .Select(etapa => Pendencia(etapa, corretores))
            .OfType<DomainError>());
    }

    private static DomainError? Pendencia(EtapaProcesso etapa, CorretoresDasEtapas corretores)
    {
        if (!corretores.Resolvida(etapa))
        {
            return new DomainError(
                EtapaProcessoErrorCodes.RegraDeConsolidacaoNaoResolvida,
                $"A regra de consolidação da etapa '{Eco(etapa)}' não pôde ser confirmada no catálogo, e a quantidade de corretores da correção independente não é conhecida. Tente novamente ou fale com a equipe de suporte.");
        }

        return corretores.Distintos(etapa) >= MinimoDeCorretores
            ? null
            : new DomainError(
                EtapaProcessoErrorCodes.CorrecaoIndependenteComMenosDeDoisCorretores,
                $"O tipo da etapa '{Eco(etapa)}' exige correção independente, e a regra de consolidação dela tem {corretores.Distintos(etapa)} corretor: use uma regra de ao menos {MinimoDeCorretores} corretores distintos.");
    }

    private static string Eco(EtapaProcesso etapa) => CaracteresInvisiveis.ParaEco(etapa.Nome, TextoCongelado.TamanhoMaximoEcoado);
}
