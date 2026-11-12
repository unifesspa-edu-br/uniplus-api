namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// As travas que a revogação direta toma antes de mexer na concessão, na ordem única: o papel de Representante do
/// controlador, quando a concessão é dele; a pessoa do beneficiário, quando a concessão é de perfil de governança; e a
/// linha da concessão. A trava dos gestores, quando cabe, vem depois, na regra comum da revogação. As revogações
/// concorrentes esperam umas pelas outras em vez de travarem em ciclo, e a designação do outro papel para a mesma pessoa
/// lê o estado já gravado.
/// </summary>
public sealed class TravasDaRevogacao
{
    private readonly IConcessaoRepository _concessoes;
    private readonly IPapelDoRepresentante _papel;
    private readonly IIncompatibilidadeDosPapeis _papeis;

    public TravasDaRevogacao(IConcessaoRepository concessoes, IPapelDoRepresentante papel, IIncompatibilidadeDosPapeis papeis)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(papel);
        ArgumentNullException.ThrowIfNull(papeis);

        _concessoes = concessoes;
        _papel = papel;
        _papeis = papeis;
    }

    /// <summary>A concessão travada para alterar, ou nula quando não existe.</summary>
    public async Task<Concessao?> TravarAsync(Guid concessaoId, CancellationToken cancellationToken)
    {
        // O perfil e o beneficiário de uma concessão não mudam: a leitura sem trava basta para saber o que travar.
        if (await _concessoes.ObterComPerfilAsync(concessaoId, cancellationToken).ConfigureAwait(false) is not { } lida)
        {
            return null;
        }

        if (lida.Concessao.PerfilId == PerfisDeSistema.RepresentanteDoControladorId)
        {
            await _papel.TravarAsync(cancellationToken).ConfigureAwait(false);
        }

        await _papeis.TravarBeneficiarioAsync(lida.Concessao, cancellationToken).ConfigureAwait(false);

        return await _concessoes.ObterParaAlterarAsync(concessaoId, cancellationToken).ConfigureAwait(false);
    }
}
