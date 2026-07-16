namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Retenção do documento publicado, contra o Postgres real. É o guard rail que a #804 teve
/// de <b>repor</b>, e não apenas mover.
/// </summary>
/// <remarks>
/// <para>
/// Enquanto o <c>Edital</c> existia, a chave estrangeira <c>fk_editais_documento_edital_id</c>
/// (<c>ON DELETE RESTRICT</c>) era o que impedia apagar fisicamente o PDF de um edital já
/// publicado. Ao eliminar a tabela, essa proteção iria junto: <c>DocumentoEdital</c> continua
/// na Seleção, e um <c>DELETE</c> cru levaria embora o documento que fundamenta uma
/// configuração congelada. A referência <c>{id, hash}</c> guardada no snapshot prova a
/// integridade do CONTEÚDO — não a sua permanência.
/// </para>
/// <para>
/// O substituto é mais forte que a FK antiga: um documento <b>confirmado</b> não se remove,
/// tenha ele sido publicado ou não. É o que o próprio ciclo de vida já declarava —
/// <c>StatusDocumentoEdital.Confirmado</c> não tem caminho de volta. O pendente, que é um
/// upload que nunca se completou, segue removível (a limpeza dos expirados depende disso).
/// </para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — não recebe entrada externa.")]
public sealed class RetencaoDocumentoEditalTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("cd23456789", 7))[..64];

    private readonly ProcessoSeletivoDbFixture _fixture;

    public RetencaoDocumentoEditalTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "DELETE cru de documento CONFIRMADO é recusado pelo banco — a evidência do publicado não se apaga")]
    public async Task DeleteCru_DocumentoConfirmado_Recusado()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: true);

        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        Func<Task> apagar = async () => await ExecutarAsync(
            conexao, $"DELETE FROM selecao.documentos_edital WHERE id = '{documentoId}'");

        // O DELETE não passa pelo agregado: é o banco que o recusa. Sem esta trava, a
        // eliminação da tabela `editais` teria levado junto a retenção que a sua chave
        // estrangeira garantia — e o PDF que fundamenta uma configuração congelada ficaria
        // removível por um comando solto.
        (await apagar.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_documentos_edital_confirmado_retido");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        (await contexto.DocumentosEdital.FindAsync(documentoId)).Should().NotBeNull(
            "o documento continua lá — a recusa não é cosmética");
    }

    [Fact(DisplayName = "Rebaixar o status de um documento confirmado é recusado — senão bastaria desconfirmar e então apagar")]
    public async Task UpdateCru_DesconfirmarDocumento_Recusado()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: true);

        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // Reter só o DELETE deixaria o desvio aberto: `UPDATE status = 0` seguido de DELETE
        // levaria a evidência do mesmo jeito. Confirmado não tem caminho de volta — é o que o
        // ciclo de vida já declarava, e agora o banco o impõe.
        Func<Task> desconfirmar = async () => await ExecutarAsync(
            conexao, $"UPDATE selecao.documentos_edital SET status = 0 WHERE id = '{documentoId}'");

        (await desconfirmar.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_documentos_edital_confirmado_retido");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        DocumentoEdital? documento = await contexto.DocumentosEdital.FindAsync(documentoId);
        documento!.Status.Should().Be(Domain.Enums.StatusDocumentoEdital.Confirmado);
    }

    [Fact(DisplayName = "TRUNCATE da tabela de documentos é recusado — não dispara trigger de linha, e levaria os confirmados junto")]
    public async Task TruncateCru_Recusado()
    {
        await SemearDocumentoAsync(confirmado: true);

        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // TRUNCATE não dispara trigger de linha: sem uma trava de statement, um único comando
        // apagaria a tabela inteira, confirmados inclusive.
        Func<Task> truncar = async () => await ExecutarAsync(
            conexao, "TRUNCATE TABLE selecao.documentos_edital CASCADE");

        (await truncar.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ck_documentos_edital_confirmado_retido");
    }

    [Fact(DisplayName = "DELETE de documento PENDENTE é aceito — um upload que nunca se completou não é evidência de nada")]
    public async Task DeleteCru_DocumentoPendente_Aceito()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: false);

        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // A retenção é do CONFIRMADO, não de toda linha da tabela: a limpeza dos pendentes
        // expirados tem de continuar possível.
        await ExecutarAsync(conexao, $"DELETE FROM selecao.documentos_edital WHERE id = '{documentoId}'");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        (await contexto.DocumentosEdital.FindAsync(documentoId)).Should().BeNull();
    }

    [Fact(DisplayName = "Confirmar um documento pendente continua possível — a retenção não congela o que ainda não é evidência")]
    public async Task UpdateCru_ConfirmarPendente_Aceito()
    {
        Guid documentoId = await SemearDocumentoAsync(confirmado: false);

        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        // O trigger de UPDATE dispara só quando o status SAI de Confirmado. A transição
        // normal do ciclo de vida (Pendente → Confirmado) não pode ser afetada — se fosse, o
        // próprio upload deixaria de funcionar.
        await ExecutarAsync(conexao, $"UPDATE selecao.documentos_edital SET status = 1 WHERE id = '{documentoId}'");

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        DocumentoEdital? documento = await contexto.DocumentosEdital.FindAsync(documentoId);
        documento!.Status.Should().Be(Domain.Enums.StatusDocumentoEdital.Confirmado);
    }

    private async Task<Guid> SemearDocumentoAsync(bool confirmado)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Retenção {Guid.CreateVersion7()}", Domain.Enums.TipoProcesso.SiSU, Domain.Enums.OrigemCandidatos.InscricaoPropria);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        if (confirmado)
        {
            documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        }

        await using SelecaoDbContext contexto = _fixture.CreateDbContext();
        await contexto.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
        await contexto.DocumentosEdital.AddAsync(documento, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return documento.Id;
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }
}
