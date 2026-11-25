namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Verifica no PostgreSQL a retenção de documentos confirmados e a remoção de pendentes.
/// </summary>
public sealed class RetencaoDocumentoAPublicarTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("cd23456789", 7))[..64];

    private readonly ProcessoSeletivoDbFixture _fixture;

    public RetencaoDocumentoAPublicarTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "DELETE cru de documento CONFIRMADO é recusado pelo banco — a evidência do publicado não se apaga")]
    public async Task DeleteCru_DocumentoConfirmado_Recusado()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: true);

        await using NpgsqlConnection conexao = await AbrirConexaoAsync();

        Func<Task> apagar = () => ExecutarAsync(conexao, "DELETE FROM selecao.documentos_a_publicar WHERE id = @id", documentoId);

        (await apagar.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_documentos_a_publicar_confirmado_retido");

        await ConferirDocumentoExistenteAsync(documentoId);
    }

    [Fact(DisplayName = "Rebaixar a situação de um documento confirmado é recusado — senão bastaria desconfirmar e então apagar")]
    public async Task UpdateCru_DesconfirmarDocumento_Recusado()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: true);

        await using NpgsqlConnection conexao = await AbrirConexaoAsync();

        // Reter só o DELETE deixaria o desvio aberto: `UPDATE situacao = 'PENDENTE'` seguido de DELETE
        // levaria a evidência do mesmo jeito. Confirmado não tem caminho de volta — é o que o
        // ciclo de vida declara, e o banco o impõe.
        Func<Task> desconfirmar = () => ExecutarAsync(conexao, "UPDATE selecao.documentos_a_publicar SET situacao = @situacao WHERE id = @id", documentoId, StatusArquivoEnviado.Pendente);

        (await desconfirmar.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_documentos_a_publicar_confirmado_retido");

        await ConferirDocumentoConfirmadoAsync(documentoId);
    }

    [Fact]
    public async Task UpdateCru_TrocarProcessoDeDocumentoConfirmado_Recusado()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: true);
        await using NpgsqlConnection conexao = await AbrirConexaoAsync();
        Func<Task> trocar = () => ExecutarAsync(conexao, "UPDATE selecao.documentos_a_publicar SET processo_seletivo_id = gen_random_uuid() WHERE id = @id", documentoId);
        (await trocar.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_documentos_a_publicar_confirmado_retido");
        await ConferirDocumentoConfirmadoAsync(documentoId);
    }

    [Fact(DisplayName = "TRUNCATE da tabela de documentos é recusado — não dispara trigger de linha, e levaria os confirmados junto")]
    public async Task TruncateCru_Recusado()
    {
        await SemearDocumentoAsync(confirmado: true);

        await using NpgsqlConnection conexao = await AbrirConexaoAsync();

        // TRUNCATE não dispara trigger de linha: sem uma trava de statement, um único comando
        // apagaria a tabela inteira, confirmados inclusive.
        Func<Task> truncar = async () => await ExecutarAsync(
            conexao, "TRUNCATE TABLE selecao.documentos_a_publicar CASCADE");

        (await truncar.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_documentos_a_publicar_confirmado_retido");
    }

    [Fact(DisplayName = "DELETE de documento PENDENTE é aceito — um upload que nunca se completou não é evidência de nada")]
    public async Task DeleteCru_DocumentoPendente_Aceito()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: false);

        await using NpgsqlConnection conexao = await AbrirConexaoAsync();

        // A retenção é do CONFIRMADO, não de toda linha da tabela: a limpeza dos pendentes
        // expirados tem de continuar possível.
        await ExecutarAsync(conexao, "DELETE FROM selecao.documentos_a_publicar WHERE id = @id", documentoId);

        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        (await contexto.DocumentosAPublicar.FindAsync(documentoId)).Should().BeNull();
    }

    [Fact(DisplayName = "Confirmar um documento pendente continua possível — a retenção não congela o que ainda não é evidência")]
    public async Task UpdateCru_ConfirmarPendente_Aceito()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: false);

        await using NpgsqlConnection conexao = await AbrirConexaoAsync();

        // O trigger de UPDATE protege os documentos já confirmados. A transição
        // normal do ciclo de vida (Pendente → Confirmado) não pode ser afetada — se fosse, o
        // próprio upload deixaria de funcionar.
        await ExecutarAsync(conexao, "UPDATE selecao.documentos_a_publicar SET situacao = @situacao WHERE id = @id", documentoId, StatusArquivoEnviado.Confirmado);

        await ConferirDocumentoConfirmadoAsync(documentoId);
    }

    private async Task<Guid> SemearDocumentoAsync(bool confirmado)
    {
        ProcessoSeletivo processo = CriarProcesso();
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        if (confirmado)
        {
            documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        }

        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        await contexto.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
        await contexto.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return documento.Id;
    }

    private static ProcessoSeletivo CriarProcesso() => ProcessoSeletivo.Criar(
        $"Retenção {Guid.CreateVersion7()}", TiposDeProcessoDeTeste.Sisu, Domain.Enums.OrigemCandidatos.InscricaoPropria,
        Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private async Task ConferirDocumentoExistenteAsync(Guid documentoId)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        (await contexto.DocumentosAPublicar.FindAsync(documentoId)).Should().NotBeNull();
    }

    private async Task ConferirDocumentoConfirmadoAsync(Guid documentoId)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        DocumentoAPublicar? documento = await contexto.DocumentosAPublicar.FindAsync(documentoId);
        documento!.Situacao.Should().Be(StatusArquivoEnviado.Confirmado);
    }

    private async Task<NpgsqlConnection> AbrirConexaoAsync()
    {
        NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        return conexao;
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql, Guid? id = null, StatusArquivoEnviado? situacao = null)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        if (id is { } documentoId)
        {
            comando.Parameters.AddWithValue("id", documentoId);
        }

        if (situacao is { } valor)
        {
            comando.Parameters.AddWithValue("situacao", valor.Codigo());
        }

        await comando.ExecuteNonQueryAsync();
    }
}
