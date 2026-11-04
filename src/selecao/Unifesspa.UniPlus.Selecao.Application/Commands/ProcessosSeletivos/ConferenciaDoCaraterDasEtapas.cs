namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
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
/// O snapshot do tipo só é lido de novo quando o vínculo é novo ou muda de tipo, ou quando muda o caráter
/// da etapa; do contrário, renomear um tipo ativo reescreveria o snapshot que o cliente nem tocou
/// (ADR-0061).
/// </remarks>
internal sealed class ConferenciaDoCaraterDasEtapas(
    ITipoEtapaReader tipoEtapaReader,
    IReadOnlyDictionary<Guid, EtapaProcesso> existentes)
{
    private readonly Dictionary<Guid, TipoEtapaView> _tiposAtivos = [];
    private readonly Dictionary<int, TipoEtapaView> _sinalizadoresRelidos = [];

    public async Task<Result<TiposDasEtapasResolvidos>> ConferirAsync(
        IReadOnlyList<EtapaProcessoInput> etapas, CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            erros.AddRange(await ConferirEtapaAsync(etapas[indice], indice, cancellationToken).ConfigureAwait(false));
        }

        return erros.Count > 0
            ? Result<TiposDasEtapasResolvidos>.ValidationFailure(erros)
            : Result<TiposDasEtapasResolvidos>.Success(new TiposDasEtapasResolvidos(_tiposAtivos, _sinalizadoresRelidos));
    }

    private async Task<IReadOnlyList<FieldError>> ConferirEtapaAsync(
        EtapaProcessoInput etapa, int indice, CancellationToken cancellationToken)
    {
        EtapaProcesso? existente = EtapaExistente(etapa);
        bool vinculoInalterado = existente?.TipoEtapaOrigemId == etapa.TipoEtapaOrigemId;
        if (vinculoInalterado && existente!.Carater == etapa.Carater)
        {
            return [];
        }

        TipoEtapaView? tipo = await TipoAtivoAsync(etapa.TipoEtapaOrigemId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return vinculoInalterado
                ? ErrosContraOTipoCongelado(etapa, existente!, indice)
                : [RecusaDoTipoInativo(etapa, indice)];
        }

        if (vinculoInalterado)
        {
            _sinalizadoresRelidos[indice] = tipo;
        }

        return EtapaDoPayload.ErrosDeCarater(etapa.Carater, tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.Nome, indice);
    }

    private EtapaProcesso? EtapaExistente(EtapaProcessoInput etapa) =>
        etapa.Id is { } id && existentes.TryGetValue(id, out EtapaProcesso? existente) ? existente : null;

    private async Task<TipoEtapaView?> TipoAtivoAsync(Guid tipoEtapaId, CancellationToken cancellationToken)
    {
        if (_tiposAtivos.TryGetValue(tipoEtapaId, out TipoEtapaView? emCache))
        {
            return emCache;
        }

        TipoEtapaView? lido = await tipoEtapaReader.ObterAtivoPorIdAsync(tipoEtapaId, cancellationToken).ConfigureAwait(false);
        if (lido is not null)
        {
            _tiposAtivos[tipoEtapaId] = lido;
        }

        return lido;
    }

    // Vínculo inalterado e tipo desde então desativado: recusar puniria quem só quer corrigir o caráter
    // de uma etapa que já existia, e o que o tipo admitia continua conhecido no snapshot da etapa.
    private static IReadOnlyList<FieldError> ErrosContraOTipoCongelado(
        EtapaProcessoInput etapa, EtapaProcesso existente, int indice)
    {
        TipoEtapaSnapshot congelado = existente.TipoEtapa;
        return EtapaDoPayload.ErrosDeCarater(
            etapa.Carater, congelado.AdmitePontuacao, congelado.AdmiteEliminacao, congelado.Nome, indice);
    }

    private static FieldError RecusaDoTipoInativo(EtapaProcessoInput etapa, int indice) =>
        new($"etapas[{indice}].tipoEtapaOrigemId", new DomainError(
            "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo",
            $"Tipo de etapa {etapa.TipoEtapaOrigemId} não encontrado ou não está ativo."));
}

/// <summary>
/// O que a conferência do caráter leu do cadastro: os tipos ativos por identidade — dado bruto, nunca a
/// instância de snapshot, que o EF não aceita em dois donos — e, por posição no payload, o tipo relido
/// cujos sinalizadores a etapa existente passa a congelar.
/// </summary>
internal sealed record TiposDasEtapasResolvidos(
    IReadOnlyDictionary<Guid, TipoEtapaView> Ativos,
    IReadOnlyDictionary<int, TipoEtapaView> SinalizadoresRelidos);
