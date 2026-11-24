namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ProcessosSeletivos.Etapas;
using Domain.ValueObjects;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

using Services;

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
        (List<DeclaracaoDaAvaliacao> declaracoes, List<FieldError> erros) = DeclararEtapas(etapas);
        if (erros.Count > 0)
        {
            return Result<List<EtapaConferida>>.ValidationFailure(erros);
        }

        return await ConferirConsolidacoesAsync(etapas, declaracoes, cancellationToken).ConfigureAwait(false);
    }

    private static (List<DeclaracaoDaAvaliacao>, List<FieldError>) DeclararEtapas(IReadOnlyList<EtapaConferida> etapas)
    {
        List<FieldError> erros = [];
        List<DeclaracaoDaAvaliacao> declaracoes = [];
        for (int indice = 0; indice < etapas.Count; indice++)
        {
            (DeclaracaoDaAvaliacao declaracao, IReadOnlyList<FieldError> recusas) = Declarar(etapas[indice], indice);
            declaracoes.Add(declaracao);
            erros.AddRange(recusas);
        }

        return (declaracoes, erros);
    }

    private async Task<Result<List<EtapaConferida>>> ConferirConsolidacoesAsync(
        IReadOnlyList<EtapaConferida> etapas, List<DeclaracaoDaAvaliacao> declaracoes, CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
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
        bool formaEntendida = CodigoLidoDoPedido.TentarLerOpcional(declarada.FormaDeAvaliacao, out FormaDeAvaliacao? forma);
        RegistrarFormaDesconhecida(declarada, formaEntendida, erros);
        (string? codigo, string? versao) = ConferirPar(declarada, erros);
        if (formaEntendida)
        {
            erros.AddRange(ValidarAvaliacao(etapa, forma, codigo));
        }

        (string, string)? regra = erros.Count == 0 && codigo is not null ? (codigo, versao!) : null;
        return (new DeclaracaoDaAvaliacao(forma, regra), EscritaDasEtapasDoProcesso.ComIndice(erros, indice));
    }

    private static void RegistrarFormaDesconhecida(EtapaProcessoInput declarada, bool formaEntendida, List<FieldError> erros)
    {
        if (!formaEntendida)
        {
            erros.Add(RecusaDaFormaDesconhecida(declarada));
        }
    }

    private static (string? Codigo, string? Versao) ConferirPar(EtapaProcessoInput declarada, List<FieldError> erros)
    {
        string? codigo = declarada.RegraConsolidacaoCodigo?.Trim();
        string? versao = declarada.RegraConsolidacaoVersao?.Trim();
        bool temCodigo = !string.IsNullOrEmpty(codigo);
        bool temVersao = !string.IsNullOrEmpty(versao);
        if (temCodigo != temVersao)
        {
            erros.Add(RecusaDoParIncompleto(declarada, temCodigo));
        }

        return temCodigo && temVersao ? (codigo, versao) : (null, null);
    }

    private static List<FieldError> ValidarAvaliacao(EtapaConferida etapa, FormaDeAvaliacao? forma, string? regraConsolidacaoCodigo) =>
        CoerenciaDaAvaliacaoDaEtapa.Validar(new AvaliacaoParaCoerencia(
            etapa.NotaDeOrigemNoEnem,
            forma,
            regraConsolidacaoCodigo,
            etapa.Declarada.PassoDoLancamento,
            DeclaraItens: etapa.Declarada.ItensDeAvaliacao is { Count: > 0 },
            DeclaraEscala: etapa.Declarada.EscalaDeAtendimento is { Count: > 0 },
            ExigeCorrecaoIndependente: etapa.ExigeCorrecaoIndependente));

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
        if (regra is null || regra.Tipo != TipoRegra.RegraApuracaoAvaliacao)
        {
            return Result<ReferenciaRegra>.ValidationFailure([new("regraConsolidacao", new DomainError(
                EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida,
                $"A regra {codigo}/{versao}, declarada pela etapa '{etapa.Nome}', não é uma regra de consolidação do catálogo."))]);
        }

        return Result<ReferenciaRegra>.Success(regra.Referencia());
    }

    private static FieldError RecusaDaFormaDesconhecida(EtapaProcessoInput etapa) =>
        new("formaDeAvaliacao", new DomainError(
            EtapaProcessoErrorCodes.FormaDeAvaliacaoDesconhecida,
            $"A forma de avaliação declarada pela etapa '{etapa.Nome}' não existe — use {string.Join(" ou ", CatalogoDe<FormaDeAvaliacao>.Todos().Select(static declarado => $"'{declarado.Codigo}'"))}."));

    private static FieldError RecusaDoParIncompleto(EtapaProcessoInput etapa, bool temCodigo) =>
        new("regraConsolidacao", new DomainError(
            EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida,
            $"A regra de consolidação da etapa '{etapa.Nome}' precisa de código e versão juntos: falta {(temCodigo ? "a versão" : "o código")}."));

    private readonly record struct DeclaracaoDaAvaliacao(FormaDeAvaliacao? Forma, (string Codigo, string Versao)? Regra);
}
