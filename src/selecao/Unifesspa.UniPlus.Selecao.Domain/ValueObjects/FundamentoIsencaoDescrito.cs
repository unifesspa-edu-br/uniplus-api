namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um fundamento de isenção referenciável por um processo que cobra taxa (UNI-REQ-0101),
/// com o rótulo e a explicação que um cliente administrativo precisa para escolher entre
/// eles sem consultar o edital.
/// </summary>
/// <param name="Codigo">Código de wire — o mesmo valor que trafega em <c>fundamentos</c> e no envelope.</param>
/// <param name="Nome">Rótulo humano do fundamento.</param>
/// <param name="Descricao">
/// Quem o fundamento alcança, em uma frase. Deliberadamente sem citar norma: a base legal
/// do Cadastro Único está em reexame pelo Jurídico e pelo DPO (UNI-REQ-0110), e publicar
/// aqui uma referência que pode mudar faria a API afirmar como assente algo que ainda não é.
/// </param>
public sealed record FundamentoIsencaoDescrito(
    string Codigo,
    string Nome,
    string Descricao);
