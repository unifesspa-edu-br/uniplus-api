namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CodigoDoGrupoNaInscricaoPersistenceTests : IClassFixture<InscricaoDbFixture>
{
    private const string CodigoDoGrupo = "ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGH";
    private readonly CenarioDaInscricaoGravada _cenario;

    public CodigoDoGrupoNaInscricaoPersistenceTests(InscricaoDbFixture fixture) => _cenario = new(fixture);

    [Fact]
    public async Task Gravar_GrupoComSessentaCaracteres_PreservaOcorrencia()
    {
        Inscricao inscricao = InscricaoComGrupo();

        await _cenario.GravarAsync(inscricao);
        Inscricao lida = await _cenario.LerAsync(inscricao.Id);

        CodigoDoGrupo.Should().HaveLength(60);
        lida.Formulario.Preenchimento.Grupos[CodigoDoGrupo].Should().ContainSingle()
            .Which.Id.Should().Be("membro-1");
    }

    private static Inscricao InscricaoComGrupo()
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            [CodigoDoGrupo] = [new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal))],
        };
        return Inscricao.Reidratar(Guid.CreateVersion7(), Guid.CreateVersion7(),
            TitularDaInscricao.Reidratar(PersonasDeTeste.ErickIsaacRamos, "conta-teste", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Rascunho, null, CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioDaInscricao.Reidratar(Guid.CreateVersion7(), PreenchimentoDoCandidato.Criar(
                OpcoesDeCursoDaInscricao.Reidratar(null, null), CenarioDaInscricaoGravada.Respostas(("CPF", PersonasDeTeste.ErickIsaacRamos.Valor)), grupos, []),
                new Dictionary<string, JsonElement>(StringComparer.Ordinal), CenarioDaInscricaoGravada.DefinicoesDasRespostas),
            1, new DatasDeGravacaoDaInscricao(default, null));
    }
}
