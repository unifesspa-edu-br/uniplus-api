namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>O que o verificador da persona recebeu: as decisões registradas e as permissões exigidas, para o teste afirmar o que foi ou não gravado.</summary>
internal static class RegistrosDeAcessoDeTeste
{
    /// <summary>Foi registrada uma única vez a projeção com exatamente estas permissões permitidas, em qualquer ordem.</summary>
    public static Task RegistrouAsPermitidasAsync(this DecisoesDeAcessoDeTeste persona, params string[] permissoes) =>
        persona.Verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d =>
                d.OfType<DecisaoPermitida>().Select(x => x.Registro.Permissao).Order().SequenceEqual(permissoes.Order())),
            IntencaoDoRegistro.Projecao,
            Arg.Any<CancellationToken>());

    /// <summary>Foi registrada uma única vez a projeção que inclui a decisão permitida desta permissão.</summary>
    public static Task RegistrouAClasseAsync(this DecisoesDeAcessoDeTeste persona, string permissao) =>
        persona.Verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.OfType<DecisaoPermitida>().Any(x => x.Registro.Permissao == permissao)),
            IntencaoDoRegistro.Projecao,
            Arg.Any<CancellationToken>());

    /// <summary>Nenhum registro inclui a decisão permitida desta permissão.</summary>
    public static Task NaoRegistrouAClasseAsync(this DecisoesDeAcessoDeTeste persona, string permissao) =>
        persona.Verificador.DidNotReceive().RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.OfType<DecisaoPermitida>().Any(x => x.Registro.Permissao == permissao)),
            Arg.Any<IntencaoDoRegistro>(),
            Arg.Any<CancellationToken>());

    /// <summary>A permissão não foi exigida (decidida e registrada) em nenhum momento; consultar a capacidade não a exige.</summary>
    public static Task NaoExigiuAsync(this DecisoesDeAcessoDeTeste persona, string permissao) =>
        persona.Verificador.DidNotReceive().ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == permissao), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());

    /// <summary>O acesso à inscrição foi exigido uma única vez, sobre o resource dela.</summary>
    public static Task ExigiuOAcessoAsync(this DecisoesDeAcessoDeTeste persona, Guid inscricaoId, string permissao) =>
        persona.Verificador.Received(1).ExigirAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == permissao),
            Arg.Is<ResourceContext?>(r => r!.ResourceId == inscricaoId && r.InscricaoId == inscricaoId),
            Arg.Any<CancellationToken>());

    /// <summary>A capacidade desta permissão não foi consultada; a consulta de capacidade não grava registro.</summary>
    public static Task NaoConsultouACapacidadeAsync(this DecisoesDeAcessoDeTeste persona, string permissao) =>
        persona.Verificador.DidNotReceive().PodeAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == permissao), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());

    /// <summary>O acesso à inscrição não foi exigido, qualquer que fosse a permissão.</summary>
    public static Task NaoExigiuNadaAsync(this DecisoesDeAcessoDeTeste persona) =>
        persona.Verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);

    /// <summary>Nenhuma permissão foi avaliada, nem para registrar a negativa.</summary>
    public static Task NaoAvaliouNadaAsync(this DecisoesDeAcessoDeTeste persona) =>
        persona.Verificador.DidNotReceiveWithAnyArgs().AvaliarAsync(default!, default, default);

    /// <summary>Nenhum desfecho de acesso foi registrado.</summary>
    public static Task NaoRegistrouNadaAsync(this DecisoesDeAcessoDeTeste persona) =>
        persona.Verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);

    /// <summary>As decisões do único registro de desfecho que o verificador recebeu.</summary>
    public static IReadOnlyCollection<DecisaoDeAcesso> DecisoesRegistradas(this DecisoesDeAcessoDeTeste persona) =>
        (IReadOnlyCollection<DecisaoDeAcesso>)persona.Verificador.ReceivedCalls()
            .Single(static c => c.GetMethodInfo().Name == nameof(IVerificadorDeAcesso.RegistrarDesfechoAsync)).GetArguments()[0]!;
}
