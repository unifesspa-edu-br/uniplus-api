namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// Os avisos da contenção do perfil. A suspensão avisa quem mantém perfis e quem aprova pela proteção de dados, menos
/// quem suspendeu, uma mensagem por pessoa. Nenhum aviso leva motivo, nome de pessoa nem e-mail; o envelope nasce uma
/// vez, na transação que cria a suspensão.
/// </summary>
public sealed class AvisosDaContencao
{
    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;

    public AvisosDaContencao(EmissorDeAvisos emissor, GruposDaGovernanca grupos, ILinkDaGovernanca link)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(link);

        _emissor = emissor;
        _grupos = grupos;
        _link = link;
    }

    public async Task<IReadOnlyList<EnviarEmail>> DaSuspensaoAsync(
        Perfil perfil, SuspensaoDoPerfil suspensao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(suspensao);

        ModeloDeAviso modelo = new(TipoDeAviso.PerfilSuspenso, perfil.Nome, null, [], _link.DoPerfil(perfil.Id));
        List<DestinatarioDoAviso> destinatarios = await _grupos
            .ListarAsync([GrupoDaGovernanca.Manutencao, GrupoDaGovernanca.ProtecaoDeDados], cancellationToken)
            .ConfigureAwait(false);

        return await _emissor
            .EmitirAsync(new PedidoDeAviso(modelo, suspensao.Id, string.Empty, destinatarios, [suspensao.SuspensaPor]), cancellationToken)
            .ConfigureAwait(false);
    }
}
