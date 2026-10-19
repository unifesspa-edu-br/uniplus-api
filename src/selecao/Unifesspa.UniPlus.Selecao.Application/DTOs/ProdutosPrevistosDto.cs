namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Onde um item que o edital declara publicar está no registro dos atos do processo.</summary>
public enum SituacaoDoProdutoPrevisto
{
    /// <summary>Nenhum ato do processo cumpre o item.</summary>
    Aberto,

    /// <summary>O ato foi enviado ao registro central e a confirmação ainda não chegou.</summary>
    Pendente,

    /// <summary>O registro central confirmou o ato.</summary>
    Publicado,
}

/// <summary>Por que um item que o edital declara publicar não pode ser publicado agora.</summary>
public enum ImpedimentoDoProduto
{
    /// <summary>O item já foi publicado: corrigi-lo é uma errata ou retificação do ato.</summary>
    JaPublicado,

    /// <summary>O item foi enviado e aguarda a confirmação do registro.</summary>
    AguardaConfirmacaoDoRegistro,

    /// <summary>O definitivo de uma matéria só sai depois do preliminar dela, que ainda não foi publicado.</summary>
    PreliminarNaoPublicado,

    /// <summary>O preliminar da matéria foi enviado e aguarda a confirmação do registro.</summary>
    PreliminarAguardaConfirmacao,

    /// <summary>O catálogo de Publicações não tem versão vigente do tipo do ato.</summary>
    TipoDeAtoNaoVigente,
}

/// <summary>O motivo pelo qual o item não pode ser publicado agora.</summary>
/// <param name="Motivo">O motivo, em vocabulário fechado.</param>
/// <param name="Mensagem">O motivo, em texto para quem opera.</param>
public sealed record ImpedimentoDoProdutoDto(ImpedimentoDoProduto Motivo, string Mensagem);

/// <summary>
/// Um item que o edital declara publicar, congelado com um <c>Id</c> próprio, e a situação dele no
/// registro dos atos do processo.
/// </summary>
/// <param name="Id">Identificador congelado do produto na versão vigente.</param>
/// <param name="TipoCodigo">Código do tipo do ato que o item publica.</param>
/// <param name="TipoNome">Nome do tipo do ato; nulo quando o catálogo não o tem vigente e nenhum ato o registrou.</param>
/// <param name="Papel">Token canônico (<c>PRELIMINAR</c> abre o prazo de recurso, <c>DEFINITIVO</c> o encerra); nulo quando o ato não é resultado.</param>
/// <param name="Situacao">Aberto, pendente ou publicado.</param>
/// <param name="Ato">O ato que cumpre o item (a primeira publicação); nulo enquanto aberto.</param>
/// <param name="Correcoes">Os atos que corrigem o ato do item, do mais antigo para o mais recente.</param>
/// <param name="PodePublicar">Se o item pode ser publicado agora.</param>
/// <param name="Impedimento">O motivo de não poder; nulo quando pode.</param>
public sealed record ProdutoPrevistoDto(
    Guid Id,
    string TipoCodigo,
    string? TipoNome,
    string? Papel,
    SituacaoDoProdutoPrevisto Situacao,
    AtoDoProcessoDto? Ato,
    IReadOnlyList<AtoDoProcessoDto> Correcoes,
    bool PodePublicar,
    ImpedimentoDoProdutoDto? Impedimento);

/// <summary>Os itens previstos de uma etapa.</summary>
public sealed record EtapaComProdutosPrevistosDto(
    Guid Id,
    string Nome,
    int? Ordem,
    IReadOnlyList<ProdutoPrevistoDto> Produtos);

/// <summary>Os itens previstos de uma fase do cronograma e os das etapas dela.</summary>
public sealed record FaseComProdutosPrevistosDto(
    Guid Id,
    string Codigo,
    int Ordem,
    IReadOnlyList<ProdutoPrevistoDto> Produtos,
    IReadOnlyList<EtapaComProdutosPrevistosDto> Etapas);

/// <summary>
/// Os itens que a versão vigente do edital declara publicar, agrupados pela fase do cronograma.
/// </summary>
/// <param name="NumeroDaVersao">Número da versão vigente de onde vêm as declarações.</param>
/// <param name="PoliticaDeDivulgacao">
/// A política de divulgação do certame congelada na versão vigente — o que o administrador vê e confirma
/// antes de publicar um resultado.
/// </param>
/// <param name="Fases">As fases do cronograma, em ordem.</param>
/// <param name="EtapasSemFase">As etapas que não declaram fase, quando o edital as tem.</param>
public sealed record ProdutosPrevistosDoProcessoDto(
    int NumeroDaVersao,
    ConfiguracaoDivulgacaoDto PoliticaDeDivulgacao,
    IReadOnlyList<FaseComProdutosPrevistosDto> Fases,
    IReadOnlyList<EtapaComProdutosPrevistosDto> EtapasSemFase);
