namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class ReferenciaTemporalDosFatos
{
    private const string FaixaEtaria = "FAIXA_ETARIA";

    internal static DomainError? RecusaDaFaseForaDoCronograma(ConfiguracaoDeclarada configuracao, ReferenciaTemporalFatos? referencia)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return referencia is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } and { FaseId: { } faseId }
            && !configuracao.Cronograma.CronogramaFases.Any(f => f.Id == faseId)
                ? new DomainError(
                    "ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso",
                    $"A fase {faseId} não pertence ao cronograma deste processo.")
                : null;
    }

    /// <summary>
    /// A fase do cronograma que ancora o período de inscrição do Edital — a de
    /// menor <see cref="FaseCronograma.Ordem"/> entre as que coletam inscrição, ou
    /// <see langword="null"/> quando o certame não coleta inscrição pelo sistema.
    /// </summary>
    /// <remarks>
    /// Nada impede mais de uma fase com <see cref="FaseCronograma.ColetaInscricao"/>, e a coleção
    /// preserva a ordem de ENTRADA, não a de <c>Ordem</c> — mesma armadilha que
    /// <see cref="ResolverDataReferenciaFatos"/> documenta: o envelope escreve as fases ordenadas
    /// por <c>Ordem</c>, então escolher posicionalmente faria a reidratação eleger outra fase e
    /// quebrar o round-trip. <c>Ordem</c> é única por processo, o que torna a escolha determinística.
    /// <para>
    /// Havendo duas janelas de coleta, o período publicado é o da primeira. É consequência aceita:
    /// a segunda janela (tipicamente remanejamento) não estende o período declarado no Edital.
    /// </para>
    /// </remarks>
    public static FaseCronograma? FaseQueAncoraOPeriodoDeInscricao(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return FaseQueAncoraOPeriodoDeInscricao(configuracao.Cronograma.CronogramaFases, static f => f.ColetaInscricao, static f => f.Ordem);
    }

    /// <summary>A eleição da fase de referência da inscrição sobre qualquer representação de fase: a de menor ordem entre as que coletam inscrição.</summary>
    internal static TFase? FaseQueAncoraOPeriodoDeInscricao<TFase>(
        IEnumerable<TFase> fases, Func<TFase, bool> coletaInscricao, Func<TFase, int> ordem)
        where TFase : class =>
        fases.Where(coletaInscricao).OrderBy(ordem).FirstOrDefault();

    /// <summary>
    /// Pendência de <see cref="ReferenciaTemporalFatos"/>, avaliada depois de
    /// <see cref="PendenciasDasExigencias.PendenciaDasExigenciasDocumentais"/>.
    /// </summary>
    /// <remarks>
    /// Sem fallback silencioso (ADR-0111:235-236): se alguma regra — gatilho de exigência ou regra
    /// do formulário — citar <c>FAIXA_ETARIA</c>, a referência
    /// precisa resolver para uma data concreta — política ausente, âncora de fase sem o
    /// extremo escolhido, ou <c>FIM_INSCRICAO</c> sem fase que colete inscrição com
    /// <c>Fim</c> definido bloqueiam a publicação. O congelamento da <c>DateOnly</c>
    /// concreta é do congelamento; esta validação só prova que ela É resolvível.
    /// </remarks>
    internal static DomainError? PendenciaDaReferenciaTemporalFatos(ConfiguracaoDeclarada configuracao) =>
        !CitaFaixaEtaria(configuracao) ? null
        : ReferenciaTemporalFatosAusenteQuandoExigida(configuracao) ? ReferenciaAusente()
        : ReferenciaTemporalFatosFaseNaoPertenceAoCronograma(configuracao) ? FaseQueAncoraForaDoCronograma()
        : ReferenciaTemporalFatosExtremoDaFaseAusente(configuracao) ? ExtremoDaFaseAusente(configuracao.Coleta.ReferenciaTemporalFatos!.Tipo)
        : ReferenciaTemporalFatosFimInscricaoIndisponivel(configuracao) ? FimDaInscricaoIndisponivel()
        : null;

    private static DomainError ReferenciaAusente() => new(
        "ProcessoSeletivo.ReferenciaTemporalFatosAusente",
        "Uma regra do processo (gatilho de exigência ou regra do formulário) cita FAIXA_ETARIA, mas nenhuma referência temporal de fatos foi configurada — a publicação não pode resolver a idade do candidato sem fallback silencioso (ADR-0111).");

    private static DomainError FaseQueAncoraForaDoCronograma() => new(
        "ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente",
        "A fase usada como referência temporal de fatos não pertence (mais) ao cronograma deste processo. Escolha uma fase do cronograma.");

    private static DomainError ExtremoDaFaseAusente(ReferenciaTipo tipo) => new(
        "ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente",
        $"A fase usada como referência temporal de fatos não tem a data de {(tipo == ReferenciaTipo.InicioFase ? "início" : "fim")} definida. Defina-a no cronograma.");

    private static DomainError FimDaInscricaoIndisponivel() => new(
        "ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel",
        "FIM_INSCRICAO exige uma fase que colete inscrição com Fim definido — sem fallback silencioso.");

    /// <summary>
    /// Alguma regra viva do processo — gatilho de exigência ou regra do formulário — cita a faixa
    /// etária; sem isso, a referência temporal é opcional.
    /// </summary>
    internal static bool CitaFaixaEtaria(ConfiguracaoDeclarada configuracao) =>
        ReferenciasVivas.CondicoesVivasComOperador(configuracao).Any(static c => string.Equals(c.Fato, FaixaEtaria, StringComparison.Ordinal));

    /// <summary>A fase âncora de InicioFase/FimFase — <see langword="null"/> quando o tipo não usa fase ou ela não pertence (mais) ao cronograma.</summary>
    internal static FaseCronograma? FaseAncoraDaReferenciaTemporal(ConfiguracaoDeclarada configuracao) =>
        configuracao.Coleta.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } referencia
            ? configuracao.Cronograma.CronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId)
            : null;

    internal static bool ReferenciaTemporalFatosAusenteQuandoExigida(ConfiguracaoDeclarada configuracao) =>
        CitaFaixaEtaria(configuracao) && configuracao.Coleta.ReferenciaTemporalFatos is null;

    internal static bool ReferenciaTemporalFatosFaseNaoPertenceAoCronograma(ConfiguracaoDeclarada configuracao) =>
        CitaFaixaEtaria(configuracao)
        && configuracao.Coleta.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase }
        && FaseAncoraDaReferenciaTemporal(configuracao) is null;

    internal static bool ReferenciaTemporalFatosExtremoDaFaseAusente(ConfiguracaoDeclarada configuracao) =>
        CitaFaixaEtaria(configuracao)
        && configuracao.Coleta.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } referencia
        && FaseAncoraDaReferenciaTemporal(configuracao) is { } fase
        && (referencia.Tipo == ReferenciaTipo.InicioFase ? fase.Inicio : fase.Fim) is null;

    internal static bool ReferenciaTemporalFatosFimInscricaoIndisponivel(ConfiguracaoDeclarada configuracao) =>
        CitaFaixaEtaria(configuracao)
        && configuracao.Coleta.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.FimInscricao }
        && !configuracao.Cronograma.CronogramaFases.Any(static f => f.ColetaInscricao && f.Fim is not null);

    /// <summary>
    /// Resolve <see cref="ReferenciaTemporalFatos"/> para a <see cref="DateOnly"/> concreta
    /// que o envelope congela como <c>dataReferenciaFatos</c> — o
    /// mesmo par (Tipo, âncora) que <see cref="ReferenciaTemporalDosFatos.PendenciaDaReferenciaTemporalFatos"/> já
    /// provou resolvível antes da transição chamar este método; aqui só se resolve, sem
    /// revalidar. A virada do dia UTC→local é a razão de existir deste método, em vez de o
    /// encoder ler <see cref="DateTimeOffset"/> cru.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> quando nenhuma regra cita <c>FAIXA_ETARIA</c> — a resolução
    /// não foi provada nem é necessária, e congelar uma data não pedida por ninguém seria
    /// dado morto no envelope, não uma garantia a mais.
    /// </returns>
    /// <param name="fusoHorario">
    /// Zona em que a âncora vira dia civil. Vem da canonicalização — na publicação é o fuso
    /// institucional corrente, e ao provar uma versão já publicada é o que ela congelou. Deixar o
    /// método escolher a zona sozinho faria o envelope declarar um fuso no bloco de localidade e
    /// calcular a data por outro, congelando o dia civil errado numa versão imutável.
    /// </param>
    public static DateOnly? ResolverDataReferenciaFatos(ConfiguracaoDeclarada configuracao, TimeZoneInfo fusoHorario)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        ArgumentNullException.ThrowIfNull(fusoHorario);

        return CitaFaixaEtaria(configuracao) ? DataJaProvada(configuracao, fusoHorario) : null;
    }

    /// <summary>A data que a pendência da referência temporal já provou resolvível; só chega aqui quem passou por ela.</summary>
    private static DateOnly DataJaProvada(ConfiguracaoDeclarada configuracao, TimeZoneInfo fusoHorario)
    {
        if (configuracao.Coleta.ReferenciaTemporalFatos is not { } referencia)
        {
            throw new InvalidOperationException(
                "Resolução de dataReferenciaFatos sem configuracao.Coleta.ReferenciaTemporalFatos configurada — PendenciaDaReferenciaTemporalFatos deveria ter recusado a transição antes deste ponto.");
        }

        return DataDaReferencia(configuracao, referencia, fusoHorario)
            ?? throw new InvalidOperationException(
                "dataReferenciaFatos não resolvível a partir do cronograma — PendenciaDaReferenciaTemporalFatos deveria ter recusado a transição antes deste ponto.");
    }

    /// <summary>
    /// A data de referência que a configuração viva já resolve, para o rascunho em montagem: nula
    /// quando nenhuma regra cita <c>FAIXA_ETARIA</c>, quando a referência ainda não foi declarada ou
    /// quando a fase que a ancora ainda não tem a data. A publicação usa
    /// <see cref="ResolverDataReferenciaFatos"/>, que recusa o que não resolve.
    /// </summary>
    public static DateOnly? DataReferenciaFatosEmMontagem(ConfiguracaoDeclarada configuracao, TimeZoneInfo fusoHorario)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        ArgumentNullException.ThrowIfNull(fusoHorario);
        return CitaFaixaEtaria(configuracao) && configuracao.Coleta.ReferenciaTemporalFatos is { } referencia ? DataDaReferencia(configuracao, referencia, fusoHorario) : null;
    }

    internal static DateOnly? DataDaReferencia(ConfiguracaoDeclarada configuracao, ReferenciaTemporalFatos referencia, TimeZoneInfo fusoHorario) =>
        referencia.Tipo == ReferenciaTipo.DataEspecifica ? referencia.Data!.Value
        : InstanteQueAncora(configuracao, referencia) is { } instante ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fusoHorario).DateTime)
        : null;

    /// <summary>O instante da fase ou da inscrição em que a referência se ancora; ausente quando a fase ou o extremo não existe.</summary>
    private static DateTimeOffset? InstanteQueAncora(ConfiguracaoDeclarada configuracao, ReferenciaTemporalFatos referencia) =>
        referencia.Tipo switch
        {
            ReferenciaTipo.InicioFase => FaseAncoraDaReferenciaTemporal(configuracao)?.Inicio,
            ReferenciaTipo.FimFase => FaseAncoraDaReferenciaTemporal(configuracao)?.Fim,
            ReferenciaTipo.FimInscricao => FimDaPrimeiraFaseDeInscricaoComFim(configuracao),
            _ => throw new InvalidOperationException($"Tipo de configuracao.Coleta.ReferenciaTemporalFatos não reconhecido: {referencia.Tipo}."),
        };

    /// <summary>
    /// O fim da primeira fase, por ordem, que coleta inscrição e tem fim. A coleção de fases preserva a ordem de entrada,
    /// mas o envelope escreve as fases ordenadas por ordem: havendo mais de uma fase de coleta com fim, a fase publicada
    /// dependeria da ordem em que o chamador as passou, e a reidratação poderia escolher outra, quebrando o round-trip da
    /// sombra de verificação. Ordenar torna a escolha determinística.
    /// </summary>
    private static DateTimeOffset? FimDaPrimeiraFaseDeInscricaoComFim(ConfiguracaoDeclarada configuracao) =>
        configuracao.Cronograma.CronogramaFases
            .Where(static f => f.ColetaInscricao && f.Fim is not null)
            .OrderBy(static f => f.Ordem)
            .FirstOrDefault()?.Fim;
}
