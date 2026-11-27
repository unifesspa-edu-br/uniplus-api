namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O erro de item inexistente na leitura da proteção, conforme o cadastro em que o item foi procurado.</summary>
internal static class ItemDaProtecaoNaoEncontrado
{
    private static readonly DomainError TipoDeDocumento = new(
        TipoDocumentoErrorCodes.NaoEncontrado,
        "Tipo de documento não encontrado.");

    private static readonly DomainError DadoDoCandidato = new(
        FatoCandidatoErrorCodes.NaoEncontrado,
        "Dado do candidato não encontrado.");

    internal static DomainError Do(CadastroDaProtecaoDoCatalogo cadastro) =>
        cadastro == CadastroDaProtecaoDoCatalogo.Fato ? DadoDoCandidato : TipoDeDocumento;
}
