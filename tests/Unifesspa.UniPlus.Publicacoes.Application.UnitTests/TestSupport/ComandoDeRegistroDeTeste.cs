namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O pedido de registro sem nenhuma recusa; o teste muda só o que o cenário exige.</summary>
internal static class ComandoDeRegistroDeTeste
{
    public const string Tipo = "EDITAL_ABERTURA";
    public const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    public static readonly DateOnly DiaDaPublicacao = new(2026, 3, 13);
    public static readonly DateTimeOffset PublicadoEm = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    public static RegistrarAtoNormativoCommand Valido() =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: Tipo,
            DataDoAto: new DateOnly(2026, 3, 10),
            DocumentoHash: HashValido,
            Assinante: PersonasDeTeste.NomeKevinCarvalho);
}
