namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os motivos que um processo de teste escolhe para cobrir a decisão sobre a isenção da taxa: em cada fase ou etapa que
/// declara o tipo de decisão <c>ISENCAO_DA_TAXA</c>, um motivo favorável e um desfavorável para cada fundamento de isenção
/// habilitado. O processo sem fase nem etapa que declare o tipo, ou sem fundamento habilitado, não escolhe nenhum.
/// </summary>
internal static class MotivosDaIsencaoDeTeste
{
    private static readonly SentidoDoMotivo[] Sentidos = [SentidoDoMotivo.Favoravel, SentidoDoMotivo.Desfavoravel];

    /// <summary>A escolha que cobre o processo na versão que nasce com a publicação.</summary>
    public static IEnumerable<MotivoEscolhidoComOrigem> Escolha(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IReadOnlyList<string> fundamentos = [.. processo.ConfiguracaoTaxaInscricao?.Fundamentos.Select(static f => f.ToCodigo()) ?? []];

        return [.. LugaresQueDeclaramOTipo(processo).SelectMany(lugar => EscolherEm(processo, lugar, fundamentos))];
    }

    private static IEnumerable<LugarDaDecisao> LugaresQueDeclaramOTipo(ProcessoSeletivo processo) =>
    [
        .. processo.CronogramaFases.Where(static f => f.TiposDeDecisao.Contains(TipoMotivoDeDecisao.IsencaoDaTaxa)).Select(static f => LugarDaDecisao.DaFase(f.FaseCanonicaOrigemId)),
        .. processo.Etapas.Where(static e => e.TiposDeDecisao.Contains(TipoMotivoDeDecisao.IsencaoDaTaxa)).Select(static e => LugarDaDecisao.DaEtapa(e.Id)),
    ];

    private static IEnumerable<MotivoEscolhidoComOrigem> EscolherEm(ProcessoSeletivo processo, LugarDaDecisao lugar, IReadOnlyList<string> fundamentos) =>
        from fundamento in fundamentos
        from sentido in Sentidos
        let origem = OrigemDaIsencao(fundamento, sentido)
        select new MotivoEscolhidoComOrigem(Escolher(processo, lugar, origem), origem);

    private static MotivoDeDecisao OrigemDaIsencao(string fundamento, SentidoDoMotivo sentido) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo(), $"TEST_ISN_{Guid.NewGuid():N}"[..30].ToUpperInvariant(), "Situação conferida pela comissão", fundamento, sentido)).Value!;

    private static MotivoDoProcesso Escolher(ProcessoSeletivo processo, LugarDaDecisao lugar, MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(processo, lugar, [], origem)).Value!;
}
