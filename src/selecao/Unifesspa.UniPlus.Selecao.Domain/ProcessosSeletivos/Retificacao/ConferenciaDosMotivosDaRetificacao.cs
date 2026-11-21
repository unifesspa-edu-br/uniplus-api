namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// O que a retificação faz com os motivos de decisão (UNI-REQ-0124, UNI-REQ-0285): o motivo já publicado não sai;
/// o motivo novo entra com justificativa e só enquanto a avaliação no lugar dele não começou, medida sobre a versão
/// publicada vigente. O motivo se identifica pelo lugar e pela origem no cadastro. As recusas se acumulam, uma por motivo: as retiradas e depois as inclusões, cada
/// grupo em ordem de código; na inclusão vale a justificativa antes do início da avaliação. O motivo retirado não tem posição
/// no documento novo, e a recusa aponta a coleção dos motivos; a inclusão aponta o motivo, ou a justificativa dele, pela
/// posição na lista do documento novo, que chega na ordem do documento.
/// </summary>
public static class ConferenciaDosMotivosDaRetificacao
{
    public const int JustificativaMaxLength = 1000;

    private const string MotivosNoDocumento = "motivos";

    private static readonly DomainError JustificativaObrigatoria = new(
        MotivoDeDecisaoErrorCodes.JustificativaObrigatoria,
        "Incluir motivo no processo publicado exige justificativa, de uso interno da administração.");

    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyCollection<MotivoDoDocumento> motivosDaBase,
        IReadOnlyList<MotivoDoDocumento> motivosNovos,
        AndamentoDasFases andamento)
    {
        ArgumentNullException.ThrowIfNull(motivosDaBase);
        ArgumentNullException.ThrowIfNull(motivosNovos);
        ArgumentNullException.ThrowIfNull(andamento);

        return [.. Retiradas(motivosDaBase, motivosNovos), .. Inclusoes(motivosDaBase, motivosNovos, andamento)];
    }

    private static IEnumerable<FieldError> Retiradas(IReadOnlyCollection<MotivoDoDocumento> motivosDaBase, IReadOnlyList<MotivoDoDocumento> motivosNovos)
    {
        HashSet<(LugarDaDecisao, Guid)> novos = [.. motivosNovos.Select(Identidade)];

        return motivosDaBase.Where(m => !novos.Contains(Identidade(m))).OrderBy(static m => m.Codigo, StringComparer.Ordinal).Select(Retirada);
    }

    private static IEnumerable<FieldError> Inclusoes(
        IReadOnlyCollection<MotivoDoDocumento> motivosDaBase, IReadOnlyList<MotivoDoDocumento> motivosNovos, AndamentoDasFases andamento)
    {
        HashSet<(LugarDaDecisao, Guid)> daBase = [.. motivosDaBase.Select(Identidade)];

        return motivosNovos
            .Select(static (motivo, posicao) => (Motivo: motivo, Posicao: posicao))
            .Where(par => !daBase.Contains(Identidade(par.Motivo)))
            .OrderBy(static par => par.Motivo.Codigo, StringComparer.Ordinal)
            .Select(par => Inclusao(par.Motivo, par.Posicao, andamento))
            .OfType<FieldError>();
    }

    private static (LugarDaDecisao Lugar, Guid OrigemId) Identidade(MotivoDoDocumento motivo) => (motivo.Lugar, motivo.OrigemId);

    private static FieldError Retirada(MotivoDoDocumento motivo) => new(
        MotivosNoDocumento,
        new DomainError(
            MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao,
            $"O motivo {motivo.Codigo} já consta do edital publicado e não pode ser retirado; a retificação só inclui motivos."));

    private static FieldError? Inclusao(MotivoDoDocumento motivo, int posicao, AndamentoDasFases andamento)
    {
        string item = $"{MotivosNoDocumento}[{posicao}]";

        return RecusaDaJustificativa(motivo.Justificativa) is { } daJustificativa
            ? new FieldError($"{item}.justificativaDaInclusao", daJustificativa)
            : RecusaDaAvaliacaoIniciada(motivo, andamento) is { } daAvaliacao ? new FieldError(item, daAvaliacao) : null;
    }

    private static DomainError? RecusaDaJustificativa(string? justificativa) => justificativa switch
    {
        _ when string.IsNullOrWhiteSpace(justificativa) => JustificativaObrigatoria,
        _ when ValidacaoDoMotivo.TemCaractereNulo(justificativa) => new DomainError(
            MotivoDeDecisaoErrorCodes.JustificativaCaractereInvalido, "A justificativa não pode conter o caractere nulo."),
        _ when justificativa.Trim().Length > JustificativaMaxLength => new DomainError(
            MotivoDeDecisaoErrorCodes.JustificativaTamanho, $"A justificativa deve ter até {JustificativaMaxLength} caracteres."),
        _ => null,
    };

    private static DomainError? RecusaDaAvaliacaoIniciada(MotivoDoDocumento motivo, AndamentoDasFases andamento) =>
        andamento.Fatos.FasesIniciadas.AvaliacaoComecou(motivo.Lugar)
            ? new DomainError(
                MotivoDeDecisaoErrorCodes.AvaliacaoIniciada,
                $"A avaliação de {motivo.Tipo.Codigo()} nesta fase ou etapa já começou; motivo novo não entra mais.")
            : null;
}
