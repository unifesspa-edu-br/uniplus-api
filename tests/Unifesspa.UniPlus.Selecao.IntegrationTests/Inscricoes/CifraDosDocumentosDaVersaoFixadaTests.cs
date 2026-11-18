namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessosDeInscricaoDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>Os números de documento (fatos Identificador, no nível superior e no grupo) da versão fixada de um processo publicado e divulgado: o que as definições das respostas marcam e a cifra do que o rascunho grava. O fato Identificador declarado (RG do candidato e do membro do grupo) é genérico, porque o catálogo semeado da Medicina ainda não declara nenhum fato Identificador próprio; a Medicina continua sendo um caso de validação da regra genérica.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class CifraDosDocumentosDaVersaoFixadaTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public CifraDosDocumentosDaVersaoFixadaTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "As definições da versão fixada distinguem identificadores e campos com cifra individual")]
    public async Task Handle_ComProcessoPublicadoEDivulgadoReal_DefinicoesDasRespostasMarcamOsFatosIdentificador()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Handle_ComProcessoPublicadoEDivulgadoReal_DefinicoesDasRespostasMarcamOsFatosIdentificador));
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.FernandaDrumond);
        iniciar.Formulario.Respostas.Keys.Should().Equal(["CPF"]);
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        Inscricao? gravada = await _cenario.NovoRepositorioDeInscricao(leitura).ObterPorIdAsync(iniciar.Id);
        gravada.Should().NotBeNull();
        ContextoDaVersaoFixada contexto = await ContextoAsync(leitura, gravada!.Formulario.VersaoFormularioId, processoId);
        ConferirProtecoes(contexto);
    }

    private static void ConferirProtecoes(ContextoDaVersaoFixada contexto)
    {
        contexto.Admitidas.Definicoes.Where(static f => f.Value.Identificador).Select(static f => f.Key).Should().BeEquivalentTo(
            ["CPF", "RG_NUMERO", "PASSAPORTE_NUMERO", "RNM_NUMERO", "RG_NUMERO_MEMBRO"]);
        contexto.Admitidas.Definicoes.Where(static f => f.Value.CifraIndividual).Select(static f => f.Key).Should().BeEquivalentTo(
            ["IDENTIDADE_GENERO", "ORIENTACAO_SEXUAL"]);
    }

    [Fact(DisplayName = "Salvar sobre a versão fixada cifra os documentos do candidato e do membro do grupo")]
    public async Task Salvar_SobreVersaoFixadaReal_CifraDocumentosDeNivelSuperiorEDeGrupo()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(nameof(Salvar_SobreVersaoFixadaReal_CifraDocumentosDeNivelSuperiorEDeGrupo));
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        (Guid processoId, InscricaoDto iniciar) = await _cenario.PublicarEIniciarAsync(escrita, processo, PersonasDeTeste.KevinCarvalho);
        InscricaoRepository repositorio = _cenario.NovoRepositorioDeInscricao(escrita);
        Inscricao inscricao = (await repositorio.ObterPorIdAsync(iniciar.Id))!;
        ContextoDaVersaoFixada contexto = await ContextoAsync(escrita, inscricao.Formulario.VersaoFormularioId, processoId);
        SalvarDocumentos(inscricao, contexto.Admitidas);
        await repositorio.AtualizarAsync(inscricao, CancellationToken.None);
        await escrita.SaveChangesAsync(CancellationToken.None);
        await ConferirCifraAsync(inscricao.Id);
        await ConferirReleituraAsync(inscricao.Id);
    }

    private static void SalvarDocumentos(Inscricao inscricao, RespostasAdmitidas admitidas) =>
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["RG_NUMERO"] = JsonSerializer.SerializeToElement("30.111.222-9") }, RespostasDoGrupo(), []), admitidas with { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond }, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> RespostasDoGrupo() => new(StringComparer.Ordinal)
    {
        ["COMPOSICAO_FAMILIAR"] =
        [
            new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("40.333.444-5"),
                ["PARENTESCO"] = JsonSerializer.SerializeToElement("FILHO"),
            }),
        ],
    };

    private async Task ConferirCifraAsync(Guid inscricaoId)
    {
        await using NpgsqlConnection conexao = new(_cenario.StringDeConexao);
        await conexao.OpenAsync();
        await using NpgsqlCommand command = conexao.CreateCommand();
        command.CommandText = """
            SELECT r.fato_codigo, r.valor_texto, r.valor_cifrado
            FROM selecao.inscricoes i JOIN selecao.respostas_da_inscricao r ON r.inscricao_id = i.id
            WHERE i.id = @id AND r.fato_codigo IN ('RG_NUMERO', 'RG_NUMERO_MEMBRO')
            """;
        command.Parameters.AddWithValue("id", inscricaoId);
        (await ConferirLinhasCifradasAsync(command)).Should().Be(2);
    }

    private static async Task<int> ConferirLinhasCifradasAsync(NpgsqlCommand command)
    {
        int linhas = 0;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            linhas++;
            reader["valor_texto"].Should().Be(DBNull.Value);
            Encoding.UTF8.GetString((byte[])reader["valor_cifrado"]).Should().NotContain("30.111.222-9").And.NotContain("40.333.444-5");
        }
        return linhas;
    }

    private async Task ConferirReleituraAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext leitura = _cenario.CriarDbContext();
        Inscricao inscricao = (await _cenario.NovoRepositorioDeInscricao(leitura).ObterPorIdAsync(inscricaoId))!;
        inscricao.Formulario.Preenchimento.Respostas["RG_NUMERO"].GetString().Should().Be("30.111.222-9");
        inscricao.Formulario.Preenchimento.Grupos["COMPOSICAO_FAMILIAR"].Single(o => o.Id == "membro-1").Respostas["RG_NUMERO_MEMBRO"].GetString().Should().Be("40.333.444-5");
    }
}
