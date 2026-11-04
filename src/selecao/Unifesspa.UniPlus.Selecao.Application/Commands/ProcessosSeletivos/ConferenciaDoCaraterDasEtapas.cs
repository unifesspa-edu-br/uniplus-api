namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Confere o caráter declarado de cada etapa contra o que o tipo dela admite, numa passada própria e antes
/// de qualquer mutação: assim um payload com duas etapas incoerentes devolve as duas (ADR-0125), e nenhuma
/// instância rastreada precisa ser descartada.
/// </summary>
/// <remarks>
/// A passada só lê o cadastro quando há o que conferir. A etapa existente que mantém o vínculo e o caráter
/// não é reavaliada: sem esse recorte, estreitar um tipo ainda ativo — a operação que o cadastro existe
/// para permitir — recusaria todo PUT posterior de qualquer certame que já tivesse uma etapa daquele tipo.
/// O tipo lido fica na etapa conferida; os sinalizadores do snapshot só mudam quando o vínculo é novo ou
/// muda de tipo, ou quando muda o caráter, e renomear um tipo ativo não reescreve o snapshot que o cliente
/// nem tocou (ADR-0061).
/// </remarks>
internal sealed class ConferenciaDoCaraterDasEtapas(ITipoEtapaReader tipoEtapaReader)
{
    private readonly LeituraMemoizada<Guid, TipoEtapaView> _tiposAtivos =
        new((tipoEtapaId, cancellationToken) => tipoEtapaReader.ObterAtivoPorIdAsync(tipoEtapaId, cancellationToken));

    public async Task<Result<List<EtapaConferida>>> ConferirAsync(
        IReadOnlyList<EtapaConferida> etapas, CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        List<EtapaConferida> conferidas = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            (EtapaConferida conferida, IReadOnlyList<FieldError> recusas) =
                await ConferirEtapaAsync(etapas[indice], indice, cancellationToken).ConfigureAwait(false);
            conferidas.Add(conferida);
            erros.AddRange(recusas);
        }

        return erros.Count > 0
            ? Result<List<EtapaConferida>>.ValidationFailure(erros)
            : Result<List<EtapaConferida>>.Success(conferidas);
    }

    private async Task<(EtapaConferida Etapa, IReadOnlyList<FieldError> Recusas)> ConferirEtapaAsync(
        EtapaConferida etapa, int indice, CancellationToken cancellationToken)
    {
        if (etapa.CaraterInalterado)
        {
            return (etapa, []);
        }

        EtapaProcessoInput declarada = etapa.Declarada;
        TipoEtapaView? tipo = await _tiposAtivos.ObterAsync(declarada.TipoEtapaOrigemId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return (etapa, etapa.VinculoInalterado ? ErrosContraOTipoCongelado(etapa, indice) : [RecusaDoTipoInativo(declarada, indice)]);
        }

        return (etapa with { TipoLido = tipo },
            EscritaDasEtapasDoProcesso.ErrosDeCarater(declarada.Carater, tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.Nome, indice));
    }

    // Vínculo inalterado e tipo desde então desativado: recusar puniria quem só quer corrigir o caráter
    // de uma etapa que já existia, e o que o tipo admitia continua conhecido no snapshot da etapa.
    private static IReadOnlyList<FieldError> ErrosContraOTipoCongelado(EtapaConferida etapa, int indice)
    {
        TipoEtapaSnapshot congelado = etapa.Existente!.TipoEtapa;
        return EscritaDasEtapasDoProcesso.ErrosDeCarater(
            etapa.Declarada.Carater, congelado.AdmitePontuacao, congelado.AdmiteEliminacao, congelado.Nome, indice);
    }

    private static FieldError RecusaDoTipoInativo(EtapaProcessoInput etapa, int indice) =>
        new($"etapas[{indice}].tipoEtapaOrigemId", new DomainError(
            "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo",
            $"Tipo de etapa {etapa.TipoEtapaOrigemId} não encontrado ou não está ativo."));
}
