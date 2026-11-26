namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>A oferta do processo: a distribuição de vagas, o número de opções de curso, o Fator de Atenuação das Desigualdades de Acesso (FADA), a cascata e o atendimento especializado.</summary>
public sealed class VagasDeclaradas
{
    public VagasDeclaradas(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicaoVagas,
        int? numeroDeOpcoesDeCurso,
        bool? aplicaFada,
        ConfiguracaoDoFada? fada,
        ConfiguracaoCascataRemanejamento? cascata,
        AtendimentoEspecializado? atendimentoEspecializado)
    {
        ArgumentNullException.ThrowIfNull(distribuicaoVagas);
        DistribuicaoVagas = [.. distribuicaoVagas];
        NumeroDeOpcoesDeCurso = numeroDeOpcoesDeCurso;
        AplicaFada = aplicaFada;
        Fada = fada;
        Cascata = cascata;
        AtendimentoEspecializado = atendimentoEspecializado;
    }

    public IReadOnlyList<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas { get; }

    public int? NumeroDeOpcoesDeCurso { get; }

    public bool? AplicaFada { get; }

    public ConfiguracaoDoFada? Fada { get; }

    public ConfiguracaoCascataRemanejamento? Cascata { get; }

    public AtendimentoEspecializado? AtendimentoEspecializado { get; }

    /// <summary>Alguma oferta declara vagas: o processo tem resultado a produzir.</summary>
    public bool HaVagasOfertadas => DistribuicaoVagas.Any(static oferta => oferta.VagasOfertadasBase > 0);
}
