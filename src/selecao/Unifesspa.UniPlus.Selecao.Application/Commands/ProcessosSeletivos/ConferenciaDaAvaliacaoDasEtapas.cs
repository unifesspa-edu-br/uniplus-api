namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Resolve a forma de avaliação e a regra de consolidação de cada etapa, antes de qualquer mutação. A forma
/// é um token do contrato e o desconhecido é recusado, nunca lido como ausente: um erro de digitação
/// viraria etapa sem forma, que só o gate de publicação acusaria, longe de quem o cometeu. A regra é
/// montada a partir dos valores RESOLVIDOS do catálogo, e não ecoados do payload, para o hash bater por
/// construção.
/// </summary>
/// <remarks>
/// As coerências que independem do catálogo rodam para TODAS as etapas antes de qualquer leitura dele, e
/// acumulam entre as etapas (ADR-0125).
/// </remarks>
internal sealed class ConferenciaDaAvaliacaoDasEtapas(LeituraMemoizada<(string Codigo, string Versao), RegraCatalogo> regras)
{
    public async Task<Result<List<EtapaConferida>>> ConferirAsync(
        IReadOnlyList<EtapaConferida> etapas, CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        List<DeclaracaoDaAvaliacao> declaracoes = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            (DeclaracaoDaAvaliacao declaracao, IReadOnlyList<FieldError> recusas) = Declarar(etapas[indice], indice);
            declaracoes.Add(declaracao);
            erros.AddRange(recusas);
        }

        if (erros.Count > 0)
        {
            return Result<List<EtapaConferida>>.ValidationFailure(erros);
        }

        List<EtapaConferida> conferidas = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            (EtapaConferida conferida, IReadOnlyList<FieldError> recusas) =
                await ComConsolidacaoAsync(etapas[indice], declaracoes[indice], indice, cancellationToken).ConfigureAwait(false);
            conferidas.Add(conferida);
            erros.AddRange(recusas);
        }

        return erros.Count > 0
            ? Result<List<EtapaConferida>>.ValidationFailure(erros)
            : Result<List<EtapaConferida>>.Success(conferidas);
    }

    private static (DeclaracaoDaAvaliacao Declaracao, IReadOnlyList<FieldError> Recusas) Declarar(EtapaConferida etapa, int indice)
    {
        EtapaProcessoInput declarada = etapa.Declarada;
        List<FieldError> erros = [];
        bool formaEntendida = FormaDeAvaliacaoCodigo.TentarConverter(declarada.FormaDeAvaliacao, out FormaDeAvaliacao? forma);
        if (!formaEntendida)
        {
            erros.Add(RecusaDaFormaDesconhecida(declarada));
        }

        (string? codigo, string? versao) = (declarada.RegraConsolidacaoCodigo?.Trim(), declarada.RegraConsolidacaoVersao?.Trim());
        bool temCodigo = !string.IsNullOrEmpty(codigo);
        bool temVersao = !string.IsNullOrEmpty(versao);
        if (temCodigo != temVersao)
        {
            erros.Add(RecusaDoParIncompleto(declarada, temCodigo));
        }

        // Só com a forma já entendida: forma desconhecida não vira também "regra fora da forma humana".
        if (formaEntendida)
        {
            erros.AddRange(ValidarAvaliacao(etapa, forma, temCodigo && temVersao ? codigo : null));
        }

        (string, string)? regra = erros.Count == 0 && temCodigo && temVersao ? (codigo!, versao!) : null;
        return (new DeclaracaoDaAvaliacao(forma, regra), EscritaDasEtapasDoProcesso.ComIndice(erros, indice));
    }

    private static List<FieldError> ValidarAvaliacao(EtapaConferida etapa, FormaDeAvaliacao? forma, string? regraConsolidacaoCodigo) =>
        EtapaProcesso.ValidarAvaliacao(
            etapa.NotaDeOrigemNoEnem,
            etapa.Declarada.PassoDoLancamento,
            forma,
            regraConsolidacaoCodigo,
            declaraItens: etapa.Declarada.ItensDeAvaliacao is { Count: > 0 },
            declaraEscala: etapa.Declarada.EscalaDeAtendimento is { Count: > 0 });

    // A referência da regra sai do catálogo; a etapa sem regra declarada fica sem consolidação.
    private async Task<(EtapaConferida Etapa, IReadOnlyList<FieldError> Recusas)> ComConsolidacaoAsync(
        EtapaConferida etapa, DeclaracaoDaAvaliacao declaracao, int indice, CancellationToken cancellationToken)
    {
        EtapaConferida comForma = etapa with { Forma = declaracao.Forma };
        if (declaracao.Regra is not { } par)
        {
            return (comForma, []);
        }

        RegraCatalogo? regra = await regras.ObterAsync(par, cancellationToken).ConfigureAwait(false);
        Result<ReferenciaRegra> referencia = ReferenciaDeConsolidacao(etapa.Declarada, par.Codigo, par.Versao, regra);
        return referencia.IsSuccess
            ? (comForma with { Consolidacao = referencia.Value }, [])
            : (comForma, EscritaDasEtapasDoProcesso.ComIndice(referencia.Errors, indice));
    }

    private static Result<ReferenciaRegra> ReferenciaDeConsolidacao(
        EtapaProcessoInput etapa, string codigo, string versao, RegraCatalogo? regra)
    {
        if (regra is null || regra.Tipo != TipoRegra.RegraConsolidacao)
        {
            return Result<ReferenciaRegra>.ValidationFailure([new("regraConsolidacao", new DomainError(
                EtapaProcesso.RegraDeConsolidacaoInvalida,
                $"A regra {codigo}/{versao}, declarada pela etapa '{etapa.Nome}', não é uma regra de consolidação do catálogo."))]);
        }

        Result<ReferenciaRegra> referencia = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        return referencia.IsSuccess
            ? referencia
            : Result<ReferenciaRegra>.ValidationFailure(
                [.. referencia.Errors.Select(erro => new FieldError("regraConsolidacao", erro.Error))]);
    }

    private static FieldError RecusaDaFormaDesconhecida(EtapaProcessoInput etapa) =>
        new("formaDeAvaliacao", new DomainError(
            EtapaProcesso.FormaDeAvaliacaoDesconhecida,
            $"A forma de avaliação '{etapa.FormaDeAvaliacao}', declarada pela etapa '{etapa.Nome}', não existe — use '{FormaDeAvaliacaoCodigo.Objetiva}' ou '{FormaDeAvaliacaoCodigo.Humana}'."));

    private static FieldError RecusaDoParIncompleto(EtapaProcessoInput etapa, bool temCodigo) =>
        new("regraConsolidacao", new DomainError(
            EtapaProcesso.RegraDeConsolidacaoInvalida,
            $"A regra de consolidação da etapa '{etapa.Nome}' precisa de código e versão juntos: falta {(temCodigo ? "a versão" : "o código")}."));

    private readonly record struct DeclaracaoDaAvaliacao(FormaDeAvaliacao? Forma, (string Codigo, string Versao)? Regra);
}
