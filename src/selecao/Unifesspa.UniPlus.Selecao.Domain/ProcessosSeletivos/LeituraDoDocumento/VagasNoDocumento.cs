namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// As vagas como o documento as traz, copiadas sem conferir regra: quantas opções de curso o candidato escolhe, a distribuição de
/// cada oferta de curso, a cascata de remanejamento, o Fator de Atenuação das Desigualdades de Acesso e o atendimento especializado.
/// Cada item de lista guarda a posição que tinha na lista enviada e cada código de cadastro vai como foi escrito. O bloco ausente é
/// nulo (ou a lista vazia), e quer dizer que o processo ainda não o declarou.
/// </summary>
public sealed record VagasNoDocumento(int? OpcoesDeCurso, IReadOnlyList<DistribuicaoNoDocumento> Distribuicoes)
{
    public CascataNoDocumento? Cascata { get; init; }

    public FadaNoDocumento? Fada { get; init; }

    public AtendimentoNoDocumento? Atendimento { get; init; }
}

/// <summary>
/// A distribuição de vagas de uma oferta de curso, com a posição dela na lista enviada. O quadro de vagas que a exportação traz é só
/// de leitura e não entra: o sistema o calcula.
/// </summary>
public sealed record DistribuicaoNoDocumento(
    int PosicaoNoDocumento,
    string? OfertaCursoCodigo,
    int? VagasOfertadasBase,
    IReadOnlyList<ModalidadeNoDocumento> Modalidades)
{
    public decimal? PercentualDaReservaLegal { get; init; }

    public ReferenciaDeRegraNoDocumento? RegraDeDistribuicao { get; init; }

    public ReferenciaDeRegraNoDocumento? RegraDeAjuste { get; init; }

    public AjusteDoQuadroNoDocumento? AjusteDoQuadro { get; init; }

    public string? ReferenciaDemograficaCodigo { get; init; }
}

/// <summary>Uma modalidade da distribuição, com a posição dela na lista enviada e a quantidade que o edital declarou, quando declarou.</summary>
public sealed record ModalidadeNoDocumento(int PosicaoNoDocumento, string? Codigo, int? QuantidadeDeclarada);

/// <summary>
/// O parâmetro do ajuste do quadro como o documento o traz: o tipo, a modalidade da qual se reduz o excesso e as modalidades que o
/// repartem; cada tipo traz só o seu.
/// </summary>
public sealed record AjusteDoQuadroNoDocumento(string? Tipo, string? ModalidadeCodigo, IReadOnlyList<string> ModalidadeCodigos);

/// <summary>A cascata de remanejamento entre modalidades: a regra, o destino final e os destinos, cada um com a posição que tinha na lista enviada.</summary>
public sealed record CascataNoDocumento(
    ReferenciaDeRegraNoDocumento? Regra,
    string? DestinoFinalCodigo,
    IReadOnlyList<DestinoDaCascataNoDocumento> Destinos)
{
    /// <summary>
    /// Cada destino com a ordem dele entre os da mesma origem, a partir de 1: o documento não tem ordem escrita, e a posição na lista
    /// é que a define.
    /// </summary>
    public IReadOnlyList<(DestinoDaCascataNoDocumento Destino, int Ordem)> DestinosComOrdem()
    {
        Dictionary<string, int> vistosPorOrigem = new(StringComparer.Ordinal);

        return [.. Destinos.OrderBy(static destino => destino.PosicaoNoDocumento).Select(destino => (destino, OrdemNaOrigem(vistosPorOrigem, destino.ModalidadeOrigemCodigo)))];
    }

    private static int OrdemNaOrigem(Dictionary<string, int> vistosPorOrigem, string? origem)
    {
        string chave = origem ?? string.Empty;
        vistosPorOrigem[chave] = vistosPorOrigem.GetValueOrDefault(chave) + 1;

        return vistosPorOrigem[chave];
    }
}

/// <summary>Um destino da cascata, com a posição dele na lista enviada: a modalidade de origem e a de destino, como foram escritas.</summary>
public sealed record DestinoDaCascataNoDocumento(int PosicaoNoDocumento, string? ModalidadeOrigemCodigo, string? ModalidadeDestinoCodigo);
