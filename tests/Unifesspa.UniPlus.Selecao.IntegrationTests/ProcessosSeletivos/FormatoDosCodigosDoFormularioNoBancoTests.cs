namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

public sealed class FormatoDosCodigosDoFormularioNoBancoTests : IClassFixture<InscricaoDbFixture>
{
    private readonly InscricaoDbFixture _fixture;

    public FormatoDosCodigosDoFormularioNoBancoTests(InscricaoDbFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("UPDATE selecao.paginas_do_formulario SET codigo = @codigo WHERE id = (SELECT p.id FROM selecao.paginas_do_formulario p JOIN selecao.formularios_processo f ON f.id = p.formulario_processo_id WHERE f.processo_seletivo_id = @processo LIMIT 1)", "ck_paginas_do_formulario_codigo_formato", 1)]
    [InlineData("UPDATE selecao.grupos_dos_formularios SET codigo = @codigo WHERE processo_seletivo_id = @processo", "ck_grupos_dos_formularios_codigo_formato", 2)]
    [InlineData("UPDATE selecao.grupos_dos_formularios SET pagina_codigo = @codigo WHERE processo_seletivo_id = @processo", "ck_grupos_dos_formularios_pagina_codigo_formato", 3)]
    [InlineData("UPDATE selecao.termos_exigidos_formulario SET codigo = @codigo WHERE id = (SELECT id FROM selecao.termos_exigidos_formulario WHERE processo_seletivo_id = @processo LIMIT 1)", "ck_termos_exigidos_formulario_codigo_formato", 4)]
    [InlineData("UPDATE selecao.dados_dos_formularios SET pagina_codigo = @codigo WHERE id = (SELECT id FROM selecao.dados_dos_formularios WHERE processo_seletivo_id = @processo LIMIT 1)", "ck_dados_dos_formularios_pagina_codigo_formato", 5)]
    public async Task CodigoNoBanco_RecusaFormatoInvalidoEAceitaDigitos(string consulta, string restricao, int variante)
    {
        Guid processo = await GravarProcessoAsync(variante);
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(consulta, conexao);
        comando.Parameters.AddWithValue("processo", processo);
        comando.Parameters.AddWithValue("codigo", "A__B");

        PostgresException violacao = (await FluentActions.Awaiting(() => comando.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>()).Which;
        violacao.SqlState.Should().Be("23514");
        violacao.ConstraintName.Should().Be(restricao);
        comando.Parameters["codigo"].Value = "LEI_12711";
        (await comando.ExecuteNonQueryAsync()).Should().Be(1);
    }

    private async Task<Guid> GravarProcessoAsync(int variante)
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico(variante);
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        contexto.ProcessosSeletivos.Add(processo);
        await contexto.SaveChangesAsync();
        return processo.Id;
    }
}
