namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Um documento que o pedido cita e a conferência lê: o principal ou um anexo, com o lugar dele no pedido.</summary>
/// <param name="Id">O documento citado.</param>
/// <param name="PosicaoDoAnexo">A posição na lista de anexos; nulo para o documento principal.</param>
/// <param name="NomeDoAnexo">O nome pedido para o download do anexo; nulo para o documento principal.</param>
internal sealed record DocumentoDoPedido(Guid Id, int? PosicaoDoAnexo, string? NomeDoAnexo)
{
    public bool EhPrincipal => PosicaoDoAnexo is null;

    public string Campo => PosicaoDoAnexo is { } posicao ? $"anexos[{posicao}].documentoId" : "documentoPrincipalId";

    public string CampoDoNome => $"anexos[{PosicaoDoAnexo}].nome";
}

/// <summary>
/// A forma dos documentos do pedido, conferida sem ler nada: o documento principal e o do anexo ausentes, a quantidade
/// de anexos e o documento que aparece mais de uma vez, e o que a conferência dos documentos vai ler.
/// </summary>
/// <param name="Recusas">As causas da forma, na ordem do pedido.</param>
/// <param name="ALer">
/// Os documentos a ler: a primeira ocorrência de cada um citado. Vazio quando há anexos demais, porque a leitura é limitada.
/// </param>
internal sealed record PedidoDosDocumentosDoAto(IReadOnlyList<FieldError> Recusas, IReadOnlyList<DocumentoDoPedido> ALer)
{
    public const int QuantidadeMaximaDeAnexos = 20;

    public const string CampoDosAnexos = "anexos";

    public static PedidoDosDocumentosDoAto Vazio { get; } = new([], []);

    public bool PodeLer => ALer.Count > 0;

    public static PedidoDosDocumentosDoAto De(Guid? documentoPrincipalId, IReadOnlyList<AnexoSolicitadoDoAto>? anexos)
    {
        IReadOnlyList<AnexoSolicitadoDoAto> pedidos = anexos ?? [];
        DocumentoDoPedido[] citados = [.. Citados(documentoPrincipalId, pedidos)];
        DocumentoDoPedido[] primeiras = [.. citados.DistinctBy(static d => d.Id)];
        bool anexosDemais = pedidos.Count > QuantidadeMaximaDeAnexos;
        FieldError[] recusas =
        [
            .. Ausencias(documentoPrincipalId, pedidos),
            .. RecusaDaQuantidade(anexosDemais),
            .. citados.Except(primeiras).Select(RecusaDaRepeticao),
        ];

        return new(recusas, anexosDemais ? [] : primeiras);
    }

    private static IEnumerable<DocumentoDoPedido> Citados(Guid? documentoPrincipalId, IReadOnlyList<AnexoSolicitadoDoAto> anexos)
    {
        if (documentoPrincipalId is { } principal)
        {
            yield return new(principal, null, null);
        }

        for (int posicao = 0; posicao < anexos.Count; posicao++)
        {
            if (anexos[posicao].DocumentoId is { } documento)
            {
                yield return new(documento, posicao, anexos[posicao].Nome);
            }
        }
    }

    private static IEnumerable<FieldError> Ausencias(Guid? documentoPrincipalId, IReadOnlyList<AnexoSolicitadoDoAto> anexos)
    {
        if (documentoPrincipalId is null)
        {
            yield return AusenciasNosDadosDoAto.DocumentoPrincipal();
        }

        for (int posicao = 0; posicao < anexos.Count; posicao++)
        {
            if (anexos[posicao].DocumentoId is null)
            {
                yield return AusenciasNosDadosDoAto.DocumentoDoAnexo(posicao);
            }
        }
    }

    private static IReadOnlyList<FieldError> RecusaDaQuantidade(bool anexosDemais) =>
        anexosDemais
            ? [new FieldError(CampoDosAnexos, new DomainError(AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida,
                $"O ato aceita no máximo {QuantidadeMaximaDeAnexos} anexos."))]
            : [];

    private static FieldError RecusaDaRepeticao(DocumentoDoPedido repetido) =>
        new(repetido.Campo, new DomainError(AtoDoProcessoErrorCodes.DocumentoRepetido,
            "O mesmo documento não pode constar duas vezes no ato, nem como principal e anexo."));
}
