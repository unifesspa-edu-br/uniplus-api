namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class FaseEfetivaDoFato
{
    /// <summary>
    /// A recusa de citar o fato num gatilho de exigência da fase dada (UNI-REQ-0144, UNI-REQ-0077):
    /// nenhum documento é pedido antes de o fato ser conhecido. O fato fica conhecido na mais
    /// tardia entre a fase do catálogo (<paramref name="pontoResolucaoPorFato"/>) e a fase do
    /// formulário que o produz; o derivado por regra, não antes das suas dependências; e o agregado
    /// sobre grupo repetível (<paramref name="membroPorAgregado"/>), não antes do fato de membro. Fato
    /// produzido só pelo formulário de isenção só é citado por documento do formulário de isenção: a
    /// habilitação usa, de outra finalidade, só fatos da inscrição. Inscrição e isenção podem dividir
    /// a fase, e por isso a regra olha a finalidade da exigência, e não a fase.
    /// </summary>
    /// <remarks>
    /// Fato que o processo não coleta, não deriva e o catálogo não situa não é recusado aqui: a
    /// recusa por fato fora do processo é da conferência do universo. O derivado do sistema usa a
    /// fase do catálogo e a das dependências declaradas pelo mecanismo dele. A finalidade que falta
    /// ou que a fase não responde também não é recusada aqui: a recusa que orienta é a da própria
    /// finalidade, que a definição das exigências confere.
    /// </remarks>
    public static DomainError? RecusaDeFaseDoGatilho(
        EstadoDaConfiguracao estado,
        string fato,
        Guid exigidoNaFaseId,
        FinalidadeFormulario? finalidadeDaExigencia,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentException.ThrowIfNullOrWhiteSpace(fato);
        ArgumentNullException.ThrowIfNull(pontoResolucaoPorFato);
        ArgumentNullException.ThrowIfNull(membroPorAgregado);

        if (estado.CronogramaFases.FirstOrDefault(f => f.Id == exigidoNaFaseId) is not { } faseDaExigencia
            || Services.ValidadorVinculoDaExigencia.RecusaDaFinalidade(finalidadeDaExigencia, faseDaExigencia) is not null)
        {
            return null;
        }

        FaseEfetiva efetiva = Resolver(estado, fato, pontoResolucaoPorFato, membroPorAgregado, []);
        if (efetiva.Recusa is { } recusa)
        {
            return recusa;
        }

        if (efetiva.Fase is { } fase && fase.Ordem > faseDaExigencia.Ordem)
        {
            return new DomainError(
                DocumentoExigidoErrorCodes.FatoResolvidoEmFasePosterior,
                $"O fato '{fato}' só é conhecido na fase '{fase.Codigo}' (ordem {fase.Ordem}), posterior à fase em que o documento é exigido (ordem {faseDaExigencia.Ordem}).");
        }

        if (finalidadeDaExigencia != FinalidadeFormulario.IsencaoTaxa
            && DependeDeFatoSoDaIsencao(estado, fato, membroPorAgregado, []))
        {
            return new DomainError(
                DocumentoExigidoErrorCodes.FatoDaIsencaoEmOutraFinalidade,
                $"O fato '{fato}' vem do formulário de isenção e só é citado por documento exigido no formulário de isenção.");
        }

        // As exigências de uma finalidade seguem a versão do formulário dela; o fato de outra
        // finalidade tem de ter sido coletado em todas as versões publicadas (UNI-REQ-0144).
        return CitacoesDosFormularios.CitacaoNaoGarantida(
            estado,
            [($"o gatilho de um documento exigido na fase '{faseDaExigencia.Codigo}'", fato)],
            finalidadeDaExigencia,
            membroPorAgregado);
    }

    /// <summary>A fase em que o fato fica conhecido no processo, ou a recusa quando o catálogo o situa fora do cronograma.</summary>
    internal static FaseEfetiva Resolver(
        EstadoDaConfiguracao estado,
        string fato,
        IReadOnlyDictionary<string, string> pontoResolucaoPorFato,
        IReadOnlyDictionary<string, string> membroPorAgregado,
        HashSet<string> emAvaliacao)
    {
        FaseCronograma? maisTardia = null;
        void Considerar(FaseCronograma? fase)
        {
            if (fase is not null && (maisTardia is null || fase.Ordem > maisTardia.Ordem))
            {
                maisTardia = fase;
            }
        }

        if (pontoResolucaoPorFato.TryGetValue(fato, out string? ponto))
        {
            if (estado.CronogramaFases.FirstOrDefault(f => string.Equals(f.Codigo, ponto, StringComparison.Ordinal)) is not { } faseDoPonto)
            {
                return new FaseEfetiva(null, new DomainError(
                    DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma,
                    $"O fato '{fato}' resolve na fase '{ponto}', que não pertence ao cronograma deste processo."));
            }

            Considerar(faseDoPonto);
        }

        if (estado.Campos.FirstOrDefault(f => string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal)) is { } coletado
            && estado.FormularioDe(coletado.Finalidade)?.FaseId is { } faseDoFormulario)
        {
            Considerar(estado.CronogramaFases.FirstOrDefault(f => f.Id == faseDoFormulario));
        }

        if (emAvaliacao.Add(fato))
        {
            foreach (string dependencia in DependenciasDoFato(estado, fato, membroPorAgregado))
            {
                FaseEfetiva daDependencia = Resolver(estado, dependencia, pontoResolucaoPorFato, membroPorAgregado, emAvaliacao);
                if (daDependencia.Recusa is not null)
                {
                    return daDependencia;
                }

                Considerar(daDependencia.Fase);
            }
        }

        return new FaseEfetiva(maisTardia, null);
    }

    /// <summary>Se o fato vem só do formulário de isenção, diretamente ou por uma dependência.</summary>
    internal static bool DependeDeFatoSoDaIsencao(EstadoDaConfiguracao estado, string fato, IReadOnlyDictionary<string, string> membroPorAgregado, HashSet<string> emAvaliacao) =>
        estado.Campos.Any(f => f.Finalidade == FinalidadeFormulario.IsencaoTaxa && string.Equals(f.FatoCodigo, fato, StringComparison.Ordinal))
        || (emAvaliacao.Add(fato)
            && DependenciasDoFato(estado, fato, membroPorAgregado).Any(d => DependeDeFatoSoDaIsencao(estado, d, membroPorAgregado, emAvaliacao)));

    /// <summary>
    /// Os fatos de que o valor do fato depende no processo: os citados pela regra de derivação dele,
    /// os declarados pelo mecanismo do derivado do sistema, ou o fato de membro, quando ele é
    /// agregado sobre grupo repetível.
    /// </summary>
    internal static IEnumerable<string> DependenciasDoFato(EstadoDaConfiguracao estado, string fato, IReadOnlyDictionary<string, string> membroPorAgregado) =>
        (estado.DerivacoesEfetivas().FirstOrDefault(r => string.Equals(r.CodigoFato, fato, StringComparison.Ordinal))?.FatosCitados ?? [])
            .Concat(DerivadosDoSistema.Dependencias.GetValueOrDefault(fato) ?? [])
            .Concat(membroPorAgregado.TryGetValue(fato, out string? membro) ? [membro] : []);

    internal sealed record FaseEfetiva(FaseCronograma? Fase, DomainError? Recusa);
}
