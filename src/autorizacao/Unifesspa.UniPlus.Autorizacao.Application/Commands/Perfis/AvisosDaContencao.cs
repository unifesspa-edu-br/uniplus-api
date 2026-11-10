namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Os avisos da contenção do perfil. A suspensão avisa quem mantém perfis e quem aprova pela proteção de dados, menos
/// quem suspendeu, uma mensagem por pessoa. Nenhum aviso leva motivo, nome de pessoa nem e-mail; o envelope nasce uma
/// vez, na transação que cria a suspensão.
/// </summary>
public sealed class AvisosDaContencao
{
    private static readonly string CodigoDeManterPerfis = CodigoDe(PermissaoAutorizacao.PerfisManter);
    private static readonly string CodigoDaProtecaoDeDados = CodigoDe(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados);

    private readonly EmissorDeAvisos _emissor;
    private readonly ITitularesDaPermissao _titulares;
    private readonly ILinkDaGovernanca _link;

    public AvisosDaContencao(EmissorDeAvisos emissor, ITitularesDaPermissao titulares, ILinkDaGovernanca link)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(titulares);
        ArgumentNullException.ThrowIfNull(link);

        _emissor = emissor;
        _titulares = titulares;
        _link = link;
    }

    public async Task<IReadOnlyList<EnviarEmail>> DaSuspensaoAsync(
        Perfil perfil, SuspensaoDoPerfil suspensao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(suspensao);

        ModeloDeAviso modelo = new(TipoDeAviso.PerfilSuspenso, perfil.Nome, null, [], _link.DoPerfil(perfil.Id));
        DestinatarioDoAviso[] destinatarios =
        [
            .. await TitularesAsync(CodigoDeManterPerfis, cancellationToken).ConfigureAwait(false),
            .. await TitularesAsync(CodigoDaProtecaoDeDados, cancellationToken).ConfigureAwait(false),
        ];

        return await _emissor
            .EmitirAsync(new PedidoDeAviso(modelo, suspensao.Id, string.Empty, destinatarios, [suspensao.SuspensaPor]), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string CodigoDe(PermissaoAutorizacao permissao) => CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Codigo;

    private async Task<IEnumerable<DestinatarioDoAviso>> TitularesAsync(string codigoDaPermissao, CancellationToken cancellationToken) =>
        (await _titulares.ListarTitularesComEmailAsync(codigoDaPermissao, cancellationToken).ConfigureAwait(false))
            .Select(static t => new DestinatarioDoAviso(new Identidade(t.Emissor, t.Sub), t.Email));
}
