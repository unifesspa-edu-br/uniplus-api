namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// A configuração de um <see cref="ProcessoSeletivo"/> como o autor a declarou, num instante: é o que as regras da
/// configuração recebem no lugar da raiz. Só a raiz a produz (<see cref="ProcessoSeletivo.Declaracao"/>), com cópias das
/// coleções do agregado, de modo que uma mutação posterior da raiz não altera uma declaração já tirada. A posição de um
/// item é o índice dele na lista, e a origem (<see cref="Origem"/>) liga essa posição à do documento recebido.
/// </summary>
public sealed class ConfiguracaoDeclarada
{
    internal ConfiguracaoDeclarada(
        IdentidadeDoProcessoDeclarada identidade,
        CronogramaDeclarado cronograma,
        AvaliacaoDeclarada avaliacao,
        VagasDeclaradas vagas,
        ExigenciasDeclaradas exigencias,
        ColetaDeclarada coleta,
        DivulgacaoETaxaDeclaradas divulgacaoETaxa)
    {
        Identidade = identidade;
        Cronograma = cronograma;
        Avaliacao = avaliacao;
        Vagas = vagas;
        Exigencias = exigencias;
        Coleta = coleta;
        DivulgacaoETaxa = divulgacaoETaxa;
    }

    public IdentidadeDoProcessoDeclarada Identidade { get; }

    public CronogramaDeclarado Cronograma { get; }

    public AvaliacaoDeclarada Avaliacao { get; }

    public VagasDeclaradas Vagas { get; }

    public ExigenciasDeclaradas Exigencias { get; }

    public ColetaDeclarada Coleta { get; }

    public DivulgacaoETaxaDeclaradas DivulgacaoETaxa { get; }

    public DeclaracaoDaProvaPresencial? ProvaPresencial { get; internal init; }

    public IReadOnlyList<MotivoDeDecisaoDeclarado> MotivosDeDecisao { get; internal init; } = [];

    /// <summary>
    /// Onde cada parte está no documento recebido e as partes que a leitura recusou; a declaração que vem da raiz tem a
    /// identidade: a posição de origem é a canônica e nada foi recusado.
    /// </summary>
    public OrigemNoDocumento Origem { get; internal init; } = OrigemNoDocumento.Identidade;

    /// <summary>As derivações configuradas e as que a oferta de vagas define.</summary>
    public IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesEfetivas() =>
        DerivacoesDaOferta.Efetivas(Coleta.RegrasDerivacao, Vagas.DistribuicaoVagas);
}
