namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As recusas da suspensão, da desativação e da reativação que só dependem do estado do perfil e do motivo informado.</summary>
internal static class ConferenciasDaContencao
{
    public static DomainError? DaContencao(Perfil perfil, string? motivo) =>
        perfil.Governanca is not null ? PerfilErros.GovernancaImutavel()
        : MotivosDaVersao.Normalizar(motivo) is null ? PerfilErros.MotivoInvalido(MotivosDaVersao.MaxLength)
        : null;

    public static DomainError? DaReativacao(Perfil perfil, Identidade autor) =>
        perfil.DesativacaoAberta is { } desativacao && desativacao.DesativadaPor.Equals(autor) ? PerfilErros.ReativacaoPorQuemDesativou() : null;
}
