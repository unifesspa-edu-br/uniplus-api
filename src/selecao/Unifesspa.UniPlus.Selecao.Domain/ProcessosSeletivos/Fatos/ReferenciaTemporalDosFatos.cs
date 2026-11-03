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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class ReferenciaTemporalDosFatos
{
    private const string FaixaEtaria = "FAIXA_ETARIA";

    internal static DomainError? RecusaDaFaseForaDoCronograma(EstadoDaConfiguracao estado, ReferenciaTemporalFatos? referencia)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return referencia is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } and { FaseId: { } faseId }
            && !estado.CronogramaFases.Any(f => f.Id == faseId)
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
    public static FaseCronograma? FaseQueAncoraOPeriodoDeInscricao(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return estado.CronogramaFases
            .Where(static f => f.ColetaInscricao)
            .OrderBy(static f => f.Ordem)
            .FirstOrDefault();
    }

    /// <summary>
    /// Pendência de <see cref="ReferenciaTemporalFatos"/>, avaliada depois de
    /// <see cref="ProtecaoDasExigencias.PendenciaDasExigenciasDocumentais"/>.
    /// </summary>
    /// <remarks>
    /// Sem fallback silencioso (ADR-0111:235-236): se alguma regra — gatilho de exigência ou regra
    /// do formulário — citar <c>FAIXA_ETARIA</c>, a referência
    /// precisa resolver para uma data concreta — política ausente, âncora de fase sem o
    /// extremo escolhido, ou <c>FIM_INSCRICAO</c> sem fase que colete inscrição com
    /// <c>Fim</c> definido bloqueiam a publicação. O congelamento da <c>DateOnly</c>
    /// concreta é do congelamento; esta validação só prova que ela É resolvível.
    /// </remarks>
    internal static DomainError? PendenciaDaReferenciaTemporalFatos(EstadoDaConfiguracao estado)
    {
        if (!CitaFaixaEtaria(estado))
        {
            return null;
        }

        if (ReferenciaTemporalFatosAusenteQuandoExigida(estado))
        {
            return new DomainError(
                "ProcessoSeletivo.ReferenciaTemporalFatosAusente",
                "Uma regra do processo (gatilho de exigência ou regra do formulário) cita FAIXA_ETARIA, mas nenhuma referência temporal de fatos foi configurada — a publicação não pode resolver a idade do candidato sem fallback silencioso (ADR-0111).");
        }

        ReferenciaTemporalFatos referencia = estado.ReferenciaTemporalFatos!;

        if (referencia.Tipo is ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase)
        {
            if (ReferenciaTemporalFatosFaseNaoPertenceAoCronograma(estado))
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente",
                    "A fase âncora da referência temporal de fatos não pertence (mais) ao cronograma deste processo.");
            }

            if (ReferenciaTemporalFatosExtremoDaFaseAusente(estado))
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente",
                    $"A fase âncora da referência temporal de fatos não tem {(referencia.Tipo == ReferenciaTipo.InicioFase ? "Início" : "Fim")} definido — sem fallback silencioso.");
            }
        }
        else if (referencia.Tipo == ReferenciaTipo.FimInscricao)
        {
            // Nada no domínio impede MAIS de uma fase com ColetaInscricao: a pergunta certa é
            // existencial (Any), não posicional.
            if (ReferenciaTemporalFatosFimInscricaoIndisponivel(estado))
            {
                return new DomainError(
                    "ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel",
                    "FIM_INSCRICAO exige uma fase que colete inscrição com Fim definido — sem fallback silencioso.");
            }
        }

        // DATA_ESPECIFICA: ReferenciaTemporalFatos.Criar já garante Data presente — nada a checar aqui.
        return null;
    }

    /// <summary>
    /// Alguma regra viva do processo — gatilho de exigência ou regra do formulário — cita a faixa
    /// etária; sem isso, a referência temporal é opcional.
    /// </summary>
    internal static bool CitaFaixaEtaria(EstadoDaConfiguracao estado) =>
        ReferenciasVivas.CondicoesVivasComOperador(estado).Any(static c => string.Equals(c.Fato, FaixaEtaria, StringComparison.Ordinal));

    /// <summary>A fase âncora de InicioFase/FimFase — <see langword="null"/> quando o tipo não usa fase ou ela não pertence (mais) ao cronograma.</summary>
    internal static FaseCronograma? FaseAncoraDaReferenciaTemporal(EstadoDaConfiguracao estado) =>
        estado.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } referencia
            ? estado.CronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId)
            : null;

    internal static bool ReferenciaTemporalFatosAusenteQuandoExigida(EstadoDaConfiguracao estado) =>
        CitaFaixaEtaria(estado) && estado.ReferenciaTemporalFatos is null;

    internal static bool ReferenciaTemporalFatosFaseNaoPertenceAoCronograma(EstadoDaConfiguracao estado) =>
        CitaFaixaEtaria(estado)
        && estado.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase }
        && FaseAncoraDaReferenciaTemporal(estado) is null;

    internal static bool ReferenciaTemporalFatosExtremoDaFaseAusente(EstadoDaConfiguracao estado) =>
        CitaFaixaEtaria(estado)
        && estado.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.InicioFase or ReferenciaTipo.FimFase } referencia
        && FaseAncoraDaReferenciaTemporal(estado) is { } fase
        && (referencia.Tipo == ReferenciaTipo.InicioFase ? fase.Inicio : fase.Fim) is null;

    internal static bool ReferenciaTemporalFatosFimInscricaoIndisponivel(EstadoDaConfiguracao estado) =>
        CitaFaixaEtaria(estado)
        && estado.ReferenciaTemporalFatos is { Tipo: ReferenciaTipo.FimInscricao }
        && !estado.CronogramaFases.Any(static f => f.ColetaInscricao && f.Fim is not null);

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
    public static DateOnly? ResolverDataReferenciaFatos(EstadoDaConfiguracao estado, TimeZoneInfo fusoHorario)
    {
        ArgumentNullException.ThrowIfNull(estado);

        ArgumentNullException.ThrowIfNull(fusoHorario);

        if (!CitaFaixaEtaria(estado))
        {
            return null;
        }

        if (estado.ReferenciaTemporalFatos is not { } referencia)
        {
            throw new InvalidOperationException(
                "Resolução de dataReferenciaFatos sem estado.ReferenciaTemporalFatos configurada — PendenciaDaReferenciaTemporalFatos deveria ter recusado a transição antes deste ponto.");
        }

        return DataDaReferencia(estado, referencia, fusoHorario)
            ?? throw new InvalidOperationException(
                "dataReferenciaFatos não resolvível a partir do cronograma — PendenciaDaReferenciaTemporalFatos deveria ter recusado a transição antes deste ponto.");
    }

    /// <summary>
    /// A data de referência que a configuração viva já resolve, para o rascunho em montagem: nula
    /// quando nenhuma regra cita <c>FAIXA_ETARIA</c>, quando a referência ainda não foi declarada ou
    /// quando a fase que a ancora ainda não tem a data. A publicação usa
    /// <see cref="ResolverDataReferenciaFatos"/>, que recusa o que não resolve.
    /// </summary>
    public static DateOnly? DataReferenciaFatosEmMontagem(EstadoDaConfiguracao estado, TimeZoneInfo fusoHorario)
    {
        ArgumentNullException.ThrowIfNull(estado);

        ArgumentNullException.ThrowIfNull(fusoHorario);
        return CitaFaixaEtaria(estado) && estado.ReferenciaTemporalFatos is { } referencia ? DataDaReferencia(estado, referencia, fusoHorario) : null;
    }

    internal static DateOnly? DataDaReferencia(EstadoDaConfiguracao estado, ReferenciaTemporalFatos referencia, TimeZoneInfo fusoHorario)
    {
        if (referencia.Tipo == ReferenciaTipo.DataEspecifica)
        {
            return referencia.Data!.Value;
        }

        DateTimeOffset? extremo = referencia.Tipo switch
        {
            ReferenciaTipo.InicioFase => estado.CronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId)?.Inicio,
            ReferenciaTipo.FimFase => estado.CronogramaFases.FirstOrDefault(f => f.Id == referencia.FaseId)?.Fim,
            // A coleção de fases preserva a ORDEM DE
            // ENTRADA de DefinirCronogramaFases, mas o envelope escreve as fases ordenadas
            // por Ordem (canonicalização determinística) — havendo mais de uma fase de
            // coleta com Fim, a fase publicada dependeria da ordem em que o caller as
            // passou, e a reidratação (sempre em ordem de Ordem) poderia escolher outra,
            // quebrando o round-trip (SombraParaVerificacao). OrderBy(Ordem) torna a escolha
            // determinística e independente de como a coleção foi populada.
            ReferenciaTipo.FimInscricao => estado.CronogramaFases
                .Where(static f => f.ColetaInscricao && f.Fim is not null)
                .OrderBy(static f => f.Ordem)
                .FirstOrDefault()?.Fim,
            _ => throw new InvalidOperationException($"Tipo de estado.ReferenciaTemporalFatos não reconhecido: {referencia.Tipo}."),
        };

        return extremo is { } instante ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fusoHorario).DateTime) : null;
    }
}
