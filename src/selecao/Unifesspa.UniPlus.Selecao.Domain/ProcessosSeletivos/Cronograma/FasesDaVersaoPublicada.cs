namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As fases que uma versão publicada do processo declara e o período em que a versão vigorou: vigente a partir de um
/// instante e, quando outra a sucedeu, substituída em outro. É só o que o início das fases precisa do cronograma.
/// </summary>
public sealed record FasesDaVersaoPublicada(
    Guid VersaoId,
    DateTimeOffset VigenteAPartirDe,
    DateTimeOffset? SubstituidaEm,
    IReadOnlyList<FaseDaVersaoPublicada> Fases)
{
    /// <summary>
    /// Se a versão chegou a vigorar. A versão sucedida no mesmo instante em que passou a vigir nunca vigorou, e o período
    /// que ela declarava não é evidência de nada.
    /// </summary>
    public bool Vigorou => SubstituidaEm != VigenteAPartirDe;

    /// <summary>As fases que contêm a etapa nesta versão; a etapa se liga à fase pelo código dela, e mais de uma fase pode contê-la.</summary>
    public IEnumerable<FaseDaVersaoPublicada> FasesComAEtapa(Guid etapaId) =>
        Fases.Where(f => f.Etapas.Any(e => e.Id == etapaId));

    /// <summary>Se alguma fase desta versão tem a fase canônica de origem informada.</summary>
    public bool TemAFase(Guid faseCanonicaOrigemId) =>
        Fases.Any(f => f.FaseCanonicaOrigemId == faseCanonicaOrigemId);

    /// <summary>Se alguma fase desta versão tem a etapa informada.</summary>
    public bool TemAEtapa(Guid etapaId) =>
        Fases.Any(f => f.Etapas.Any(e => e.Id == etapaId));

    /// <summary>
    /// A fase que recebe a inscrição no instante: a que coleta inscrição com o período aberto nele e, sem período que o
    /// contenha, a primeira que coleta, a mesma que determina o período de inscrição do edital. Nula quando nenhuma coleta.
    /// </summary>
    public FaseDaVersaoPublicada? FaseQueRecebeInscricaoEm(DateTimeOffset instante)
    {
        List<FaseDaVersaoPublicada> quemColeta = [.. Fases.Where(static f => f.ColetaInscricao)];

        return quemColeta.FirstOrDefault(f => f.Periodo.Inicio <= instante && (f.Periodo.Fim is null || instante <= f.Periodo.Fim)) ?? quemColeta.FirstOrDefault();
    }

    /// <summary>
    /// A primeira das datas informadas que já chegou a <paramref name="agora"/> enquanto a versão vigia. A data que coincide
    /// com o instante da substituição não conta, e data ausente nunca conta. Nula quando nenhuma conta.
    /// </summary>
    public DateTimeOffset? PrimeiraDataQueChegouEnquantoVigia(IEnumerable<DateTimeOffset?> datas, DateTimeOffset agora) =>
        datas.Where(d => d <= agora && (SubstituidaEm is null || d < SubstituidaEm)).Min();
}

/// <summary>Uma fase da versão publicada, com o período e as etapas que a versão declara para ela.</summary>
public sealed record FaseDaVersaoPublicada(
    Guid Id,
    Guid FaseCanonicaOrigemId,
    string Codigo,
    string TipoFaseNome,
    Guid? UnidadeResponsavelOrigemId,
    JanelaDeTempo Periodo,
    bool ColetaInscricao,
    IReadOnlyList<EtapaDaVersaoPublicada> Etapas)
{
    /// <summary>A fase citada pelo nome que a versão publicada guardou.</summary>
    public CitacaoDaFase Citacao => new(TipoFaseNome);
}

/// <summary>Uma etapa da fase, com o período próprio que a versão declara.</summary>
public sealed record EtapaDaVersaoPublicada(Guid Id, string Nome, JanelaDeTempo Periodo);
