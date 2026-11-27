namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A marca "organizada em chamadas" na fase do cronograma do envelope: a chave existe em toda fase, a marca é
/// restaurada fielmente, muda os bytes e o hash, e a gramática fechada da fase recusa a chave ausente e o valor
/// que não é booleano.
/// </summary>
public sealed class CronogramaComAFaseOrganizadaEmChamadasTests
{
    private const string Chave = "organizadaEmChamadas";

    private static FaseCronograma Fase(bool organizada) => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new Guid("6666aaaa-0000-4000-8000-000000000003"),
        OrigensCanonicasDeTeste.Da(
            new Guid("4444dddd-0000-4000-8000-000000000003"), "HABILITACAO", OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, organizadaEmChamadas: organizada),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: null, Fim: null, UnidadeResponsavel: new UnidadeResponsavelSnapshot(new Guid("5555eeee-0000-4000-8000-000000000003"), "CEPS", "CEPS", "Centro de Processos Seletivos")),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    private static byte[] Bytes(FaseCronograma fase) => PerfilCanonicoV1.Instancia.Serializar(SerializacaoDoCronograma.SerializarFase(fase));

    private static JsonObject SacarFase(FaseCronograma fase) => JsonNode.Parse(Bytes(fase))!.AsObject();

    private static LeitorEnvelope Ler(JsonObject item, out IReadOnlyList<FaseCronograma> fases)
    {
        LeitorEnvelope leitor = new();
        fases = LeituraDoCronograma.LerCronogramaFases(leitor, new JsonObject
        {
            ["cronogramaFases"] = new JsonObject
            {
                ["origemCandidatos"] = OrigemCandidatos.InscricaoPropria.Codigo(),
                ["fases"] = new JsonArray(item),
            },
        });

        return leitor;
    }

    [Theory(DisplayName = "A fase grava a chave da marca, marcada ou não, e a restaura fielmente")]
    [InlineData(true)]
    [InlineData(false)]
    public void Fase_GravaERestauraAMarca(bool organizada)
    {
        JsonObject item = SacarFase(Fase(organizada));

        LeitorEnvelope leitor = Ler(item, out IReadOnlyList<FaseCronograma> fases);

        item[Chave]!.GetValue<bool>().Should().Be(organizada);
        leitor.Falhou.Should().BeFalse();
        fases.Single().OrganizadaEmChamadas.Should().Be(organizada);
    }

    [Fact(DisplayName = "A marca muda os bytes canônicos e o hash do envelope, e a mesma marca produz os mesmos bytes")]
    public void Marca_MudaBytesEHash()
    {
        byte[] marcada = Bytes(Fase(true));
        byte[] naoMarcada = Bytes(Fase(false));

        naoMarcada.Should().NotEqual(marcada);
        HashCanonicalComputer.ComputeSha256Hex(naoMarcada).Should().NotBe(HashCanonicalComputer.ComputeSha256Hex(marcada));
        Bytes(Fase(true)).Should().Equal(marcada);
    }

    [Fact(DisplayName = "A fase sem a chave da marca é recusada como envelope malformado: a escrita sempre a grava")]
    public void ChaveAusente_Recusada()
    {
        JsonObject item = SacarFase(Fase(true));
        item.Remove(Chave);

        LeitorEnvelope leitor = Ler(item, out _);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Contain(Chave);
    }

    [Fact(DisplayName = "A marca que não é booleana é recusada como envelope malformado")]
    public void MarcaQueNaoEBooleana_Recusada()
    {
        JsonObject item = SacarFase(Fase(true));
        item[Chave] = "sim";

        LeitorEnvelope leitor = Ler(item, out _);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }
}
