namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A oferta do processo: a distribuição de vagas, o número de opções de curso, o bônus regional, a cascata e o atendimento especializado.</summary>
public sealed class VagasDeclaradas
{
    public VagasDeclaradas(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas,
        int? numeroDeOpcoesDeCurso,
        bool? aplicaBonusRegional,
        ConfiguracaoBonusRegional? bonusRegional,
        ConfiguracaoCascataRemanejamento? cascata,
        AtendimentoEspecializado? atendimentoEspecializado)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);
        DistribuicaoVagas = [.. distribuicaoVagas];
        NumeroDeOpcoesDeCurso = numeroDeOpcoesDeCurso;
        AplicaBonusRegional = aplicaBonusRegional;
        BonusRegional = bonusRegional;
        Cascata = cascata;
        AtendimentoEspecializado = atendimentoEspecializado;
    }

    public IReadOnlyList<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas { get; }

    public int? NumeroDeOpcoesDeCurso { get; }

    public bool? AplicaBonusRegional { get; }

    public ConfiguracaoBonusRegional? BonusRegional { get; }

    public ConfiguracaoCascataRemanejamento? Cascata { get; }

    public AtendimentoEspecializado? AtendimentoEspecializado { get; }

    /// <summary>Alguma oferta declara vagas: o processo tem resultado a produzir.</summary>
    public bool HaVagasOfertadas => DistribuicaoVagas.Any(static oferta => oferta.VagasOfertadasBase > 0);
}
