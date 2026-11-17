namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere o tipo de decisão e o motivo desfavorável de cada condição da homologação. A regra que a leitura recusou inteira
/// fica sem julgar, e o painel continua pendente enquanto houver parte recusada (<see cref="LeituraIncompleta"/>).
/// </summary>
public sealed class ExigenciaDaHomologacaoDasInscricoes : IExigenciaDeMotivos
{
    private const string CampoDaRegra = "regraDeHomologacao";
    private const TipoMotivoDeDecisao Tipo = TipoMotivoDeDecisao.HomologacaoDasInscricoes;

    public IEnumerable<FieldError> TiposNaoDeclarados(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(lugares);

        return FasesComRegra(configuracao, lugares)
            .Where(static fase => !fase.DeclaraOTipo && !fase.TiposComParteRecusada)
            .Select(static fase => TipoNaoDeclarado(fase.Fase, fase.Posicao));
    }

    public IEnumerable<FieldError> FaltasDeCobertura(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares, IReadOnlyCollection<MotivoDoProcesso> escolhidos)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(lugares);
        ArgumentNullException.ThrowIfNull(escolhidos);

        return FasesComRegra(configuracao, lugares)
            .Where(static fase => fase.DeclaraOTipo)
            .SelectMany(fase => FaltasDasCondicoes(fase.Fase, fase.Posicao, fase.Regra, escolhidos));
    }

    internal static bool LeituraIncompleta(ConfiguracaoDeclarada configuracao)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem);
        return cronograma.TemParteRecusadaNaListaDeFases || cronograma.Fases.Any(static fase => fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeHomologacao));
    }

    private static IEnumerable<FaseComRegra> FasesComRegra(ConfiguracaoDeclarada configuracao, LugaresDaDecisao lugares)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem);
        foreach ((int posicao, FaseDeclarada fase) in cronograma.Fases.Index())
        {
            if (!fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeHomologacao) && fase.Atos.RegraDeHomologacao is { } regra)
            {
                yield return new FaseComRegra(fase, posicao, regra, lugares.DaLinha(fase.Id));
            }
        }
    }

    private static FieldError TipoNaoDeclarado(FaseDeclarada fase, int posicao) =>
        CronogramaDoProcesso.NaFase(posicao, CampoDaRegra, new DomainError(
            MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado,
            $"A fase '{fase.Codigo}' tem regra de homologação das inscrições, mas não declara o tipo de decisão de homologação das inscrições."));

    private static IEnumerable<FieldError> FaltasDasCondicoes(FaseDeclarada fase, int posicao, RegraDeHomologacaoDasInscricoes regra, IReadOnlyCollection<MotivoDoProcesso> escolhidos)
    {
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(fase.OrigemId);

        return
        from alternativa in regra.Alternativas.Select(static (valor, indice) => (valor, indice))
        from condicao in alternativa.valor.Condicoes.Select(static (valor, indice) => (valor, indice))
        let codigo = condicao.valor.MotivoDesfavoravelCodigo
        where !escolhidos.Any(m => m.Lugar == lugar && m.Tipo == Tipo && m.Sentido == SentidoDoMotivo.Desfavoravel && string.Equals(m.Codigo.Valor, codigo, StringComparison.Ordinal))
        select CronogramaDoProcesso.NaFase(posicao, $"{CampoDaRegra}.alternativas[{alternativa.indice}].condicoes[{condicao.indice}].motivoDesfavoravelCodigo",
            new DomainError(MotivoDeDecisaoErrorCodes.CoberturaInsuficiente, Mensagem(fase, lugar, codigo, escolhidos)));
    }

    private static string Mensagem(FaseDeclarada fase, LugarDaDecisao lugar, string? codigo, IReadOnlyCollection<MotivoDoProcesso> escolhidos)
    {
        if (codigo is null)
        {
            return $"A condição da fase '{fase.Codigo}' não informa motivo desfavorável.";
        }

        MotivoDoProcesso? motivo = escolhidos.FirstOrDefault(m => m.Lugar == lugar && string.Equals(m.Codigo.Valor, codigo, StringComparison.Ordinal));
        string causa = motivo is null ? "não foi escolhido na fase" : motivo.Tipo != Tipo ? "é de outro tipo de decisão" : "é favorável";
        return $"O motivo da condição da fase '{fase.Codigo}' {causa}.";
    }

    /// <summary>A fase com regra de homologação e o lugar que a sua própria declaração ocupa no retrato, não o de outra fase de mesma origem.</summary>
    private sealed record FaseComRegra(FaseDeclarada Fase, int Posicao, RegraDeHomologacaoDasInscricoes Regra, LugarNoProcesso? Lugar)
    {
        public bool DeclaraOTipo => Lugar?.TiposDeDecisao.Contains(Tipo) is true;

        public bool TiposComParteRecusada => Lugar?.TiposDeDecisaoComParteRecusada is true;
    }
}
