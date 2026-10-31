namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que o solicitante da requisição pode fazer na revisão dos acessos: conduzi-la como proteção de dados e decidir os
/// itens dela, ou rever como gestor de acessos os itens da proteção de dados. Cada capacidade é conferida pelo ponto de
/// decisão único, por permissão do catálogo.
/// </summary>
internal sealed record AcessoDaRevisao(bool Conduz, bool ReveProtecaoDeDados)
{
    private static readonly PermissionRequirement PermissaoConduzir =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.RevisoesConduzir).Requisito;

    private static readonly PermissionRequirement PermissaoReverProtecaoDeDados =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.RevisoesReverProtecaoDeDados).Requisito;

    /// <summary>Lê as revisões: quem conduz ou quem revê.</summary>
    public bool Le => Conduz || ReveProtecaoDeDados;

    /// <summary>A permissão de quem decide o item: a proteção de dados decide os seus, e os gestores, os da proteção de dados.</summary>
    public bool Decide(TipoDeGovernanca quemDecide) =>
        quemDecide == TipoDeGovernanca.ProtecaoDeDados ? Conduz : ReveProtecaoDeDados;

    /// <summary>
    /// Consulta o que a requisição pode. Nulo quando o token não identifica o solicitante por completo: a borda responde
    /// 401, e nenhuma interpretação de acesso se faz antes disso.
    /// </summary>
    public static async Task<AcessoDaRevisao?> ConsultarAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        ResultadoDoAcesso conduz = await verificador.VerificarAsync(PermissaoConduzir, null, cancellationToken).ConfigureAwait(false);
        if (conduz == ResultadoDoAcesso.IdentidadeIncompleta)
        {
            return null;
        }

        ResultadoDoAcesso reve = await verificador.VerificarAsync(PermissaoReverProtecaoDeDados, null, cancellationToken).ConfigureAwait(false);

        return new AcessoDaRevisao(conduz == ResultadoDoAcesso.Permitido, reve == ResultadoDoAcesso.Permitido);
    }
}
