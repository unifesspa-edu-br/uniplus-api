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

    [Fact(DisplayName = "As definições das respostas da versão fixada marcam como número de documento os fatos Identificador do grafo reidratado, inclusive o de grupo")]
    public async Task Handle_ComProcessoPublicadoEDivulgadoReal_DefinicoesDasRespostasMarcamOsFatosIdentificador()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Handle_ComProcessoPublicadoEDivulgadoReal_DefinicoesDasRespostasMarcamOsFatosIdentificador));

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond));

        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        iniciar.Value!.Respostas.Keys.Should().Equal(["CPF"], "a inscrição nasce em rascunho só com o CPF da conta");

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        InscricaoRepository readRepository = _cenario.NovoRepositorioDeInscricao(readContext);
        Inscricao? gravada = await readRepository.ObterPorIdAsync(iniciar.Value!.Id);

        gravada.Should().NotBeNull();
        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            gravada!.VersaoFormularioId, processoId, ProcessoSeletivoRepositoryDeTeste.Criar(readContext), RegistroCodecs, ofertaCursoId: null, null, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);
        contexto.Value!.Admitidas.Definicoes.Where(static f => f.Value.Identificador).Select(static f => f.Key).Should().BeEquivalentTo(
            ["CPF", "RG_NUMERO", "PASSAPORTE_NUMERO", "RNM_NUMERO", "RG_NUMERO_MEMBRO"],
            "o conjunto junta os fatos Identificador do conjunto básico do candidato (CPF, RG, passaporte e RNM) " +
            "com o do campo de grupo declarado por este teste — da classificação CONGELADA na publicação, não do catálogo vivo");
        contexto.Value!.Admitidas.Definicoes.Where(static f => f.Value.CifraIndividual).Select(static f => f.Key).Should().BeEquivalentTo(
            ["IDENTIDADE_GENERO", "ORIENTACAO_SEXUAL"],
            "só a identidade de gênero e a orientação sexual têm cifra individual: o sexo, o sexo civil e a cor ou raça ficam em claro");
    }

    [Fact(DisplayName = "Salvar um rascunho sobre a versão fixada cifra o RG e o RG do membro do grupo, sem expor nada em texto claro na linha crua")]
    public async Task Salvar_SobreVersaoFixadaReal_CifraDocumentosDeNivelSuperiorEDeGrupo()
    {
        ProcessoSeletivo processo = NovoProcessoDeInscricao(
            nameof(Salvar_SobreVersaoFixadaReal_CifraDocumentosDeNivelSuperiorEDeGrupo));

        await using UniPlusDbContext writeContext = _cenario.CriarDbContext();
        (Guid processoId, _) = await PublicarEDivulgarAsync(writeContext, processo);

        InscricaoRepository inscricaoRepository = _cenario.NovoRepositorioDeInscricao(writeContext);
        Result<InscricaoDto> iniciar = await _cenario.IniciarRascunhoAsync(writeContext, processoId, UsuarioComCpf(PersonasDeTeste.KevinCarvalho));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);

        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(iniciar.Value!.Id);
        inscricao.Should().NotBeNull();
        RespostasAdmitidas admitidas = (await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao!.VersaoFormularioId, processoId, ProcessoSeletivoRepositoryDeTeste.Criar(writeContext), RegistroCodecs, ofertaCursoId: null, null, CancellationToken.None))
            .Value!.Admitidas with
        { NomeCivilDoLogin = PersonasDeTeste.NomeFernandaDrumond };

        inscricao!.Salvar(
            null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("30.111.222-9"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("40.333.444-5"),
                        ["PARENTESCO"] = JsonSerializer.SerializeToElement("FILHO"),
                    }),
                ],
            },
            [],
            admitidas,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        await inscricaoRepository.AtualizarAsync(inscricao, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        await using NpgsqlConnection connection = new(_cenario.StringDeConexao);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.fato_codigo, r.valor_texto, r.valor_cifrado
            FROM selecao.inscricoes i JOIN selecao.respostas_da_inscricao r ON r.inscricao_id = i.id
            WHERE i.id = @id AND r.fato_codigo IN ('RG_NUMERO', 'RG_NUMERO_MEMBRO')
            """;
        command.Parameters.AddWithValue("id", inscricao.Id);
        int linhas = 0;
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                linhas++;
                reader["valor_texto"].Should().Be(DBNull.Value, "o número de documento nunca fica na coluna de texto em claro");
                Encoding.UTF8.GetString((byte[])reader["valor_cifrado"]).Should().NotContain("30.111.222-9").And.NotContain("40.333.444-5");
            }
        }

        linhas.Should().Be(2, "o RG de nível superior e o do membro do grupo têm cada um a sua linha cifrada");

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        InscricaoRepository readRepository = _cenario.NovoRepositorioDeInscricao(readContext);
        Inscricao lida = (await readRepository.ObterPorIdAsync(inscricao.Id))!;
        lida.Respostas["RG_NUMERO"].GetString().Should().Be("30.111.222-9");
        lida.Grupos["COMPOSICAO_FAMILIAR"].Single(o => o.Id == "membro-1").Respostas["RG_NUMERO_MEMBRO"].GetString().Should().Be("40.333.444-5");
    }
}
