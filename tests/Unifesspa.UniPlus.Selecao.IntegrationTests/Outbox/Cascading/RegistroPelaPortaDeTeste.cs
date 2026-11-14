namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// O registro de um ato como o domínio dono do objeto o faz, para semear o que a publicação encontra pelo caminho: a
/// porta de escrita, seguida da gravação única que o domínio faz depois dela.
/// </summary>
internal static class RegistroPelaPortaDeTeste
{
    internal static AtoNormativoPublicado AtoDoProcesso(
        Guid processoId, string tipoCodigo, string numero, AtributosDoTipoAto tipoConferido) =>
        new()
        {
            AtoId = Guid.CreateVersion7(),
            Orgao = "CEPS",
            Serie = "EDITAL",
            Ano = 2026,
            Numero = numero,
            TipoCodigo = tipoCodigo,
            AtributosDoTipo = tipoConferido,
            DataDoAto = DateOnly.FromDateTime(DateTime.UtcNow),
            PublicadoEm = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero),
            DocumentoHash = new string('d', 64),
            Assinante = "Registro por outro caminho",
            Vinculos = [new VinculoDoAtoPublicado(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, processoId)],
        };

    internal static async Task<Result> RegistrarSemGravarAsync(CascadingApiFactory api, AtoNormativoPublicado ato)
    {
        ArgumentNullException.ThrowIfNull(api);

        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IRegistroDeAtoNormativo>()
            .RegistrarAsync(ato, CancellationToken.None);
    }

    internal static async Task RegistrarEGravarAsync(CascadingApiFactory api, AtoNormativoPublicado ato)
    {
        ArgumentNullException.ThrowIfNull(api);

        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        Result registrado = await escopo.ServiceProvider.GetRequiredService<IRegistroDeAtoNormativo>()
            .RegistrarAsync(ato, CancellationToken.None);
        registrado.IsSuccess.Should().BeTrue(registrado.Error?.Message);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync(CancellationToken.None);
    }
}
