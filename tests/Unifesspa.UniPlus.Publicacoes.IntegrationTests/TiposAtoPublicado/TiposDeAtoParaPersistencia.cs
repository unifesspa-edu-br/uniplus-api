namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Globalization;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class TiposDeAtoParaPersistencia
{
    internal static TipoAtoPublicado Novo(
        string codigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        bool congela = true,
        bool irreversivel = false,
        bool ehResultado = false) =>
        (new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            Nome = "Tipo de ato de teste",
            CongelaConfiguracao = congela,
            EfeitoIrreversivel = irreversivel,
            EhResultado = ehResultado,
            VigenciaInicio = vigenciaInicio,
            VigenciaFim = vigenciaFim
        }).Criar().Value!;

    /// <summary>
    /// Código único no formato aceito pelo CHECK: os 12 primeiros dígitos hex do
    /// Guid mapeados para as letras A–P, o que evita colisão entre testes que
    /// compartilham o mesmo container.
    /// </summary>
    internal static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "TESTE_" + string.Concat(hex.Select(digito => (char)('A' + Convert.ToInt32(digito.ToString(), 16))));
    }
}
