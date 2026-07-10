namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Queries;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AtoNormativoQueryHandlersTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "ObterPorId devolve null quando o ato não existe")]
    public async Task ObterPorId_Inexistente_Null()
    {
        _atos.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AtoNormativo?)null);

        AtoNormativoDto? dto = await ObterAtoNormativoPorIdQueryHandler.Handle(
            new ObterAtoNormativoPorIdQuery(Guid.CreateVersion7()), _atos, CancellationToken.None);

        dto.Should().BeNull();
    }

    [Fact(DisplayName = "ObterPorId recomputa o aviso excluindo o próprio ato")]
    public async Task ObterPorId_RecomputaAvisoExcluindoProprio()
    {
        AtoNormativo ato = NovoAto();
        _atos.ObterPorIdParaLeituraAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ato);
        Guid outro = Guid.CreateVersion7();
        _atos.ListarIdsComMesmaNumeracaoAsync(
            "CEPS", "EDITAL", 2026, "13", ato.Id, Arg.Any<CancellationToken>())
            .Returns([outro]);

        AtoNormativoDto? dto = await ObterAtoNormativoPorIdQueryHandler.Handle(
            new ObterAtoNormativoPorIdQuery(ato.Id), _atos, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Avisos.Should().ContainSingle();
        dto.Avisos![0].Codigo.Should().Be("NumeroDuplicado");
    }

    [Fact(DisplayName = "Listar projeta os atos sem avisos (evita N+1)")]
    public async Task Listar_SemAvisos()
    {
        AtoNormativo ato = NovoAto();
        _atos.ListarPaginadoAsync(null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>())
            .Returns(([ato], (Guid?)null, (Guid?)null));

        ListarAtosNormativosResult resultado = await ListarAtosNormativosQueryHandler.Handle(
            new ListarAtosNormativosQuery(null, 20, PaginationDirection.Next), _atos, CancellationToken.None);

        resultado.Items.Should().ContainSingle();
        resultado.Items[0].Avisos.Should().BeNull();
    }

    private static AtoNormativo NovoAto() =>
        AtoNormativo.Registrar(
            "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false,
            dataPublicacao: new DateOnly(2026, 3, 13),
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            registradoEm: new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero),
            versaoInvocada: null);
}
