namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>O e-mail que leva ao gestor o código de confirmação da instalação: o código vale uma vez, por duas horas e até cinco tentativas.</summary>
internal static class MensagemDoCodigoDeConfirmacao
{
    private const string Assunto = "Código de confirmação da instalação dos gestores de acessos";
    private const string Aviso = "Se você não iniciou a instalação dos gestores de acessos do Uni+, ignore esta mensagem e avise a equipe de infraestrutura.";

    public static EnviarEmail Para(Email destinatario, string codigo) => EnviarEmail.Para(
        destinatario,
        Assunto,
        $"Seu código de confirmação é {codigo}. {Validade()} {Aviso}",
        $"<p>Seu código de confirmação é <b>{codigo}</b>.</p><p>{Validade()}</p><p>{Aviso}</p>",
        "instalacao.codigo");

    private static string Validade() =>
        $"Ele vale por {Instalacao.PrazoDoCodigo.TotalHours:0} horas, aceita até {Instalacao.MaximoDeTentativas} tentativas e só pode ser usado uma vez.";
}
