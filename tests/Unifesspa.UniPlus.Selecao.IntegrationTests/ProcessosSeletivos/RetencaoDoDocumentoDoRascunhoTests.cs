namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A retenção do documento confirmado vale para o documento de um processo que já existe. O documento de um rascunho
/// sai junto com ele, sem relaxar a proibição de desconfirmar, que vale sempre, nem a retenção do processo excluído
/// logicamente, cuja linha continua no banco.
/// </summary>
public sealed class RetencaoDoDocumentoDoRascunhoTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string RestricaoDeRetencao = "ck_documentos_a_publicar_confirmado_retido";
    private const string ApagarDocumento = "DELETE FROM selecao.documentos_a_publicar WHERE id = @id";
    private const string DesconfirmarDocumento = "UPDATE selecao.documentos_a_publicar SET situacao = @situacao WHERE id = @id";

    private readonly ProcessoSeletivoDbFixture _fixture;

    public RetencaoDoDocumentoDoRascunhoTests(ProcessoSeletivoDbFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact(DisplayName = "Excluir o rascunho apaga o documento confirmado dele e emite a remoção dos objetos")]
    public async Task ExcluirRascunho_ApagaOConfirmadoEEmiteARemocaoDosObjetos()
    {
        Guid rascunhoId = await RascunhosSemeadosParaTeste.SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        DocumentoAPublicar confirmado = await SemearConfirmadoAsync(rascunhoId);
        await using UniPlusDbContext excluidor = _fixture.CreateDbContext();

        (Result resposta, IEnumerable<object> mensagens) = await new ExclusaoDoRascunhoDeTeste(excluidor).ExcluirNaTransacaoAsync(rascunhoId);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RemoverArquivosDoRascunhoExcluido>()
            .Which.Chaves.Should().BeEquivalentTo([confirmado.ObjectKey, confirmado.ObjectKeyConfirmado!]);
        (await DocumentosDoDonoAsync(rascunhoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "DELETE cru do documento confirmado de um dono sem linha de processo é aceito")]
    public async Task DeleteCru_ConfirmadoDeDonoSemLinhaDeProcesso_Aceito()
    {
        DocumentoAPublicar documento = await SemearConfirmadoAsync(Guid.CreateVersion7());

        await ExecutarAsync(ApagarDocumento, documento.Id);

        (await DocumentosDoDonoAsync(documento.ProcessoSeletivoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "DELETE cru do documento confirmado de um processo que existe é recusado com o nome da restrição")]
    public async Task DeleteCru_ConfirmadoDeProcessoExistente_Recusado()
    {
        ProcessoSeletivo processo = await RascunhosSemeadosParaTeste.SemearProcessoAsync(_fixture, Guid.CreateVersion7());
        DocumentoAPublicar documento = await SemearConfirmadoAsync(processo.Id);

        Func<Task> apagar = () => ExecutarAsync(ApagarDocumento, documento.Id);

        (await apagar.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(RestricaoDeRetencao);
        (await DocumentosDoDonoAsync(processo.Id)).Should().ContainSingle();
    }

    [Fact(DisplayName = "DELETE cru do documento confirmado de um processo excluído logicamente continua recusado")]
    public async Task DeleteCru_ConfirmadoDeProcessoExcluidoLogicamente_Recusado()
    {
        ProcessoSeletivo processo = await RascunhosSemeadosParaTeste.SemearProcessoExcluidoAsync(_fixture, Guid.CreateVersion7());
        DocumentoAPublicar documento = await SemearConfirmadoAsync(processo.Id);

        Func<Task> apagar = () => ExecutarAsync(ApagarDocumento, documento.Id);

        (await apagar.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(RestricaoDeRetencao);
        (await DocumentosDoDonoAsync(processo.Id)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Desconfirmar o documento de um rascunho continua recusado")]
    public async Task UpdateCru_DesconfirmarDocumentoDeRascunho_Recusado()
    {
        Guid rascunhoId = await RascunhosSemeadosParaTeste.SemearRascunhoAsync(_fixture, Guid.CreateVersion7());
        DocumentoAPublicar documento = await SemearConfirmadoAsync(rascunhoId);

        Func<Task> desconfirmar = () => ExecutarAsync(DesconfirmarDocumento, documento.Id);

        (await desconfirmar.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(RestricaoDeRetencao);
    }

    private async Task<DocumentoAPublicar> SemearConfirmadoAsync(Guid donoId)
    {
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(donoId);
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        contexto.DocumentosAPublicar.Add(documento);
        await contexto.SaveChangesAsync();
        return documento;
    }

    private async Task<IReadOnlyList<DocumentoAPublicar>> DocumentosDoDonoAsync(Guid donoId)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        return await leitura.DocumentosAPublicar.AsNoTracking().Where(d => d.ProcessoSeletivoId == donoId).ToListAsync();
    }

    private async Task ExecutarAsync(string sql, Guid documentoId)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("id", documentoId);
        comando.Parameters.AddWithValue("situacao", StatusArquivoEnviado.Pendente.Codigo());
        await comando.ExecuteNonQueryAsync();
    }
}
