namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que quem age pode no circuito de aprovação da proteção de dados do catálogo: manter os tipos de
/// documento, manter os fatos e aprovar. A solicitação e a consulta alcançam os dois cadastros de uma vez, então
/// o acesso é decidido item a item por permissão, e não por um único atributo no endpoint. A decisão é tomada na borda,
/// antes do handler: ela lê as concessões do módulo Autorização, e o handler precisa de um único DbContext para que a
/// trava, a conferência, a gravação e o aviso por e-mail fiquem na mesma transação.
/// </summary>
public readonly record struct AcessoAoCatalogoDeProtecao(bool ManterTipos, bool ManterFatos, bool Aprovar)
{
    public bool Algum => ManterTipos || ManterFatos || Aprovar;

    public static DomainError SemPermissao { get; } = new(
        ProtecaoDoCatalogoErrorCodes.SemPermissaoNoCatalogo,
        "Sem permissão para manter os tipos de documento, manter os fatos ou aprovar a proteção de dados do catálogo.");

    public static async Task<AcessoAoCatalogoDeProtecao> VerificarAsync(
        IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return new AcessoAoCatalogoDeProtecao(
            await TemAsync(verificador, PermissaoConfiguracao.TiposDocumentoManter, ResourceTiposConfiguracao.TipoDocumento, cancellationToken).ConfigureAwait(false),
            await TemAsync(verificador, PermissaoConfiguracao.FatosCandidatoManter, ResourceTiposConfiguracao.FatoCandidato, cancellationToken).ConfigureAwait(false),
            await TemAsync(verificador, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar, ResourceTiposConfiguracao.ProtecaoDoCatalogo, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<bool> TemAsync(
        IVerificadorDeAcesso verificador, PermissaoConfiguracao permissao, string resourceTipo, CancellationToken cancellationToken)
    {
        PermissionRequirement requisito = permissao.Requisito;
        Result<ResourceContext> resource = ResourceContext.From(resourceTipo, requisito.Sensibilidade);

        return await verificador.VerificarAsync(requisito, resource.Value, cancellationToken).ConfigureAwait(false)
            == ResultadoDoAcesso.Permitido;
    }
}
