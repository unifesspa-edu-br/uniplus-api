namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A política de registro de acesso, num lugar só: dadas as decisões avaliadas e a intenção de quem decidiu, diz quais
/// registros se gravam. Quem avalia não grava, e quem grava não decide o quê.
/// </summary>
/// <remarks>
/// Das decisões avaliadas por uma mesma pergunta, grava-se a permitida que autorizou; se nenhuma autorizou, as negadas,
/// que são a tentativa real. O que se grava das permitidas depende da intenção. A decisão sem sujeito, em qualquer posição, anula o registro do desfecho.
/// </remarks>
public static class PoliticaDeRegistroDeAcesso
{
    /// <summary>Os registros que o desfecho das decisões manda gravar, na ordem em que foram avaliadas.</summary>
    /// <param name="decisoes">As decisões avaliadas por uma mesma pergunta.</param>
    /// <param name="intencao">Para que o chamador decidiu; <see cref="IntencaoDoRegistro.PorResourceExibido"/> grava por <see cref="PorResource"/>.</param>
    public static IReadOnlyList<RegistroDecisaoAcesso> Desfecho(
        IEnumerable<DecisaoDeAcesso> decisoes, IntencaoDoRegistro intencao)
    {
        ArgumentNullException.ThrowIfNull(decisoes);

        DecisaoDeAcesso[] avaliadas = [.. decisoes];

        // Sem sujeito não houve pergunta a registrar, na principal ou em qualquer decisão da lista.
        if (avaliadas.Any(static d => d is SemSujeito))
        {
            return [];
        }

        DecisaoDeAcesso[] permitidas = [.. avaliadas.OfType<DecisaoPermitida>()];

        DecisaoDeAcesso[] escolhidas = (permitidas.Length, intencao) switch
        {
            ( > 0, IntencaoDoRegistro.Pedido) => [permitidas[0]],
            ( > 0, _) => permitidas,
            (_, IntencaoDoRegistro.Projecao) => [],
            _ => avaliadas,
        };

        return [.. escolhidas.Where(d => Grava(d, intencao)).Select(RegistroDe)];
    }

    /// <summary>
    /// Um registro por id de resource exibido, a partir da decisão permitida que a listagem tomou uma vez só sobre o
    /// conjunto.
    /// </summary>
    public static IReadOnlyList<RegistroDecisaoAcesso> PorResource(
        DecisaoPermitida permitida, string resourceTipo, IEnumerable<Guid> resourceIds, bool resourceEhInscricao)
    {
        ArgumentNullException.ThrowIfNull(permitida);

        return Grava(permitida, IntencaoDoRegistro.PorResourceExibido)
            ? permitida.Registro.ParaCadaResource(resourceTipo, resourceIds, resourceEhInscricao)
            : [];
    }

    private static bool Grava(DecisaoDeAcesso decisao, IntencaoDoRegistro intencao) => (decisao, intencao) switch
    {
        (DecisaoPermitida, IntencaoDoRegistro.Projecao or IntencaoDoRegistro.PorResourceExibido) => true,
        (DecisaoNegada, IntencaoDoRegistro.Projecao or IntencaoDoRegistro.PorResourceExibido) => false,

        // A alternativa que exige concessão em unidade e só encontrou concessão global não afirma uma tentativa sobre
        // o dado: a trilha não a recebe.
        (DecisaoNegada { Registro.MotivoNegativa: var motivo }, _)
            when motivo == ValoresCanonicos.De(MotivoNegativa.ConcessaoEmUnidadeExigida) => false,
        (DecisaoPermitida p, _) => Pessoal(p.Requisito, p.Resource),
        (DecisaoNegada n, _) => Pessoal(n.Requisito, n.Resource),
        _ => false,
    };

    // Só o dado pessoal, identificador ou sensível exige registro de quem decide por pedido; as demais decisões não
    // gravam, para que a autorização operacional não dependa do destino do registro.
    private static bool Pessoal(PermissionRequirement requisito, ResourceContext resource) =>
        RegistroDecisaoAcesso.SensibilidadeEfetiva(requisito, resource) >= ClassificacaoDeProtecao.Pessoal;

    private static RegistroDecisaoAcesso RegistroDe(DecisaoDeAcesso decisao) => decisao switch
    {
        DecisaoPermitida permitida => permitida.Registro,
        DecisaoNegada negada => negada.Registro,
        _ => throw new InvalidOperationException("A decisão sem sujeito não gera registro."),
    };
}
