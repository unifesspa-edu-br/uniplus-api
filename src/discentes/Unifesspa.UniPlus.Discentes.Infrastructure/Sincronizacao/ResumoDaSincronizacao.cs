namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System;
using System.Collections.Generic;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;

/// <summary>
/// O que uma reconciliação encontrou e fez.
/// </summary>
/// <param name="LidosNaOrigem">Vínculos que a origem entregou, somando as duas varreduras.</param>
/// <param name="Repetidos">
/// Vínculos que apareceram nas duas varreduras. Esperado: elas se sobrepõem por
/// construção, e este número mostra o tamanho da sobreposição.
/// </param>
/// <param name="Inseridos">Vínculos que ainda não existiam na réplica.</param>
/// <param name="Atualizados">Vínculos cujo conteúdo mudou desde a última execução.</param>
/// <param name="Inalterados">Vínculos que não mudaram e por isso não foram reescritos.</param>
/// <param name="DescartadosPorRegistroIncompleto">
/// Vínculos que a réplica não aceita como estão — sem unidade acadêmica no curso, sem
/// período de ingresso, ou com valor além do que ela comporta. Rotina, com volume conhecido.
/// </param>
/// <param name="DescartadosForaDoContrato">
/// Vínculos que a origem entregou fora do que promete. Não é rotina: crescimento aqui
/// significa que a origem mudou o que entrega.
/// </param>
/// <param name="NaoGravadosPorFalha">
/// Vínculos que estavam em lotes que falharam ao gravar. A réplica não os recebeu, e nada
/// do que já estava lá foi perdido por causa disso.
/// </param>
/// <param name="FalhaQueInterrompeu">
/// O que impediu a leitura de terminar, quando foi o caso. Os números acima continuam
/// valendo até o ponto em que a execução parou.
/// </param>
public sealed record ResumoDaSincronizacao(
    int LidosNaOrigem,
    int Repetidos,
    int Inseridos,
    int Atualizados,
    int Inalterados,
    int DescartadosPorRegistroIncompleto,
    int DescartadosForaDoContrato,
    int NaoGravadosPorFalha,
    Exception? FalhaQueInterrompeu = null)
{
    /// <summary>Vínculos efetivamente escritos.</summary>
    public int Escritos => Inseridos + Atualizados;

    /// <summary>
    /// Vínculos que a execução chegou a tratar — escritos, já iguais, deixados de fora por
    /// decisão, ou perdidos num lote que falhou.
    /// </summary>
    /// <remarks>
    /// Inclui os de lote falho porque eles foram tratados: a execução chegou a decidir o
    /// que fazer com cada um e tentou gravá-los. Deixá-los de fora faria a soma dos
    /// aproveitados com os recusados exceder o total tratado — e quem lesse o registro da
    /// execução veria uma conta que não fecha.
    /// </remarks>
    public int Processados =>
        Escritos + Inalterados + DescartadosPorRegistroIncompleto
        + DescartadosForaDoContrato + NaoGravadosPorFalha;

    /// <summary>
    /// Traduz o resumo para os números que a execução registra.
    /// </summary>
    /// <remarks>
    /// Aproveitado é o que a réplica passou a refletir — escrito agora ou já igual antes.
    /// Recusado é o que ela não recebeu, seja porque não servia, seja porque a gravação
    /// falhou. Vínculo repetido entre as duas varreduras não entra em nenhum dos dois: ele
    /// foi contado uma vez, na varredura que o trouxe primeiro.
    /// </remarks>
    public ContagensDaExecucao EmContagens() => new(
        LidosNaOrigem,
        Processados,
        Escritos + Inalterados,
        DescartadosPorRegistroIncompleto + DescartadosForaDoContrato + NaoGravadosPorFalha);

    /// <summary>
    /// Como esta execução deve ser registrada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Falha ao gravar torna a execução parcial: parte do que a origem tem não chegou à
    /// réplica, e a próxima execução precisa completar.
    /// </para>
    /// <para>
    /// Leitura interrompida no meio também torna a execução parcial, e fracassada quando
    /// nada chegou a ser aproveitado.
    /// </para>
    /// <para>
    /// Uma execução que não escreveu nada e não tinha nada a escrever é completa — é o que
    /// acontece todo dia em que nenhum vínculo mudou, e é o caso mais comum. Já uma
    /// execução em que <b>tudo</b> foi descartado por entrega fora do contrato não é
    /// sucesso nenhum: significa que a origem mudou o que entrega e a réplica parou de ser
    /// alimentada.
    /// </para>
    /// </remarks>
    public SyncRunStatus Situacao
    {
        get
        {
            bool nadaAproveitado = Escritos == 0 && Inalterados == 0;

            if (FalhaQueInterrompeu is not null)
            {
                return nadaAproveitado ? SyncRunStatus.Failed : SyncRunStatus.Partial;
            }

            if (NaoGravadosPorFalha > 0)
            {
                return SyncRunStatus.Partial;
            }

            return nadaAproveitado && DescartadosForaDoContrato > 0
                ? SyncRunStatus.Failed
                : SyncRunStatus.Completed;
        }
    }
}

/// <summary>
/// Acumula os números de uma reconciliação enquanto ela acontece.
/// </summary>
internal sealed class ContagemDaSincronizacao
{
    private int _lidos;
    private int _repetidos;
    private int _inseridos;
    private int _atualizados;
    private int _inalterados;
    private int _incompletos;
    private int _foraDoContrato;
    private int _naoGravados;
    private Exception? _interrupcao;

    public void RegistrarLidos(int quantos) => _lidos += quantos;

    public void RegistrarRepetido() => _repetidos++;

    public void RegistrarDescartes(IReadOnlyList<VinculoDescartado> descartados)
    {
        foreach (VinculoDescartado descartado in descartados)
        {
            if (descartado.Motivo == MotivoDeDescarte.ForaDoContrato)
            {
                _foraDoContrato++;
            }
            else
            {
                _incompletos++;
            }
        }
    }

    public void RegistrarGravacao(ResultadoDaGravacao resultado)
    {
        _inseridos += resultado.Inseridos;
        _atualizados += resultado.Atualizados;
        _inalterados += resultado.Inalterados;
    }

    public void RegistrarLoteComFalha(int quantos) => _naoGravados += quantos;

    public void RegistrarInterrupcao(Exception falha) => _interrupcao = falha;

    public ResumoDaSincronizacao Fechar() => new(
        _lidos, _repetidos, _inseridos, _atualizados, _inalterados,
        _incompletos, _foraDoContrato, _naoGravados, _interrupcao);
}
