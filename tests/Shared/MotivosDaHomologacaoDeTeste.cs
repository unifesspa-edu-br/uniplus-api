namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os motivos que um processo de teste escolhe para cobrir a decisão sobre a homologação das inscrições: em cada fase que
/// declara o tipo de decisão <c>HOMOLOGACAO_DAS_INSCRICOES</c> e tem a regra de homologação, um motivo desfavorável para cada
/// código de motivo que as condições da regra apontam. O processo sem essa fase não escolhe nenhum.
/// </summary>
internal static class MotivosDaHomologacaoDeTeste
{
    /// <summary>A escolha que cobre o processo na versão que nasce com a publicação.</summary>
    public static IEnumerable<MotivoEscolhidoComOrigem> Escolha(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        return
        [
            .. MotivosExigidos(processo).Select(exigido =>
            {
                MotivoDeDecisao origem = NovaOrigem(exigido.Codigo);
                return new MotivoEscolhidoComOrigem(Escolher(processo, exigido.Fase, origem), origem);
            }),
        ];
    }

    /// <summary>O código de cada motivo desfavorável que as condições da regra apontam, com a fase que homologa e o exige.</summary>
    public static IEnumerable<(FaseCronograma Fase, string Codigo)> MotivosExigidos(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        return
        [
            .. processo.CronogramaFases
                .Where(static fase => fase.RegraDeHomologacao is not null && fase.TiposDeDecisao.Contains(TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes))
                .SelectMany(fase => CodigosDosMotivosDasCondicoes(fase).Select(codigo => (fase, codigo))),
        ];
    }

    /// <summary>O motivo do cadastro que a condição aponta, como o cadastro o guardaria.</summary>
    public static MotivoDeDecisao NovaOrigem(string codigo) => MotivoDeDecisao.Criar(new CamposDoMotivo(
        TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes, codigo, "Condição de homologação não atendida", null, SentidoDoMotivo.Desfavoravel)).Value!;

    /// <summary>A cópia do motivo do cadastro que o processo escolhe na fase.</summary>
    public static MotivoDoProcesso Escolher(ProcessoSeletivo processo, FaseCronograma fase, MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(processo, LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId), [], origem)).Value!;

    private static IEnumerable<string> CodigosDosMotivosDasCondicoes(FaseCronograma fase) =>
        fase.RegraDeHomologacao!.Alternativas
            .SelectMany(static alternativa => alternativa.Condicoes)
            .Select(static condicao => condicao.MotivoDesfavoravelCodigo)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);
}
