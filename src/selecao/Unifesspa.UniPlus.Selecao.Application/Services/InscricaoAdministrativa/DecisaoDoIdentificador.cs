namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// As duas permissões que separam o valor completo de um identificador do mascarado, na ordem em que a rota as exige,
/// qualquer que seja o caminho que alcançou a inscrição: a classe em que o identificador aparece, que mostra o valor
/// mascarado, e a de revelar. A recusa da classe registra só a negativa dela, e nada é registrado como revelado.
/// </summary>
public static class DecisaoDoIdentificador
{
    /// <summary>O erro com que a rota recusa, ou nulo quando as duas permissões foram concedidas e o desfecho registrado.</summary>
    public static async Task<DomainError?> RecusaAsync(
        IVerificadorDeAcesso verificador, Vinculo conferido, ResourceContext resource, DomainError naoEncontrado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return await RecusaDaClasseAsync(verificador, resource, naoEncontrado, cancellationToken).ConfigureAwait(false)
            ?? await RecusaDeRevelarAsync(verificador, conferido, resource, naoEncontrado, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Se a pessoa poderia revelar o identificador: a classe e a permissão de revelar, sem registrar nada.</summary>
    public static async Task<bool> PodeAsync(IVerificadorDeAcesso verificador, ResourceContext resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return await verificador.PodeAsync(PermissaoSelecao.DadosIdentificadoresConsultar.Requisito, resource, cancellationToken).ConfigureAwait(false)
                == ResultadoDoAcesso.Permitido
            && await verificador.PodeAsync(PermissaoSelecao.DadosIdentificadoresRevelar.Requisito, resource, cancellationToken).ConfigureAwait(false)
                == ResultadoDoAcesso.Permitido;
    }

    private static async Task<DomainError?> RecusaDaClasseAsync(
        IVerificadorDeAcesso verificador, ResourceContext resource, DomainError naoEncontrado, CancellationToken cancellationToken)
    {
        DecisaoDeAcesso daClasse = await verificador
            .AvaliarAsync(PermissaoSelecao.DadosIdentificadoresConsultar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false);
        if (daClasse is DecisaoPermitida)
        {
            return null;
        }

        if (daClasse is DecisaoNegada)
        {
            await verificador.RegistrarDesfechoAsync([daClasse], IntencaoDoRegistro.Pedido, cancellationToken).ConfigureAwait(false);
        }

        return AcessoAInscricao.Erro(new Recusada(daClasse.Resultado), naoEncontrado);
    }

    private static async Task<DomainError?> RecusaDeRevelarAsync(
        IVerificadorDeAcesso verificador, Vinculo conferido, ResourceContext resource, DomainError naoEncontrado, CancellationToken cancellationToken) =>
        await AcessoAInscricao
            .DecidirAsync(verificador, conferido, PermissaoSelecao.DadosIdentificadoresRevelar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false) is Recusada recusada
            ? AcessoAInscricao.Erro(recusada, naoEncontrado)
            : null;
}
