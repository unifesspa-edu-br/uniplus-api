namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// A referência à versão do objeto, obtida pela conferência pública da declaração do ato, que é
/// quem a cria: o valor só existe com o par completo e aceito.
/// </summary>
internal static class VersaoDoObjetoDeTeste
{
    internal static ReferenciaVersaoDoObjeto Criar(Guid id, string hash) =>
        ConferenciaDaDeclaracaoDoAto.De(new DeclaracaoDoAtoNormativo
        {
            Numeracao = new NumeracaoDoAto("CEPS", "EDITAL", 2026, "13"),
            TipoCodigo = "EDITAL_ABERTURA",
            DocumentoHash = AtoNormativoDeTeste.HashValido,
            Assinante = PersonasDeTeste.NomeKevinCarvalho,
            VersaoDoObjetoId = id,
            VersaoDoObjetoHash = hash,
            Retificacao = default,
            Vinculos = [],
            Anexos = [],
        }).VersaoDoObjeto.Valor!;
}
