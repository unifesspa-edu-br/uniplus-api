namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O edital, as etapas, a distribuição de vagas, a classificação e o cronograma lidos do envelope.
/// </summary>
internal sealed record BlocosDaConfiguracao
{
    public required DadosEdital? Dados { get; init; }

    public required string HashDocumento { get; init; }

    public required IReadOnlyList<MotivoDeDecisaoDeclarado> MotivosDeDecisao { get; init; }

    public DeclaracaoDaProvaPresencial? ProvaPresencial { get; init; }

    public DeclaracaoDaImportacaoDoResultado? ImportacaoDoResultado { get; init; }

    public CadastroDeReservaPorChamada CadastroDeReserva { get; init; } = CadastroDeReservaPorChamada.SemReserva;

    public ManifestacaoDeInteresse? ManifestacaoDeInteresse { get; init; }

    public required IReadOnlyList<EtapaProcesso> Etapas { get; init; }

    public required IReadOnlyList<ConfiguracaoDistribuicaoVagas> Distribuicao { get; init; }

    public required AtendimentoEspecializado? Atendimento { get; init; }

    public required ConfiguracaoDoFada? Fada { get; init; }

    public required ConfiguracaoCascataRemanejamento? Cascata { get; init; }

    public required IReadOnlyList<CriterioDesempate> Desempate { get; init; }

    public required ConfiguracaoClassificacao? Classificacao { get; init; }

    public required IReadOnlyList<FaseCronograma> CronogramaFases { get; init; }
}
