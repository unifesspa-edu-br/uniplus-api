namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da leitura de um perfil pelo Id.</summary>
public static class ObterPerfilQueryHandler
{
    public static async Task<Result<PerfilDto>> Handle(
        ObterPerfilQuery query, IVerificadorDeAcesso verificador, DetalheDoPerfil detalhe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(detalhe);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, query.Id, cancellationToken).ConfigureAwait(false);

        return recusa is null
            ? await detalhe.MontarAsync(query.Id, cancellationToken).ConfigureAwait(false)
            : Result<PerfilDto>.Failure(recusa);
    }
}
