namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TipoDocumentoReaderPorCodigosTests(ConfiguracaoDbFixture fixture) : IAsyncLifetime
{
    private const string Admin = "admin-teste";
    private const string Comprovante = "TESTE_LOTE_COMPROVANTE";
    private const string Declaracao = "TESTE_LOTE_DECLARACAO";
    private const string Historico = "TESTE_LOTE_HISTORICO";
    private static readonly string[] CodigosUsados = [Comprovante, Declaracao, Historico];

    public Task InitializeAsync() => ApagarTiposUsadosAsync();

    public Task DisposeAsync() => ApagarTiposUsadosAsync();

    [Fact]
    public async Task ListarVivosPorCodigosAsync_TiposSolicitados_DevolveOrdenados()
    {
        await CadastrarAsync(vivos: [Comprovante, Declaracao, Historico], removidos: []);

        IReadOnlyList<TipoDocumentoView> tipos = await ListarAsync(Declaracao, Comprovante);

        tipos.Select(tipo => tipo.Codigo).Should().Equal(Comprovante, Declaracao);
        tipos.Should().OnlyContain(tipo => tipo.Id != Guid.Empty && tipo.Nome.Length > 0);
    }

    [Fact]
    public async Task ListarVivosPorCodigosAsync_CodigosInvalidosOuSemCadastro_NaoVoltam()
    {
        await CadastrarAsync(vivos: [Comprovante], removidos: []);

        IReadOnlyList<TipoDocumentoView> tipos =
            await ListarAsync(Comprovante, "TESTE_LOTE_INEXISTENTE", "documento", "01", "DOC-INVALIDO", string.Empty);

        tipos.Select(tipo => tipo.Codigo).Should().Equal(Comprovante);
    }

    [Fact]
    public async Task ListarVivosPorCodigosAsync_TipoRemovido_NaoVolta()
    {
        await CadastrarAsync(vivos: [Comprovante], removidos: [Declaracao]);

        IReadOnlyList<TipoDocumentoView> tipos = await ListarAsync(Comprovante, Declaracao);

        tipos.Select(tipo => tipo.Codigo).Should().Equal(Comprovante);
    }

    [Fact]
    public async Task ListarVivosPorCodigosAsync_CodigosRepetidosEComEspacos_DevolveUmaVez()
    {
        await CadastrarAsync(vivos: [Comprovante, Declaracao], removidos: []);

        IReadOnlyList<TipoDocumentoView> tipos =
            await ListarAsync(Comprovante, Declaracao, Comprovante, $" {Declaracao} ");

        tipos.Select(tipo => tipo.Codigo).Should().Equal(Comprovante, Declaracao);
    }

    [Fact]
    public async Task ListarVivosPorCodigosAsync_LoteVazio_DevolveListaVazia()
    {
        await CadastrarAsync(vivos: [Comprovante], removidos: []);

        IReadOnlyList<TipoDocumentoView> tipos = await ListarAsync();

        tipos.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<TipoDocumentoView>> ListarAsync(params string[] codigos)
    {
        await using UniPlusDbContext leitura = fixture.CreateDbContext(userId: null);
        return await new TipoDocumentoReader(leitura).ListarVivosPorCodigosAsync(codigos);
    }

    private async Task CadastrarAsync(string[] vivos, string[] removidos)
    {
        await using UniPlusDbContext escrita = fixture.CreateDbContext(Admin);
        List<TipoDocumento> tipos = [.. vivos.Concat(removidos).Select(Tipo)];
        escrita.TiposDocumento.AddRange(tipos);
        await escrita.SaveChangesAsync();

        escrita.TiposDocumento.RemoveRange(tipos.Where(tipo => removidos.Contains(tipo.Codigo.Valor)));
        await escrita.SaveChangesAsync();
    }

    private async Task ApagarTiposUsadosAsync()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext(userId: null);
        await contexto.Database.ExecuteSqlRawAsync(
            "DELETE FROM configuracao.tipos_de_documento WHERE codigo = ANY(@p0)",
            new NpgsqlParameter("p0", CodigosUsados));
    }

    private static TipoDocumento Tipo(string codigo) =>
        TipoDocumento.Criar(new DeclaracaoDoTipoDocumento(codigo, $"Documento {codigo}", null, "OUTROS", "pdf", 10, null,
            ClassificacaoDeProtecao.Pessoal,
            "Comprovação documental do candidato.",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
}
