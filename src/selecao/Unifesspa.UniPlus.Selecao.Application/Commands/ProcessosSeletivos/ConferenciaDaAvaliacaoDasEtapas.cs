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
internal sealed class ConferenciaDaAvaliacaoDasEtapas(
    IReadOnlyDictionary<Guid, EtapaProcesso> existentes,
    TiposDasEtapasResolvidos tipos,
    LeituraMemoizada<(string Codigo, string Versao), RegraCatalogo> regras)
{
    private readonly Dictionary<int, FormaDeAvaliacao?> _formas = [];
    private readonly Dictionary<int, (string Codigo, string Versao)> _regrasDeclaradas = [];

    public async Task<Result<AvaliacaoDasEtapasResolvida>> ConferirAsync(
        IReadOnlyList<EtapaProcessoInput> etapas, CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            erros.AddRange(ConferirDeclaracao(etapas[indice], indice));
        }

        Dictionary<int, ReferenciaRegra> consolidacoes = [];
        if (erros.Count == 0)
        {
            erros.AddRange(await ResolverConsolidacoesAsync(etapas, consolidacoes, cancellationToken).ConfigureAwait(false));
        }

        return erros.Count > 0
            ? Result<AvaliacaoDasEtapasResolvida>.ValidationFailure(erros)
            : Result<AvaliacaoDasEtapasResolvida>.Success(new AvaliacaoDasEtapasResolvida(_formas, consolidacoes));
    }

    private IReadOnlyList<FieldError> ConferirDeclaracao(EtapaProcessoInput etapa, int indice)
    {
        List<FieldError> erros = [];
        if (FormaDeAvaliacaoCodigo.TentarConverter(etapa.FormaDeAvaliacao, out FormaDeAvaliacao? forma))
        {
            _formas[indice] = forma;
        }
        else
        {
            erros.Add(RecusaDaFormaDesconhecida(etapa));
        }

        (string? codigo, string? versao) = (etapa.RegraConsolidacaoCodigo?.Trim(), etapa.RegraConsolidacaoVersao?.Trim());
        bool temCodigo = !string.IsNullOrEmpty(codigo);
        bool temVersao = !string.IsNullOrEmpty(versao);
        if (temCodigo != temVersao)
        {
            erros.Add(RecusaDoParIncompleto(etapa, temCodigo));
        }

        // Só com a forma já entendida: forma desconhecida não vira também "regra fora da forma humana".
        if (_formas.ContainsKey(indice))
        {
            erros.AddRange(ValidarAvaliacao(etapa, indice, forma, temCodigo && temVersao ? codigo : null));
        }

        if (erros.Count == 0 && temCodigo && temVersao)
        {
            _regrasDeclaradas[indice] = (codigo!, versao!);
        }

        return EtapaDoPayload.ComIndice(erros, indice);
    }

    private List<FieldError> ValidarAvaliacao(
        EtapaProcessoInput etapa, int indice, FormaDeAvaliacao? forma, string? regraConsolidacaoCodigo) =>
        EtapaProcesso.ValidarAvaliacao(
            NotaDeOrigemNoEnem(etapa),
            etapa.PassoDoLancamento,
            forma,
            regraConsolidacaoCodigo,
            declaraItens: etapa.ItensDeAvaliacao is { Count: > 0 },
            declaraEscala: etapa.EscalaDeAtendimento is { Count: > 0 });

    // A nota vem do ENEM pelo snapshot congelado quando o vínculo com o tipo não mudou, e pelo tipo lido na
    // passada do caráter quando o vínculo é novo ou alterado.
    private bool NotaDeOrigemNoEnem(EtapaProcessoInput etapa) =>
        etapa.Id is { } id
        && existentes.TryGetValue(id, out EtapaProcesso? atual)
        && atual.TipoEtapaOrigemId == etapa.TipoEtapaOrigemId
            ? atual.TipoEtapa.NotaDeOrigemNoEnem
            : tipos.Ativos[etapa.TipoEtapaOrigemId].NotaDeOrigemNoEnem;

    private async Task<List<FieldError>> ResolverConsolidacoesAsync(
        IReadOnlyList<EtapaProcessoInput> etapas,
        Dictionary<int, ReferenciaRegra> consolidacoes,
        CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        foreach ((int indice, (string codigo, string versao)) in _regrasDeclaradas)
        {
            RegraCatalogo? regra = await regras.ObterAsync((codigo, versao), cancellationToken).ConfigureAwait(false);
            Result<ReferenciaRegra> referencia = ReferenciaDeConsolidacao(etapas[indice], codigo, versao, regra);
            if (referencia.IsSuccess)
            {
                consolidacoes[indice] = referencia.Value!;
            }
            else
            {
                erros.AddRange(EtapaDoPayload.ComIndice(referencia.Errors, indice));
            }
        }

        return erros;
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
}

/// <summary>
/// A avaliação das etapas depois de conferida contra o catálogo, por posição no payload: a forma de
/// avaliação e a referência da regra de consolidação, quando a etapa declara uma.
/// </summary>
internal sealed record AvaliacaoDasEtapasResolvida(
    IReadOnlyDictionary<int, FormaDeAvaliacao?> Formas,
    IReadOnlyDictionary<int, ReferenciaRegra> Consolidacoes);
