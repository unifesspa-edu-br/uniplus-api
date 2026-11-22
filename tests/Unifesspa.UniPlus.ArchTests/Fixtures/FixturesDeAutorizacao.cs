namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Controllers mínimos que servem de contraprova às regras de autorização: cada regra tem de
/// rejeitar o que está marcado como violação e aceitar o que está marcado como válido.
/// </summary>
internal static class FixturesDeAutorizacao
{
    /// <summary>Serviço de aplicação que decide pelo ponto único: recebe o verificador no construtor.</summary>
    internal sealed class ServicoQueRecebeOVerificador
    {
        public ServicoQueRecebeOVerificador(IVerificadorDeAcesso verificador) => Verificador = verificador;

        public IVerificadorDeAcesso Verificador { get; }
    }

    /// <summary>Serviço de aplicação que não passa pelo ponto único: nada nele chega ao verificador.</summary>
    internal sealed class ServicoQueNaoRecebeOVerificador
    {
        public ServicoQueNaoRecebeOVerificador(TimeProvider relogio) => Relogio = relogio;

        public TimeProvider Relogio { get; }
    }
    internal sealed class MensagemDireta;
    internal sealed class MensagemPorServico;
    internal sealed class MensagemSemVerificador;

    internal static class HandlerDireto
    {
        public static void Handle(MensagemDireta mensagem, IVerificadorDeAcesso verificador) { }
    }

    internal static class HandlerPorServico
    {
        public static void Handle(MensagemPorServico mensagem, ServicoQueRecebeOVerificador servico) { }
    }

    internal static class HandlerSemVerificador
    {
        public static void Handle(MensagemSemVerificador mensagem, ServicoQueNaoRecebeOVerificador servico) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class FiltroSemMarca : Attribute, Microsoft.AspNetCore.Mvc.Filters.IAuthorizationFilter
    {
        public void OnAuthorization(Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext context) { }
    }
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class ExigenciaSemVerificador : Attribute, Unifesspa.UniPlus.Infrastructure.Core.Authorization.IExigeAutorizacaoAlemDoLogin;
}
