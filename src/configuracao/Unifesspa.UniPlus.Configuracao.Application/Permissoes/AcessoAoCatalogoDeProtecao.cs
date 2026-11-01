namespace Unifesspa.UniPlus.Configuracao.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que quem age pode no circuito de aprovação da proteção de dados do catálogo: manter os tipos de
/// documento, manter os fatos e aprovar. A solicitação e a consulta alcançam os dois cadastros de uma vez, então
/// o acesso é decidido item a item por permissão, e não por um único atributo no endpoint. Quem decide é o próprio
/// handler: nenhum emissor consegue entregar o acesso pronto.
/// </summary>
public readonly record struct AcessoAoCatalogoDeProtecao(bool ManterTipos, bool ManterFatos, bool Aprovar)
{
    public bool Algum => ManterTipos || ManterFatos || Aprovar;

    public static DomainError SemPermissao { get; } = new(
        ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo,
        "Sem permissão para manter os tipos de documento, manter os fatos ou aprovar a proteção de dados do catálogo.");

    public static DomainError IdentidadeIncompleta { get; } = new(
        ProtecaoDoCatalogoErrorCodes.IdentidadeIncompleta,
        "O token não identifica o solicitante por completo; autentique-se de novo.");

    /// <summary>
    /// Avalia as três permissões sem registrar e, se nenhuma delas autoriza o pedido, grava as negativas pela política de
    /// registro. O token que não identifica o solicitante recusa com a identidade incompleta, e não com a falta de
    /// permissão.
    /// </summary>
    public static async Task<Result<AcessoAoCatalogoDeProtecao>> VerificarAsync(
        IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso tipos = await AvaliarAsync(verificador, PermissaoConfiguracao.TiposDocumentoManter, ResourceTiposConfiguracao.TipoDocumento, cancellationToken).ConfigureAwait(false);
        if (tipos is SemSujeito)
        {
            return Result<AcessoAoCatalogoDeProtecao>.Failure(IdentidadeIncompleta);
        }

        DecisaoDeAcesso fatos = await AvaliarAsync(verificador, PermissaoConfiguracao.FatosCandidatoManter, ResourceTiposConfiguracao.FatoCandidato, cancellationToken).ConfigureAwait(false);
        DecisaoDeAcesso aprovar = await AvaliarAsync(verificador, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar, ResourceTiposConfiguracao.ProtecaoDoCatalogo, cancellationToken).ConfigureAwait(false);

        AcessoAoCatalogoDeProtecao acesso = new(tipos is DecisaoPermitida, fatos is DecisaoPermitida, aprovar is DecisaoPermitida);
        if (!acesso.Algum)
        {
            await verificador.RegistrarDesfechoAsync([tipos, fatos, aprovar], IntencaoDoRegistro.Pedido, cancellationToken).ConfigureAwait(false);
        }

        return Result<AcessoAoCatalogoDeProtecao>.Success(acesso);
    }

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissaoConfiguracao permissao, string resourceTipo, CancellationToken cancellationToken)
    {
        PermissionRequirement requisito = permissao.Requisito;
        Result<ResourceContext> resource = ResourceContext.From(resourceTipo, requisito.Sensibilidade);

        return verificador.AvaliarAsync(requisito, resource.Value, cancellationToken);
    }
}
