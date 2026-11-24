namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;

/// <summary>
/// As linhas filhas da inscrição (respostas, ocorrências de grupo, endereços e aceites) ficam na coluna do tipo com a
/// origem (ADR-0141), a regravação as sincroniza sem colidir no índice único, a remoção as apaga e a consulta por fato
/// e valor usa índice.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class LinhasDaInscricaoGravadaTests
{
    private const string ColunasPorTipo = """
        SELECT codigo_do_dado, posicao, origem, valor_booleano, valor_numero, valor_codigo, valor_data
        FROM selecao.respostas_da_inscricao WHERE inscricao_id = @id
        """;

    private const string PlanoDaConsultaPorFatoEValor = """
        SET enable_seqscan = off;
        EXPLAIN SELECT inscricao_id FROM selecao.respostas_da_inscricao
        WHERE codigo_do_dado = 'CONCORRER_RENDA' AND valor_booleano = true
        """;

    private readonly CenarioDaInscricaoGravada _cenario;

    public LinhasDaInscricaoGravadaTests(InscricaoDbFixture fixture) => _cenario = new CenarioDaInscricaoGravada(fixture);

    [Fact(DisplayName = "Cada valor fica na coluna do tipo, com a origem: informado pelo candidato ou derivado")]
    public async Task Submeter_GravaCadaValorNaColunaDoTipoComAOrigem()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        Enviar(original, "000321", new ConteudoDoEnvio
        {
            Respostas = RespostasDeCadaTipo(),
            Derivados = Respostas(("FAIXA_ETARIA", 25), ("UF_RESIDENCIA", "PA")),
        }).IsSuccess.Should().BeTrue();
        await _cenario.GravarAsync(original);

        await ConferirAsColunasPorTipoAsync(original.Id);
        await ConferirAInscricaoRelidaAsync(original.Id);
    }

    [Fact(DisplayName = "Remover apaga a inscrição e todas as linhas filhas: respostas, ocorrências de grupo e aceites")]
    public async Task Remover_ApagaAInscricaoEAsLinhasFilhas()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        original.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(("NOME_SOCIAL", "Ariel")), Familia("membro-1"), [AceiteDoTermoDeVeracidade()]), Admitidas, PrecondicaoDe(original));
        await _cenario.GravarAsync(original);
        await RemoverAsync(original.Id);

        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        (await leitura.Inscricoes.AnyAsync(r => r.Id == original.Id)).Should().BeFalse();
        (await leitura.RespostasDaInscricao.AnyAsync(r => r.InscricaoId == original.Id)).Should().BeFalse();
        (await leitura.EnderecosDaInscricao.AnyAsync(r => r.InscricaoId == original.Id)).Should().BeFalse();
        (await leitura.AceitesDaInscricao.AnyAsync(r => r.InscricaoId == original.Id)).Should().BeFalse();
        (await leitura.OcorrenciasDeGrupoDaInscricao.AnyAsync(r => r.InscricaoId == original.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "Regravar troca, acrescenta e remove linhas, inclusive ocorrências de grupo, sem colidir no índice único")]
    public async Task Atualizar_SincronizaAsLinhas()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        original.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(("NOME_SOCIAL", "Ariel"), ("MODALIDADES", new[] { "AC", "LB_PPI" })), Familia("membro-1", "membro-2"), []), Admitidas, PrecondicaoDe(original));
        await _cenario.GravarAsync(original);

        await RegravarComOutrasRespostasEOutroGrupoAsync(original.Id);

        Inscricao relida = await _cenario.LerAsync(original.Id);
        relida.Formulario.Preenchimento.Respostas.Should().NotContainKey("NOME_SOCIAL");
        relida.Formulario.Preenchimento.Respostas["MODALIDADES"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("LB_PPI");
        relida.Formulario.Preenchimento.Grupos["COMPOSICAO_FAMILIAR"].Select(static o => o.Id).Should().Equal("membro-2", "membro-3");
        relida.Formulario.Preenchimento.Grupos["COMPOSICAO_FAMILIAR"][1].Respostas["RG_NUMERO"].GetString().Should().Be("rg-membro-3");
    }

    [Fact(DisplayName = "A consulta de candidatos por fato e valor usa índice, sem operador JSON")]
    public async Task ConsultaPorFatoEValor_UsaIndice()
    {
        await using NpgsqlConnection conexao = await _cenario.AbrirConexaoAsync();
        await using NpgsqlCommand comando = new(PlanoDaConsultaPorFatoEValor, conexao);
        List<string> plano = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            plano.Add(leitor.GetString(0));
        }

        string.Join('\n', plano).Should().Contain("ix_respostas_da_inscricao_dado_booleano");
    }

    private static Dictionary<string, JsonElement> RespostasDeCadaTipo()
    {
        Dictionary<string, JsonElement> respostas = Respostas(
            ("DESEJA_NOME_SOCIAL", false),
            ("DATA_NASCIMENTO", "2001-03-09"),
            ("MODALIDADES", new[] { "LB_PPI", "AC" }));
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.Deserialize<JsonElement>(
            """{"cep":"68500000","logradouro":"Folha 31","cidade":{"codigoIbge":"1504208","nome":"Marabá","uf":"PA"},"origem":"geo-api"}""");

        return respostas;
    }

    private static object?[] ColunasDaLinha(NpgsqlDataReader leitor)
    {
        object?[] colunas = new object?[leitor.FieldCount];
        leitor.GetValues(colunas!);

        return colunas;
    }

    private async Task ConferirAsColunasPorTipoAsync(Guid inscricaoId)
    {
        List<object?[]> todas = await _cenario.ConsultarAsync(ColunasPorTipo, inscricaoId, ColunasDaLinha);
        Dictionary<string, List<object?[]>> linhas = todas.GroupBy(static l => (string)l[0]!).ToDictionary(static g => g.Key, static g => g.ToList());

        linhas["DESEJA_NOME_SOCIAL"].Single()[3].Should().Be(false);
        linhas["DATA_NASCIMENTO"].Single()[6].Should().Be(new DateOnly(2001, 3, 9));
        linhas["MODALIDADES"].Select(static l => ((int)l[1]!, (string)l[5]!)).Should().BeEquivalentTo([(0, "LB_PPI"), (1, "AC")]);
        linhas["FAIXA_ETARIA"].Single()[4].Should().Be(25m);
        linhas["UF_RESIDENCIA"].Single()[2].Should().Be("CALCULADA");
        linhas["DATA_NASCIMENTO"].Single()[2].Should().Be("INFORMADA");
    }

    private async Task ConferirAInscricaoRelidaAsync(Guid inscricaoId)
    {
        Inscricao lida = await _cenario.LerAsync(inscricaoId);

        lida.Formulario.Derivados.Should().ContainKey("UF_RESIDENCIA");
        lida.Formulario.Preenchimento.Respostas.Should().NotContainKey("UF_RESIDENCIA");
        lida.Formulario.Preenchimento.Respostas["MODALIDADES"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("LB_PPI", "AC");
        lida.Formulario.Preenchimento.Respostas["ENDERECO_RESIDENCIAL"].GetProperty("cidade").GetProperty("codigoIbge").GetString().Should().Be("1504208");
    }

    private async Task RemoverAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);
        await repositorio.RemoverAsync((await repositorio.ObterPorIdAsync(inscricaoId))!);
        await contexto.SaveChangesAsync();
    }

    private async Task RegravarComOutrasRespostasEOutroGrupoAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        InscricaoRepository repositorio = _cenario.Repositorio(contexto);
        Inscricao lida = (await repositorio.ObterPorIdAsync(inscricaoId))!;
        Dictionary<string, JsonElement> mudanca = Respostas(("MODALIDADES", new[] { "LB_PPI" }));
        mudanca["NOME_SOCIAL"] = JsonSerializer.SerializeToElement<string?>(null);
        lida.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), mudanca, Familia("membro-2", "membro-3"), []), Admitidas, PrecondicaoDe(lida)).IsSuccess.Should().BeTrue();
        await repositorio.AtualizarAsync(lida);
        await contexto.SaveChangesAsync();
    }
}
