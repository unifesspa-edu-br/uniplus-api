namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Queries;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AtoNormativoQueryHandlersTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "ObterPorId falha com ato_normativo.nao_encontrado quando o ato não existe")]
    public async Task ObterPorId_Inexistente_FalhaNaoEncontrado()
    {
        _atos.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AtoNormativo?)null);

        Result<AtoNormativoDto> resultado = await ObterAtoNormativoPorIdQueryHandler.Handle(
            new ObterAtoNormativoPorIdQuery(Guid.CreateVersion7()), _atos, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "ObterPorId recomputa o aviso excluindo o próprio ato")]
    public async Task ObterPorId_AtoSemRetificacaoComOutroNumeroIgual_AvisaNumeroDuplicado()
    {
        AtoNormativo ato = NovoAto();
        Guid outro = Guid.CreateVersion7();
        PrepararDetalhe(ato, cadeia: [ato.Id], mesmaNumeracao: [outro]);

        AtoNormativoDto dto = await ObterDetalheAsync(ato.Id);

        dto.Avisos.Should().ContainSingle();
        dto.Avisos![0].Codigo.Should().Be("NumeroDuplicado");
    }

    [Fact(DisplayName = "Detalhe de retificação exclui o ato retificado dos avisos, mantendo a colisão externa")]
    public async Task ObterPorId_Retificacao_AvisaSomenteColisaoExterna()
    {
        Guid raizId = Guid.CreateVersion7();
        Guid externo = Guid.CreateVersion7();
        AtoNormativo retificador = NovoRetificador(raizId);
        PrepararDetalhe(retificador, cadeia: [raizId, retificador.Id], mesmaNumeracao: [raizId, externo]);

        AtoNormativoDto dto = await ObterDetalheAsync(retificador.Id);

        dto.Avisos.Should().ContainSingle();
        dto.Avisos![0].AtosConflitantes.Should().Contain(externo).And.NotContain(raizId);
    }

    [Fact(DisplayName = "Detalhe da cabeça de uma cadeia profunda exclui a linhagem inteira dos avisos")]
    public async Task ObterPorId_CadeiaProfunda_AvisaSomenteColisaoExterna()
    {
        Guid raizId = Guid.CreateVersion7();
        Guid meioId = Guid.CreateVersion7();
        Guid externo = Guid.CreateVersion7();
        AtoNormativo cabeca = NovoRetificador(meioId);
        PrepararDetalhe(cabeca, cadeia: [raizId, meioId, cabeca.Id], mesmaNumeracao: [raizId, meioId, externo]);

        AtoNormativoDto dto = await ObterDetalheAsync(cabeca.Id);

        dto.Avisos.Should().ContainSingle();
        dto.Avisos![0].AtosConflitantes.Should().Contain(externo)
            .And.NotContain(raizId).And.NotContain(meioId);
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

    private void PrepararDetalhe(AtoNormativo ato, Guid[] cadeia, Guid[] mesmaNumeracao)
    {
        _atos.ObterPorIdParaLeituraAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ato);
        _atos.ListarIdsDaCadeiaAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(cadeia);
        _atos.ListarIdsComMesmaNumeracaoAsync("CEPS", "EDITAL", 2026, "13", null, Arg.Any<CancellationToken>())
            .Returns(mesmaNumeracao);
    }

    private async Task<AtoNormativoDto> ObterDetalheAsync(Guid atoId)
    {
        Result<AtoNormativoDto> resultado = await ObterAtoNormativoPorIdQueryHandler.Handle(
            new ObterAtoNormativoPorIdQuery(atoId), _atos, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        return resultado.Value!;
    }

    private static AtoNormativo NovoAto() =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            dataDoAto: new DateOnly(2026, 3, 13),
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            publicadoEm: new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero),
            versaoDoObjeto: null);

    private static AtoNormativo NovoRetificador(Guid atoRetificadoId) =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            dataDoAto: new DateOnly(2026, 3, 13),
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            publicadoEm: new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero),
            versaoDoObjeto: null,
            atoRetificadoId: atoRetificadoId,
            motivoRetificacao: "corrige o anexo II");
}
